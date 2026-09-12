> **HISTORICAL SNAPSHOT / 历史快照**：本文是归档资料，只描述记录当时的计划、状态或审计判断。文中的“当前、唯一、已完成、未完成、风险、测试基线、HEAD”等均不得解释为现在的项目状态。当前事实请按 `docs/README.md` 的治理顺序核验。\n\n> **治理声明**：本文档是 `Master_Development_Plan.md` 的**输入 / 审计基线**，**不是**独立执行计划。所有里程碑状态、优先级与验收以 `Master_Development_Plan.md`（唯一事实来源）及其 `milestones/` 拆分文档为准；本文与 MDP 冲突时以 MDP 为准。映射见 MDP §17。
>
> **文档角色**：M0 收尾分析（映射到 M0）

# M0 发布门禁 —— 完成情况深度分析

> 生成日期：2026-09-04
> 方法：静态代码核查（实际读取实现与 `Program.cs` 接线）+ 全历史凭据扫描，**未重跑测试套件**。
> 纠正：此前"M0-02/03/05/06/08 未做"为误读被截断的旧记忆摘要所致，与代码事实矛盾。

---

## 一、结论摘要（判定）

- **实现完成度：9/9 项代码就绪并正确接线。**
- **生产就绪度：存在 1 个可用性缺陷（AUTH-1）+ 2 个工程缺陷（RL-1/RL-2）+ 1 个设计脆弱点（AUTH-2），且全量回归测试未复验。**
- **建议判定：代码达成，待缺陷修复与回归复验后正式验收**，而非无条件"全绿"。

---

## 二、评级总表

| 项 | 内容 | 评级 | 关键证据 |
|---|---|---|---|
| M0-01 | 凭据轮换 | ✅ 已闭环 | 历史 0 命中；强密钥 fail-fast；已强推 |
| M0-02 | 四路由契约 | ✅ 达成 | 21 控制器全 `/api/<svc>` 一致 |
| M0-03 | 权限守卫 | ✅ 达成 ⚠️ 无兜底 | `AuthMiddleware` deny-by-default；大小写锁定（AUTH-1）；无 `[Authorize]`（AUTH-2） |
| M0-04 | Ask 旗舰对话可靠性 | 🟡 代码层达成，待复验 | 默认路径确定性；`/ask/refine` 门控隔离 |
| M0-05 | 受控 Migration | ✅ 达成（设计质量高） | `SchemaProbe` 三步 + 固定顺序种子 + 异常隔离 |
| M0-06 | 字段越权与关系链 | ✅ 达成（纵深最佳） | tid 来自令牌；生效租户恒=认证租户；行级 403 |
| M0-07 | 测试基线记录 | ✅ 已记录 | 记忆 476/476、Golden 18/18（需确认含 M0 安全测试） |
| M0-08 | 匿名端点与限流 | ✅ 功能达成 ⚠️ 两缺陷 | 白名单合理；限流键安全；**内存泄漏（RL-1）+ 单实例（RL-2）** |
| M0-09 | Ask 旗舰对话（UI/多轮） | ✅ 已完成 | 同 M0-04，待回归 |

---

## 三、逐项深析

### M0-01 凭据轮换 ✅
- **证据**：`git log --all -S` 对 5 类明文签名全 0 提交；`appsettings.Local.json` 已被 `.gitignore` 忽略；`Program.cs:226-230` 经 `AuthSigningKeyPolicy.Validate` **强制密钥非空且非开发环境 fail-fast**；外部轮换已确认；已强推远程。
- **深层**：无状态令牌是**对称密钥模型**，`Auth:SigningKey` 即根信任。本地密钥已改为强随机，但须确认**部署环境变量**（`Qwen__ApiKey`、`ConnectionStrings__WmsMySql`、`Auth:SigningKey`）也同步轮换——否则仅本地安全、生产仍用旧值。

### M0-02 四路由契约 ✅
- **证据**：21 个 API 控制器全部 `[Route("api/<service>")]` 一致前缀；四大旗舰域 `api/agent`、`api/apps`、`api/ask`、`api/data-sources` 命名规范统一，管理面 `api/identity`、`api/quota`、`api/audit`、`api/tenant-management` 等齐整。
- **深层**：契约一致，但**无集中的路由契约测试/文档**防止未来漂移（Golden 覆盖查询契约，未覆盖路由命名契约）。

### M0-03 权限守卫 ✅⚠️
- **证据**：`AuthMiddleware` 在 `Program.cs:380` 接线于路由之后、授权之前；`/api` 缺令牌一律 401（deny-by-default）；`TokenService` 用 HMAC-SHA256 + `FixedTimeEquals` 防时序攻击（`:95-98`）；`SecurityStamp` 令牌吊销到位（`:77-98`）；权限码在签名载荷内防篡改。
- **深层发现**：
  - **AUTH-1（可用性缺陷 / P0）**：`IsAnonymousPath`（`AuthMiddleware:187-212`）用 `path.StartsWithSegments("/api/auth/login")`，而 `PathString.StartsWithSegments` 默认 **Ordinal 区分大小写**，但 ASP.NET 路由大小写不敏感。客户端发 `/API/auth/login`（代理改写/手输）会被要求令牌 → **登录死锁**。不影响安全，但暴露中间件与路由层不一致。
  - **AUTH-2（设计脆弱点 / P1）**：全站控制器**无 `[Authorize]`**，鉴权 100% 依赖中间件对 `/api` 前缀的判断。白名单 `IsAnonymousPath` 一旦被误改，或新增非 `/api` 敏感端点，即整体绕过。建议对管理面补 `[Authorize(Policy=...)]` 作第二层 Defense-in-Depth。

### M0-04 Ask 旗舰对话可靠性 🟡
- **证据**：`AskController` 默认路径确定性（不调 LLM）；`/api/ask/refine` 显式门控隔离（`:129-130` 注释"默认路径逐字节不变"）；语义缓存 60s TTL（Program.cs:247-257）。
- **深层**：可靠性属功能/测试层，本会话未重跑 Golden 18/18 与 476/476。门控隔离设计正确，但端到端稳定性需回归测试固化。

### M0-05 受控 Migration ✅（设计质量高）
- **证据**：`SchemaProbe` 三步（可达性 → 可选迁移 → 迁移历史，区分 DB 不可达 / Schema 未创建）；固定顺序种子 Identity→Localization→Quota→Bootstrap（`Program.cs:290-368`），每步独立 try/catch 异常隔离；`/health` 暴露诊断状态。
- **深层**：默认 `MigrateOnStartup=false`（CI/部署工具负责），首部署若未预迁移则进入 degraded 受限模式但仍启动——**正确的受控行为**。
  - **小建议**：`Program.cs:391` 把 `SeedIncomplete` 也算 `healthy`（HTTP 200），可能让编排探针误判；建议 `SeedIncomplete` 返回独立 degraded 状态码（如 207/503）以便区分。

### M0-06 字段越权与关系链 ✅（全 M0 最扎实）
- **证据**：
  - 租户 Id 来自令牌 `tid` 声明，非请求体（`DataSourceAccessController:74` `User.FindFirst("tid")`；`AuthMiddleware:104`）。
  - 生效租户恒=认证租户（`TenantDataPlanePolicy.Resolve:167` 与 `ResolvePlatformScope:202` 均 `effectiveTenantId = authenticatedTenantId`）；query/header 的 `tenantId` 仅记为"请求值"，绝不作为生效租户（代码注释明确）。
  - 行级安全 `RowLevelSecurityService.ApplyAsync`：无 Allow 规则即抛 403（`:57`）。
- **深层**：三层防护（中间件 + 控制器 scope + 行级服务）一致，纵深到位。小瑕疵：`plan.Tables` 为空时跳过行级策略应用但不报错（无表即无数据，不构成越权），可接受。

### M0-07 测试基线记录 ✅
- **深层**：基线数字在记忆中演进（431→468→476），应以最新 476/476 为准，并确认其**已包含 M0 安全相关测试**（鉴权/限流/越权）。

### M0-08 匿名端点与限流 ✅⚠️
- **证据**：
  - 匿名白名单合理：`/`、`/health`、`/api/auth/login`、`/api/platform-bootstrap`、`/api/localization/public`、静态资源；`/metrics`、`/test`、`/metadata`、`/qdrant` 需平台诊断权限（`DiagnosticsAccessPolicy`，权限**仅来自令牌 claims**，不信任请求头 ✅）。
  - 限流键基于 `TenantId+UserId`（鉴权后）或 `IP+UA 哈希`（匿名），不依赖可伪造令牌头（`RateLimitMiddleware:94-107`）✅。
  - 反向代理默认不消费 `X-Forwarded-*`（KnownProxies 空，`Program.cs:271-281`）✅，避免伪造客户端 IP 退化共享桶。
- **深层缺陷**：
  - **RL-1（内存泄漏 / P1）**：`Counters` 是 `static ConcurrentDictionary`（`:26`），**永不清理**——每个唯一 IP+UA 或用户永久驻留，长运行 + 大量匿名客户端持续膨胀。
  - **RL-2（单实例有效 / P1）**：固定窗口基于进程内存，多实例（LB）下各实例独立计数，攻击者分散请求即可绕过限流；且 `AddOrUpdate` 的 update 委托含 `existing.Count++` 副作用（并发下计数精度偏差，仅精度非崩溃）。
  - 登录路径有更严阈值（防爆破）✅，但同样受 RL-1/RL-2 约束。
  - **建议**：改为带 TTL 的滑动窗口计数器（如 `IMemoryCache` + sliding expiration），或接入分布式限流（Redis）；**多实例部署前必须解决**。

### M0-09 Ask 旗舰对话（UI/多轮） ✅
- **深层**：同 M0-04，端到端需回归复验。

---

## 四、跨项系统性风险

1. **集中式中间件鉴权缺乏 Defense-in-Depth（AUTH-2）**：M0-03/06/08 共担的系统性脆弱点。一旦中间件逻辑回归或白名单误改，全站失守。管理面（tenant-management、identity、audit、quota）应补 `[Authorize]` 或基于 Permission 的授权处理器。
2. **无状态令牌的密钥即信任根**：所有权限（含 platform 治理角色）都在签名载荷内，密钥泄露=完全沦陷。应确保密钥走密钥管理（非仅文件），并考虑短期令牌 + 吊销列表（已有 SecurityStamp 但仅覆盖新令牌）。
3. **测试复验缺口**：本会话只做静态核查 + 历史扫描，未重跑测试套件。各项"达成"应被 Golden/单测固化。

---

## 五、优先修复清单（已于 2026-09-04 实施并验证）

| 优先级 | 项 | 修复实现 | 状态 |
|---|---|---|---|
| **P0** | AUTH-1 路径大小写（**定性为鉴权绕过**） | 统一 `StringComparison.OrdinalIgnoreCase`：新增 `IsPrefix` 辅助并覆盖 `AuthMiddleware`（白名单 + `/api` 判定）、`RateLimitMiddleware`、`TenantDataPlanePolicy.IsDataPlanePath`、`DiagnosticsAccessPolicy.GetRequiredPermission` 共四处 | ✅ 已修复 |
| **P1** | RL-1 限流内存泄漏 | 新增 `IRateLimitStore` / `MemoryRateLimitStore`（`src/Api/Middleware/RateLimitStore.cs`），后台 Timer 定期逐出过期窗口；移除原永不清理的 `static` 字典 | ✅ 已修复 |
| **P1** | RL-2 单实例局限 + 计数副作用 | 计数结构改为不可变 `readonly record struct`（`AddOrUpdate` 更新委托无副作用，避免并发计数失真）；存储抽象化以支持替换分布式实现 | ✅ 架构就绪（见下方遗留） |
| **P1** | AUTH-2 缺授权兜底 | 新增 `ApiAuthorizationFilter`（`src/Api/Security/`），作为全局 MVC 过滤器二次校验 `/api` 主体已认证；尊重 `[AllowAnonymous]`；`HttpContext` 为 null（单元测试直调）时静默跳过 | ✅ 已修复 |
| P2 | M0-05 `/health` SeedIncomplete 状态码 | 未做（低风险，待排期） | ⬜ 未实施 |
| P2 | M0-02 路由契约 Golden 测试 | 未做（治理项，待排期） | ⬜ 未实施 |
| P2 | 全量回归复验 | `dotnet test` **506/506 通过，0 失败**（基线 504 + 新增 2 个 RL 回归用例） | ✅ 已完成 |

### 配套变更
- `Program.cs`：注册 `IRateLimitStore`（singleton）；注册 `ApiAuthorizationFilter` 为全局 MVC 过滤器。
- `LocalizationController`：`public/languages`、`public/texts` 补 `[AllowAnonymous]`——否则新的兜底过滤器会破坏"登录前加载公共语言"这一既有能力。
- 测试 `RateLimitMiddlewareTests`：`Build` 适配注入式存储（各用例持有独立 store，天然隔离，不再依赖唯一 IP/组合规避污染）；新增 2 个回归用例——① 窗口过期后计数重置为 1；② 过期条目被逐出（直接断言 `store.Count == 0`）。

### RL-2 遗留（部署前置条件，非代码缺陷）
默认 `MemoryRateLimitStore` 仍为**进程内存、仅单实例有效**。存储虽已抽象为 `IRateLimitStore`（可实现并替换注册），但**多实例（负载均衡）部署前必须提供分布式实现**（如 Redis 滑动窗口计数），否则各实例独立计数、攻击者分散请求即可绕过限流。

---

## 六、最终判定（2026-09-04 更新）

M0 九项在**代码层面均已落地且正确接线**（尤其 M0-06 纵深防御、M0-05 受控启动、M0-08 限流键安全）。本节四项待办**已全部实施并通过回归验证**。

### 验证证据
| 验证项 | 结果 |
|---|---|
| 后端构建 | **0 error**（23 个既有空引用警告，非本次引入） |
| 测试套件 | **506/506 通过，0 失败，0 跳过** |
| 与基线对比 | 504 → 506（+2 为新增 RL 回归用例），**零回归** |

### 修复要点回顾
1. **AUTH-1（P0，实为安全漏洞）**：`PathString.StartsWithSegments` 区分大小写而路由不敏感，导致 `/API/ask` 可绕过鉴权与租户隔离却被路由命中；同时 `/API/auth/login` 无法进入白名单造成登录死锁。已统一为 `OrdinalIgnoreCase`，覆盖四处判定。
2. **RL-1（P1）**：`static` 字典永不清理 → 内存随历史客户端数无限增长。改为带定期清理的存储实现。
3. **RL-2（P1）**：`AddOrUpdate` 更新委托含 `Count++` 副作用（并发计数失真）+ 单实例局限。改为不可变计数结构，并抽象 `IRateLimitStore` 支持分布式替换。
4. **AUTH-2（P1）**：全站无 `[Authorize]`、鉴权完全依赖中间件。新增控制器层授权兜底过滤器。

### 仍需注意
- **RL-2 部署前置**：多实例上线前必须接入分布式限流实现（默认内存实现仅单实例有效）。
- **Golden 运行时契约（18/18）**：本次执行的是单元/集成测试套件；Golden 契约需启动服务后经 `/evaluation/golden-runtime` 端点复验（依赖 Qdrant 与数据库），建议部署前单独跑一次。
- **P2 两项**（`/health` 状态码语义、路由契约 Golden 测试）为治理优化，未实施，不影响当前验收。

**结论：M0 可判定为代码达成且缺陷已修复，测试回归全绿。**
