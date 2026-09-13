# 缓存与授权一致性说明（CACHE-01 / M13-16）

本文说明 SuperBuilder AI 中**令牌、缓存、会话、数据源授权**在「授权变更 / 进程重启 / 双实例交替请求」下的一致性模型与失效策略，供运维与验收核对「过期授权不被旧令牌/缓存复用；禁止跨用户/租户复用」。

## 0. 总览

| 资产 | 存储 | 失效/隔离机制 | 进程重启 | 双实例交替请求 |
|---|---|---|---|---|
| 访问令牌 | 无状态（HMAC 签名，载荷内嵌权限） | 载入安全戳，`AuthMiddleware` 每请求比对 DB 当前安全戳 | 令牌仍有效（按 `exp` 过期），安全戳持久于库 → 一致 | 一致（无本地状态） |
| 有效权限 | 数据库（`UserRole` ∪ 组角色） | 令牌签发时快照；变更即轮换安全戳使其失效 | — | 一致 |
| Ask 语义缓存 | 进程内 `IMemoryCache` | 键含租户+数据源+问题哈希 + 7 维版本上下文；授权在缓存查询之前 | 丢失（退化为未命中，正确性不受影响） | 各自独立；键正确隔离 → 不串数据，仅可能未命中 |
| 数据源授权（Grant） | 数据库 | 每次请求实时解析，**无缓存** → 撤权立即生效 | — | 一致 |
| Ask 澄清会话 | 进程内 `ConcurrentDictionary`（Singleton） | 键 `conversationId`，以 (tenantId,userId) 校验；30 分钟 TTL | 丢失（澄清态失效，退化为新问题） | 不共享（下一请求落到另一实例则退化为新问题） |

> 结论：**授权正确性**在重启/双实例下均保持一致（令牌无状态 + 安全戳/授权实时查库）；**缓存与会话**为进程内，跨实例不共享，但均已做租户/用户隔离，最坏退化为一次未命中，不产生越权。

## 1. 令牌与授权撤销（安全戳）

- 令牌为无状态 HMAC-SHA256，载荷含 `sub`（用户）、`tid`（生效租户）、`htid`（归属主租户）、`perms`（**签发时**的权限码快照）、`sec`（安全戳）、`iat`/`exp`（默认 60 分钟）。
- `AuthMiddleware` 在每个受保护请求上：校验签名与过期 → **以 `htid` 定位用户行，比对库中 `SecurityStamp` 与令牌 `sec`**；不一致即视为已吊销，返回 `401`（`token-revoked`）。同时校验归属租户与生效租户的启用状态。
- 因此「权限变更后旧令牌不得复用」依赖**变更操作轮换安全戳**。已轮换安全戳的操作：

| 操作 | 位置 | 说明 |
|---|---|---|
| 口令变更 | `IdentityService.SetPasswordAsync` | 全部旧令牌失效 |
| 用户停用/启用 | `IdentityService.SetUserStatusAsync` | 状态机 Active↔Disabled |
| 直接角色 指派/撤销 | `IdentityService.AssignRoleAsync` / `RevokeRoleAsync` | `UserRole` 变更 |
| **组角色集修改** | `IdentityDirectoryService.SetUserGroupRolesAsync` | **CACHE-01 补齐**：轮换该组全体成员 |
| **组停用/启用** | `IdentityDirectoryService.SetUserGroupEnabledAsync` | **CACHE-01 补齐**：停用即整体收回该组角色权限 |
| **成员加入/移出** | `IdentityDirectoryService.AddUserGroupMemberAsync` / `RemoveUserGroupMemberAsync` | **CACHE-01 补齐**：轮换被变更用户 |
| **删除用户组** | `IdentityDirectoryService.DeleteUserGroupAsync` | **CACHE-01 补齐**：轮换全部成员 |

- **为何需要 CACHE-01**：有效权限 = 直接角色 ∪ **组角色**（仅**已启用**组），见 `IdentityService.GetPermissionsAsync`。组相关变更**不触碰成员的直接角色**，若只改组成员/角色而不轮换安全戳，成员旧令牌中的 `perms` 将在最长 60 分钟内继续生效（撤权延迟 = 安全问题）。
- **租户隔离**：轮换语句显式 `TenantId` 过滤，仅影响目标租户内的目标用户，绝不跨租户。
- **去抖**：组角色集**未变化**时不轮换（避免无谓强制重登）；停用/启用仅在状态**确有变化**时轮换。
- **遗留令牌**（未携带安全戳，迁移窗口内）不参与吊销校验，最长 60 分钟自然过期。

## 2. Ask 语义缓存（`MemoryAskResponseCache`）

- **键构成**：`ask:{tenantId}:{dataSourceId}:{sha256(归一化问题)}`，并由 `IAskCacheVersionProvider` 叠加 **7 维版本上下文**：权限指纹（= 用户**授权数据源集合**指纹）、策略/RLS 指纹、语言、模型、语义、元数据、数据源目录。任一维度变化即换键，旧答案不复用。
- **授权先于缓存**：`AskController` 先 `HasPermissionAsync(dashboard:view)` 与授权数据源判定（403 前置），再查缓存 → 未授权用户不可能命中他人缓存。
- **仅缓存成功响应**；失败/被闸门阻断的结果不入缓存，避免瞬时故障被钉死。TTL 可配，`?noCache=1` 旁路。
- **跨用户/租户**：键含 `tenantId + dataSourceId`，故不跨租户；同租户同数据源同问题由多用户共享——其答案由**租户级** RLS 决定（同一租户内各用户数据面一致），故非「跨用户泄露」。
- **双实例**：`IMemoryCache` 为进程内，两实例各自缓存；因键已正确隔离，最坏为一次未命中，不发生错误数据复用。

## 3. 数据源授权（Grant）

- `DataSourceAuthorizationService.GetAuthorizedDataSourceIdsAsync` **每次调用实时查库**（`UserRole`/`DataSourceAccessGrants` 联合数据源目录），**无内存/分布式缓存** → 通过 `GrantAsync`/`RevokeAsync`/`RevokeBySubjectAsync` 变更后，**下一次授权判定立即生效**。
- 因此「撤销数据源授权」不存在令牌/缓存复用窗口（与令牌吊销相互独立：前者即时，后者依赖安全戳轮换）。

## 4. 会话（Ask 澄清）

- `AskConversationService` 为**进程内** Singleton：`ConcurrentDictionary<conversationId, Pending>`，`Pending` 记录 `TenantId/UserId` 与 30 分钟 `ExpiresAt`。
- **隔离**：读取时若 `pending.TenantId != tenantId || pending.UserId != userId` 即视为无效并移除 → **禁止跨用户/租户复用**澄清上下文。
- **进程重启**：澄清态丢失；后续请求按新问题处理（无错误、无越权）——符合「按策略失效」。
- **双实例**：澄清态不跨实例共享；若澄清的下一轮请求落到另一实例，则退化为新问题（需重新澄清）。此为**已接受的设计取舍**：避免引入跨进程共享的、含用户问题的状态存储；如需跨实例续话，应引入按 (tenantId,userId,conversationId) 归属校验的分布式存储（列入后续增强，不在 CACHE-01 范围）。

## 5. 验证与回归

- 单测 `IdentityDirectorySecurityStampTests`（CACHE-01）覆盖：组角色集修改/组停用启用/成员增删/删组**均轮换受影响成员安全戳**，且**只轮换受影响者、不跨租户**；组角色集未变不轮换；端到端语义——撤销组角色后有效权限回落，且以旧戳签发的令牌安全戳与库中不一致（判定为已吊销）。
- 既有 `PasswordAndTokenRevocationTests` 覆盖口令/角色变更触发的令牌吊销；`AskControllerTests`/`M6-04` 相关用例覆盖 Ask 缓存键的 7 维失效。
