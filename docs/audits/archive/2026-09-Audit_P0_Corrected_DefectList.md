> **HISTORICAL SNAPSHOT / 历史快照**：本文是归档资料，只描述记录当时的计划、状态或审计判断。文中的“当前、唯一、已完成、未完成、风险、测试基线、HEAD”等均不得解释为现在的项目状态。当前事实请按 `docs/README.md` 的治理顺序核验。\n\n# SuperBuilder AI — 修正后 P0 缺陷清单（可下发版）

> **基线**：`master`　　**核验日期**：2026-09-01
> **来源**：对照工作区源码逐项核验《全量源码审计最终结论》后的修订版
> **修订原因**：原审计中 **P0-03 的机制描述** 与 **P0-44 的完成度** 与源码实际不符，本文为经源码验证的正确版本

---

## 一、修订摘要

| 原编号 | 原审计表述 | 修订结论 | 说明 |
|---|---|---|---|
| **P0-03** | 前端 `X-Tenant-Id` 透传决定租户，User A 改头即可查 Tenant B 数据 | ⚠️ **结论成立，机制错误** | Ask 数据路径**已由令牌驱动、安全**；真实缺陷在平台类控制器信任**请求体 `TenantId`** |
| **P0-44** | 缓存需增加 `DataSourceId` 维度 | ✅ **该项已实现，本项改写入库** | 缓存键已含 `DataSourceId`；仍缺权限上下文与版本维度，需继续补齐 |

> **重要**：P0-03 的严重等级**维持 P0 不变**——风险真实存在，只是攻击入口不是 `X-Tenant-Id` 头，而是请求体/查询串中的 `TenantId`。请勿因"Ask 路径安全"而降级。

---

## 二、P0-03：平台类控制器信任客户端 `TenantId` 定界数据（无租户成员校验）

### 基本信息

| 项 | 内容 |
|---|---|
| **缺陷编号** | P0-03（修订版） |
| **等级** | 🔴 **P0**（维持） |
| **类型** | 越权 / 多租户隔离 |
| **影响面** | Dashboard、App、Agent、Identity、Theme、Quota、Audit 七类平台资源 |
| **处置结论** | 需修复；Ask 核心路径无需改动 |

### 现状事实（三条，请勿混淆）

**① Ask 数据路径：已安全，无需修改。**

租户由**令牌声明**派生，与客户端请求头无关：

- `AuthMiddleware` 校验 Bearer 令牌后写入 `tid` 声明（`src/Api/Middleware/AuthMiddleware.cs:70`）
- `AskController.ResolveTenantId()` 读取 `User.FindFirst("tid")`（`src/Api/Controllers/AskController.cs:164-167`）
- 数据定界使用令牌租户：`BIConversationService` 调用 `_superBIContext.ApplyTenantScope(tenantId)`（`src/Application/BiQuery/BIConversationService.cs:179`）

**② `X-Tenant-Id` 头：不参与数据访问，仅用于打标。**

- 前端确实发送该头（`SuperBuilder_AI.Components/Services/ApiClient.cs:36`）
- 后端**仅** `ObservabilityMiddleware`（`:104`）与 `AuditMiddleware`（`:64`）读取，用于指标与审计日志的租户标注
- 篡改该头**只能污染监控/审计标签，无法改变查询哪个租户的数据**

> ⚠️ 原审计"User A 修改 `X-Tenant-Id` → 查到 Tenant B 数据"的断言 **不成立**。

**③ 真实缺陷：平台类控制器直接信任请求体/查询串的 `TenantId`。**

`ScopeTo(long requestedTenantId)` 的实现是把客户端传入值原样采纳，**不做"当前认证主体是否属于该租户"的校验**：

```csharp
// ThemeController.cs:49 / IdentityController.cs:56 / DashboardController.cs:70 / AuditController.cs:36
private long ScopeTo(long requestedTenantId)
{
    var tenantId = requestedTenantId > 0 ? requestedTenantId : 0;   // ❌ 无成员校验
    ...
}
```

### 缺陷位置（源码证据）

| 文件 | 行号 | 问题 |
|---|---|---|
| `src/Api/Controllers/DashboardController.cs` | 70（`ScopeTo` 定义）、99、107、126、143、160、196、215 | 信任 `request.TenantId` / `[FromQuery] tenantId` |
| `src/Api/Controllers/AppBuilderController.cs` | 62、86、124、140、156、173、208 | 同上 |
| `src/Api/Controllers/AgentController.cs` | 67、88、126、142、158、175、206 | 同上 |
| `src/Api/Controllers/IdentityController.cs` | 56、73、198、86、98、112、130、146、162、177、231、261 | 同上；且涉及角色/权限分配，风险更高 |
| `src/Api/Controllers/ThemeController.cs` | 49、70、97、113、144、171、192、244 | 同上 |
| `src/Api/Controllers/QuotaController.cs` | 30、38、47、56 | `[FromQuery] long tenantId`，含写操作 `Consume` |
| `src/Api/Controllers/AuditController.cs` | 36、38、46 | 同上 |

### 风险描述

已登录用户（持有任意合法令牌）在请求体或查询串中传入**他人租户 Id**，即可：

- 读取/写入/删除其他租户的 Dashboard、App、Agent 定义
- 查询/消耗其他租户的配额（Quota）
- 查看其他租户的审计日志
- 在 `IdentityController` 下操作其他租户的角色与权限分配（**风险最高**）

### 修复方案

**Step 1 — 统一租户来源（推荐，收口做法）**

新增租户解析辅助，强制从令牌派生并校验归属：

```
令牌 tid  →  IPlatformContextAccessor.Current  →  控制器取用
```

- 所有控制器改为从 `HttpContext.User` 的 `tid` 声明取租户，**不再读取请求体/查询串的 `TenantId`**
- 请求体中的 `TenantId` 仅作"切换意图"，服务端必须校验 `用户 ∈ 该租户` 后才采纳，否则返回 `403`

**Step 2 — 清理客户端事实来源**

- `ApiClient.CreateClient()` 移除 `X-Tenant-Id` 头（`ApiClient.cs:32/36`），租户统一由令牌承载
- `ObservabilityMiddleware` / `AuditMiddleware` 的租户标注改为优先读令牌声明，避免监控标签被污染

**Step 3 — 补充守卫**

- 平台资源读写统一增加成员校验，失败返回 `403` 而非 `404`（避免资源枚举）
- `IdentityController` 的角色/权限分配接口追加 `identity:*` 细粒度权限校验

### 验收标准

1. 携带令牌（租户 A）请求 `/api/dashboards?tenantId=B`，返回 **403**，且不返回 B 的任何数据
2. 携带令牌（租户 A）请求体 `{"tenantId": B}` 创建 Dashboard，返回 **403**
3. 移除 `X-Tenant-Id` 头后，Ask / Dashboard / App 全链路功能不受影响
4. 新增单元测试：覆盖"跨租户读取/写入被拒"、"请求体 TenantId 与令牌不一致被拒"
5. `AuthMiddlewareTests` / `TokenServiceTests` / `SuperBIContextTenantFilterTests` 全绿

### 回归约束

- Golden 运行时走独立 `evaluation/golden-runtime` 端点（匿名白名单），**不受本次改动影响**
- 改造须保持"门控隔离"：默认路径逐字节不变，禁止新增全局中间件分支
- 完成后须复跑 **Golden 18/18 PASS** 与全量单测（当前基线 308/308）

---

## 三、P0-44：Ask 响应缓存键缺少权限与版本维度（`DataSourceId` 维度已具备）

### 基本信息

| 项 | 内容 |
|---|---|
| **缺陷编号** | P0-44（修订版） |
| **等级** | 🟠 **P1**（由 P0 降级） |
| **类型** | 缓存正确性 / 数据新鲜度 |
| **处置结论** | 原"补充 `DataSourceId`"**已完成并关闭**；剩余维度继续补齐 |

### 现状事实

**① `DataSourceId` 已纳入缓存键——原审计该项描述已过时。**

```csharp
// src/Api/Controllers/AskController.cs:72
var cached = _cache.Get(tenantId, request.Question!, request.DataSourceId);

// src/Api/Controllers/AskController.cs:83
_cache.Set(tenantId, request.Question!, request.DataSourceId, response);
```

缓存键 = **租户 + 归一化问题 + DataSourceId**（`MemoryAskResponseCache`，TTL 默认 60s，LRU 淘汰）。
→ 原审计"还需增加 DataSourceId"**已落地，本子项关闭**。

**② 仍缺失的缓存键维度：**

- 用户权限上下文（同一问题在不同权限下应返回不同结果）
- Semantic / Metadata 版本
- QueryPlan 版本
- 模型与提示词版本

缺失上述维度将导致：metadata 变更、权限变更、模型升级后仍命中旧缓存，返回过期或越权可见的答案。

**③ 与 P0-04 的联动风险（本次核验新发现，建议一并处理）**

缓存键使用的是**前端传入的** `request.DataSourceId`，而实际执行使用的是**管道推断的** `plan.DataSourceId`：

```
缓存键   ← request.DataSourceId（前端选择）
实际执行 ← plan.DataSourceId（AskController.cs:81 未传递 dataSourceId；
                              BIConversationService.cs:240-244 用 plan.DataSourceId 取数据源）
```

两者不一致时，会把**数据源 B 的执行结果缓存到键 A 之下**，导致后续请求命中错误数据源的答案。
→ 该问题在 P0-04 修复（DataSourceId 全链路贯通）后自动消除，**建议与 P0-04 联合验收**。

### 修复方案

1. **缓存键扩展**：在 `IAskResponseCache` 的键中纳入权限上下文摘要与 metadata 版本标识
2. **失效联动**：metadata 变更、权限变更、模型/提示词升级时主动失效相关缓存条目
3. **与 P0-04 联合修复**：先打通 `DataSourceId` 全链路（`AskController` → `BIConversationService` → `QueryPlan`），再让缓存键与执行上下文同源

### 验收标准

1. metadata 变更后，同一问题的缓存不再命中旧结果
2. 不同权限用户问同一问题，各自缓存独立、互不串扰
3. 前端选择数据源 A，实际执行与缓存写入**均**为数据源 A（与 P0-04 联合验证）
4. `?noCache=1` 旁路能力保持不变

---

## 四、下发执行建议

| 顺序 | 任务 | 依赖 | 说明 |
|---|---|---|---|
| 1 | **P0-04** DataSourceId 全链路贯通 | 无 | 前端已传值、后端丢弃，属已确认真实断链，优先修复 |
| 2 | **P0-03** 平台控制器租户改为令牌派生 | 无 | 本文修订版；勿按原审计的 `X-Tenant-Id` 机制施工 |
| 3 | **P0-44** 缓存键补齐权限/版本维度 | 建议依赖 1 | 与 P0-04 联合验收，避免键与执行错配 |
| 4 | 复跑 Golden 18/18 + 全量单测 | 依赖 1-3 | 硬门禁，不得降级 |

> **施工提醒**：P0-03 若按原审计描述（封堵 `X-Tenant-Id` 头）施工，将**修错地方**——真正的入口是请求体/查询串的 `TenantId`。请按本文第二节的缺陷位置表执行。

---

## 五、附：核验方法

本次结论均通过源码级核验得出，未依赖文档推测：

1. 定位核心类与调用链（`Grep` 类名/字段名 → `Read` 关键文件）
2. 跟踪 `tenantId` / `dataSourceId` 在控制器层的**实际取值来源**（令牌声明 vs 请求体 vs 请求头）
3. 交叉验证中间件与服务的消费方，确认该值是否参与数据定界
4. 以 `文件:行号` 固化证据，逐条对照审计断言

可复验的关键证据点：`AskController.cs:59/72/81/83/116/164-167`、`AuthMiddleware.cs:70`、`BIConversationService.cs:179/240-244`、`ThemeController.cs:49`、`DashboardController.cs:70/99`、`TokenService.cs:54`、`Program.cs:207-218`。
