# M7-11 应用闭环前置契约

> 状态：**已冻结（第五轮定点修订，2026-09-08）**。C0 设计收口完成，五组内部矛盾已消除，可转入 C1–C4 实现与测试验证。
> **第六轮勘误（2026-09-08）**：保留冻结基线，仅作局部校正——**3 项 P0 列为实现验收必过（见 §14）**、**4 处措辞/字段勘误（见 §0/§5.1/§8/§9 内联标注）**。**不重新设计**。
> 依据：`docs/Master_Development_Plan.md` M7-11（Ask 结果发布 App 的生命周期：可编辑、授权、版本化、回滚）。
> 本文件只约束「Ask 结果 → 应用 → 运行/发布」这条链路，不覆盖全站错误码整改（M9-06）。

## 0. 本轮已核实的关键事实（修正措辞）

| 项 | 核实结果 | 对契约的影响 |
|---|---|---|
| `WidgetQueryDsl` 字段 | 已具备 `DataSourceCode/EntityCode/Metrics/Dimensions/Filters/Sorts/Limit`（确定性字段齐全） | C2 不"重新发明"，但须让解析器**真正确定性消费**它们 |
| `QueryPlanWidgetDataResolver` 实际行为 | 会消费 `Metrics/Dimensions`（拼入自然语言问句）并下推 `effectiveFilters`；但**未确定性地消费** `EntityCode`/`DataSourceCode`/`Sorts`/`Limit`/聚合语义；无 Metrics/Dimensions 时退化为"查询数据"交理解服务 | §5.1 原"完全忽略"措辞过重；真实缺口是"未确定性消费实体/源/排序/Limit/聚合"，明细查询同样会丢语义 |
| `IQueryPlanBuilder.BuildAsync` | **已支持** `requestedDataSourceId` + `authorizedDataSourceIds`（显式单源约束在选表前生效） | 单源能力待确认项**已关闭**；C2 直接透传，无需扩展接口 |
| `QueryPlan` 结构 | 含 `DataSourceId`(long)、`Tables`(EntityCode/TableName/**MetadataTableId** 稳定表标识)、`Fields`、`Metrics`、`Dimensions`、`Filters`、`Orders`、`Joins`、`IsAggregate`、`Distinct`、`Limit` | C2 可在 Pipeline 产出后**断言**主表 `MetadataTableId` 与 binding 一致（单源≠实体硬约束）；`Distinct` 首批不支持须 422 拒绝（见 §9） |
| `AppBuilderController` 授权 | 10 个端点**全部仅 `ScopeTo(tenantId)`，无 app:* 操作权限校验** | 授权收口为 **P0**，端点清单见 §5.3 |
| Ask 结果持久化 | `POST /api/ask` 返回 `BIResponse`（`ConversationId`/`Sql`/`Data`/`Answer`/`Explanation`/`ErrorMessage`），**无顶层 `QueryPlan` 属性**；其 `Explanation` 为 `QueryPlanExplanation`，含嵌套 `Explanation.Plan`(`QueryPlan?`)。结果**不落库**。前端无稳定 `turnId` | C1 查询上下文须服务端可验证引用（见 §4，引入 `turnId` + 快照表）；快照从**执行链路**取得，不假定 wire 响应含完整 plan |
| RLS 注入点 | `BIConversationService.ExecuteAsync` 持有 `IRowLevelSecurityService?`（field `BIConversationService.cs:92/106`），在执行链路中对 plan 注入行级条件 | 快照须在 **RLS 注入之前**深拷贝 plan 语义（见 §4） |
| `QueryIntent` 排序 | 仅 `OrderBy`(string?)/`OrderDirection`(string?) **单值字段**，无多字段排序承载 | §9 多字段排序承诺已撤销：首批仅单字段，多字段 422 拒绝 |
| `AppDsl` 操作符 | `AppFilterOperators` = `Eq/Neq/Gt/Lt/In/Like`，**无 `Between`/`Gte`/`Lte`** | `between` 首批直接 422 拒绝（不拆 gte/lte） |
| `AppComponentTypes` 白名单 | `chart/table/kpi/text/filter/form`，**无 `markdown`/`heading`** | 首批静态组件仅 `text`；`markdown`/`heading` 不实现即拒绝 |

## 1. 术语与状态

| 术语 | 定义 |
|---|---|
| 草稿态 `Draft` | `AppPlan` 创建后的初始状态，仅 `app:edit` 者可见 |
| 发布态 `Published` | 存在发布快照 `PublishedDslJson` + `PublishedVersion`，对 `app:view` 者可见 |
| **预览 Preview** | 具备 `app:edit` 者渲染**草稿**（`POST /api/apps/{code}/preview`） |
| **运行 Run** | 具备 `app:view` 者渲染**发布快照**（`POST /api/apps/{code}/render`） |

状态机：`Draft → Published(v1) → Published(v2) …`；回滚固化为新版本（沿用 M7-01/M7-02）。

**红线**：运行态**只渲染发布快照**。应用未发布时运行入口明确拒绝（`409 SB_APP_NOT_PUBLISHED`），**不自动回退到草稿**。

## 2. 数据绑定结构（C1 产出，DSL 升 `version: 1.1`）

`AppDataSourceBinding`（`src/Domain/AppBuilder/AppDsl.cs`）当前有 `Entity`/`Metrics`/`Dimensions`/`Filters`/`Limit`，**缺** `DataSourceId` 与 `Sort`。C1 随 DSL v1.1 补齐：

- **`DataSourceId`（long，已解析的数据源 ID）**：运行时硬约束，统一以它（而非 `DataSourceCode`）作为数据源约束（见 §5.1）。
- **`Sort`**（`List<AppSortBinding{Field,Direction}>`）：映射 `QueryPlan.Orders`（首批仅允许 `Count==1`，见 §9）。
- **`AppMetricBinding` 扩展**：新增 `string? ResultColumnName`（稳定结果列标识，缺省=Field）与 `string? DisplayName`（展示名，缺省=Field）。同一组件多聚合列各持独立 `ResultColumnName`，**不得用组件 `Title` 替代列别名**（§9）。
- **`AppFilterBinding` 扩展**：`in` 操作使用新增的 `List<string>? Values`（类型明确数组）；标量操作仍用 `Value`。逗号分隔标量字符串**不被接受**（含逗号或类型不一直接 422）。
- **明细查询表达**：`Metrics` 字段 `Aggregation` 支持新增 `none`（原始列投影，DSL 升级 `AppAggregateTypes.None`）；明细查询 `Dimensions` 为空、可选 `Filters/Sort/Limit`。
- **实体/表/字段约束独立**：`Entity`（业务实体语义名）用于运行时断言 plan 主表一致，**不把 `QueryIntent.BusinessEntityHints` 当作硬绑定**——它是理解服务的提示，非契约。

**红线（C1）**：binding 必须由**服务端从 Ask 查询快照导出的 `QueryPlan` 反查**得到，禁止仅凭 Ask 的 `Visualizations`（`Type`/`XAxis`/`Title`）反推。明细字段、聚合、筛选须无损表示；不支持的计划（见 §9）**明确拒绝发布**，不得静默丢字段。

## 3. 错误契约（本链路）

后端统一 `ApiError { code, message, traceId, details }`（`src/Api/Errors/ApiError.cs`）；前端 `ApiClientBase.ParseApiError` 解析。

### 3.1 错误 DTO（已定：扩展 `ApiError`）

**决策**：扩展 `ApiError`，不新增独立 DTO，避免前端双解析路径：
```
ApiError {
  code, message, traceId, details,
  errors?    // 字段级校验明细 List<FieldError{Field?, Code, Message}>
  decision?  // 拒绝原因码：PermissionDenied | DataSourceUnauthorized | PolicyBlocked | RequiresClarification
}
```
组件级错误内嵌于 render 响应（见 §10）。**403 响应 body 必须为结构化 `ApiError`，不可为可空/空**（修正原 §10.4 "body 可空"）。

### 3.2 错误码与 HTTP

| 码 | HTTP | 场景 |
|---|---|---|
| `SB_APP_DSL_INVALID` | 400 | DSL 结构/版本校验失败（携 `errors` 明细） |
| `SB_APP_NOT_SUPPORTED` | 422 | QueryPlan 无法无损映射 / 实体不确定 / 绑定不完整 / 多字段排序 / between 筛选 / 跨源或同库 JOIN（`decision=RequiresClarification`） |
| `SB_APP_NOT_PUBLISHED` | 409 | 运行未发布应用 |
| `SB_APP_IDEMPOTENCY_CONFLICT` | 409 | 同幂等键但请求体摘要/期望版本不一致（创建或发布） |
| `SB_APP_RESULT_EXPIRED` | 409 | Ask 查询快照过期 |
| `SB_APP_DRAFT_CHANGED` | 409 | 发布时草稿已被并发修改 |
| `SB_APP_FORBIDDEN` | 403 | 缺 `app:*` 操作权限（`decision=PermissionDenied`） |
| `SB_APP_DATASOURCE_UNAUTHORIZED` | 403 | 绑定数据源未授权/已停用/已撤权（`decision=DataSourceUnauthorized`） |
| `SB_APP_QUERY_BLOCKED` | 403 | RLS / Security Gate 策略拒绝（`decision=PolicyBlocked`） |
| `SB_APP_QUERY_ERROR` | 500 | 取数执行失败（**脱敏**，不回吐 `ex.Message`） |

### 3.3 拒绝原因映射（修正：非全部 403）

- 权限 / 安全拒绝（PermissionDenied / DataSourceUnauthorized / PolicyBlocked）→ **403**，带 `decision`。
- 绑定不完整 / 实体不确定 / 计划无法解析 / 多字段排序 / between / JOIN → **422** `SB_APP_NOT_SUPPORTED`，带 `decision=RequiresClarification`。
- C2 **不得靠解析中文 `Error` 消息**映射错误码，必须返回结构化 `decision`。

### 3.4 阶段契约

| 阶段 | 成功返回 | 失败行为 |
|---|---|---|
| 创建（from-ask） | 应用标识 + `Status=Draft` + 已导出 binding 的 DslJson | 结构化错误，**不提示任何"已发布"** |
| 发布 | `Status=Published` + `Version` | 保留草稿与已建应用，返回错误码 + TraceId |
| 预览 | 草稿渲染结果 | 未授权 403；单组件缺 binding → 该组件失败，整体 `succeeded=false` |
| 运行 | 发布快照渲染结果 | 未发布 409（不回退草稿）；安全拒绝 403 |

**红线**：创建成功 ≠ 发布成功。创建成功但发布失败时保留草稿，前端展示「已保存为草稿，发布失败：<message>（TraceId: xxx）」并提供重试发布。

### 3.5 多组件渲染结果（已定）

- 严禁"部分组件成功却整体报成功"。
- 响应逐组件携带 `Resolved/Decision/Error`；**任一数据组件 `Blocked/Error/绑定缺失/不支持` → 整体 `succeeded=false`**，暴露组件级错误（不限于 `Blocked`/`Error` 两个名称）。
- 部分失败语义（见 §10）：数据组件失败但静态文本组件正常 → HTTP 200 + `succeeded=false`；整页无任何可渲染内容（无静态文本且所有数据组件被安全/权限拒绝）→ HTTP 403。

## 4. 查询上下文来源（C0.1 已定，含快照截取时机与归属）

Ask 结果不持久化，C1 引用须**服务端可验证**。方案：引入 Ask 查询快照。

- **引用标识 `turnId`**：`POST /api/ask` 在返回 `BIResponse` 时附 `turnId`（服务端生成 GUID，与现有 `conversationId` 并存；前端当前无稳定 turnId 契约，本轮补齐）。
- **快照截取时机（关键，修正）**：在 `BIConversationService.ExecuteAsync` 执行链路中，**于 `QueryPlanPipeline.RunAsync` 产出最终 plan 之后、`RowLevelSecurity.ApplyAsync` 注入行级条件之前**，对 plan 语义做**深拷贝**并准备持久化。这保证快照**不含发布者 RLS 运算结果**。
- **仅成功才落库**：**实际查询成功**（返回数据行或确认执行）后才写入 `AskQuerySnapshots` 并返回 `turnId`；**拒绝、执行失败、Decision Gate 拒绝的请求不产生可发布快照**。需覆盖：首问成功、**澄清后成功**、**refine 后成功**、**缓存命中成功**——任一成功路径都应产出 `turnId`，否则用户调整后的结果将无可引用标识。
- **存储**：新增 `AskQuerySnapshots(TenantId, TurnId PK, UserId, DataSourceId, EntityCode, QueryPlanJson, CreatedAt, ExpiresAt)`，TTL **24h**（可配）。`QueryPlanJson` 仅存查询语义（字段/聚合/筛选/排序/Limit/主表 TableId）。
- **归属校验（修正）**：首批统一为**仅快照创建者本人**可载入；持有同租户 `app:create` **不自动获得读取他人查询上下文的能力**。跨用户/跨租户 → 403 `SB_APP_FORBIDDEN`。
- **防篡改（关键）**：`from-ask` 只接受 `(turnId, name, code?, themeKey?)`；binding 由服务端从**存储的 `QueryPlanJson`** 反查，**不信任前端传入的数据源/字段**。客户端篡改 → 服务端以快照为准。
- **敏感数据固化禁令**：快照仅存查询语义 + 创建者授权时的 `DataSourceId`，不存结果行、不存 RLS 运算结果、不存身份参数。运行时重新应用当前访问者策略。
- **AI 解读（Explanation 文本）首批不固化**：其他编辑者未必有发布者数据权限，故**不自动复制 Ask 结果摘要/AI 解读到共享应用内容**。文本组件如需展示，由编辑者手动以静态 `text` 编写。

## 5. 安全运行规则（C2 红线）

**发布者拥有数据权限，不代表应用访问者继承这些权限。**

### 5.1 复用而非"映射即完成"（确定性分支）

`IWidgetDataResolver`（`QueryPlanWidgetDataResolver`）存在，但当前走自然语言理解、未确定性消费实体/源/排序/Limit/聚合。C2 必须新增**确定性结构化查询分支**：

1. **判定条件（修正）**：以"binding 含**有效结构化查询模式**"判定是否走确定性分支，而非仅看 `Metrics/Dimensions` 是否存在。**明细查询（无聚合、纯字段投影）同样必须绕过 `IQueryUnderstandingService`**。判定依据 = `binding.DataSourceId` 已解析且 `binding.Entity`/字段可映射到元数据。
2. **直接构造 `QueryIntent`**：填 `Metrics`/`Dimensions`/`Filters`/`Limit`（来自 binding，含明细投影 `Aggregation=none`），`BusinessEntityHints` 仅作提示填入，不作硬约束。**排序：若 `binding.Sort.Count==1` → 填 `OrderBy`/`OrderDirection`；若 `Count>1` → 直接 422 `SB_APP_NOT_SUPPORTED`（结构化多字段排序透传为后续扩展，不在首批）**。
3. **单源硬约束（已确认可用）**：调用 `IQueryPlanPipeline.RunAsync(question, intent, requestedDataSourceId: binding.DataSourceId, authorizedDataSourceIds: 访问者授权源集合)`。单源接口已支持，约束在选表前生效。
4. **实体硬约束断言（单源≠实体，修正）**：Pipeline 产出 `plan` 后，C2 必须断言：
   - `plan.DataSourceId == binding.DataSourceId`；
   - `plan` 主表解析出的稳定 `MetadataTableId`（实际字段名，非 `TableId`）== `binding.Entity` 解析出的 `MetadataTableId`（**不比较语义名/物理表名字符串**，须用元数据解析的稳定标识）；
   - **完整查询语义一致性（修正：不限于"字段名+类型"）**：`plan` 与 binding 必须在以下维度逐项一致，任一不符 → 422 `SB_APP_NOT_SUPPORTED`：
     - 查询模式（`IsAggregate` / 明细投影 / `Distinct`——首批 `Distinct=true` 直接拒绝）；
     - 聚合方式（每个 `Metric.Aggregation` 与 binding 对应项一致，含 `none`）；
     - 字段集合（`Fields`/`Dimensions` 语义字段名 + 类型）；
     - 筛选（每个 `Filter` 的字段 + **操作符 + 值** 与 binding 一致，值须按元数据列类型参数化校验）；
     - 排序（字段 + **方向 ASC/DESC**，与 binding 单字段排序一致）；
     - `Limit`（与 binding 一致）；
     - 结果列别名（`ResultColumnName` 映射一致）。
   即"同库选错表、字段错位、聚合/筛选/排序/方向/Limit/别名任一偏差"均拒绝执行。
5. **复用安全执行步骤**：授权集合校验 → Decision Gate → `RowLevelSecurity.ApplyAsync` → `SecurityGate.ValidateAsync` → 执行前 `TenantId && Enabled` 复核（沿用 resolver 现有步骤）。

若扩展共享 resolver，须验证既有 Dashboard 路径零回归（Golden 18/18 + Dashboard 渲染用例）。

### 5.2 安全验收（P0，必须可证明）

resolver 内授权 / 执行身份 / RLS / Security Gate **均为可选依赖**，存在分支 ≠ App 路径始终启用。C2 验收须覆盖：

- App 路径**缺任一安全依赖时明确拒绝**执行（不静默放行）。
- 绑定源未授权时，即使访问者有其他授权源**也必须拒绝**。
- 安全拒绝后，**SQL 构建与执行调用次数 = 0**（测试探针验证）。
- 返回**结构化 `decision` 原因码**，统一异常与结果错误消息处理。
- 撤权后重新运行必须 `Blocked`，不得继续取数（回归用例）。
- resolver catch 段回吐 `ex.Message` 须脱敏为 `SB_APP_QUERY_ERROR`（契约红线）。

### 5.3 权限矩阵（C0.5，P0 —— 全部端点）

`AppBuilderController` 当前 10 个端点**全部缺 `app:*` 操作权限校验**（仅 `ScopeTo`）。本轮收口**全部端点**；授权模型采用**同租户 + 操作权限**（见下方说明），逐应用 ACL 暂缓。

| 端点 | 方法 | 权限 | 当前 | 本轮 |
|---|---|---|---|---|
| `POST /api/apps` | create | `app:create` | ❌ | 强制 |
| `POST /api/apps/generate` | generate | `app:create` | ❌ | 强制 |
| `POST /api/apps/from-ask` | create-from-result | `app:create` | 新增 | 强制 |
| `GET /api/apps` | list | `app:view` | ❌ | 强制 |
| `GET /api/apps/{code}` | get | `app:view` | ❌ | 强制 |
| `PUT /api/apps/{code}` | update | `app:edit` | ❌ | 强制 |
| `DELETE /api/apps/{code}` | delete | `app:delete` | ❌ | 强制 |
| `POST /api/apps/{code}/publish` | publish | `app:publish` | ❌ | 强制 |
| `POST /api/apps/{code}/rollback/{v}` | rollback | `app:publish` | ❌ | 强制 |
| `GET /api/apps/{code}/versions` | versions | `app:view` | ❌ | 强制 |
| `GET /api/apps/editor/blueprint` | blueprint | `app:create` | ❌ | 强制 |
| `POST /api/apps/{code}/preview` | preview | `app:edit` | 新增 | 强制 |
| `POST /api/apps/{code}/render` | render | `app:view` | 新增 | 强制 |
| `POST /api/apps/{code}/copy` | copy | 来源读取 + `app:create` | 新增 | 强制（另存为新应用，见 §7；固定路由，无 from-ask 备选） |

**授权模型澄清（修正）**：操作权限（`app:*`）与"指定用户只能访问某应用"的**逐应用 ACL 是两件事**。本轮**只补 `app:*` 操作权限**（同租户内按角色授予），逐应用 ACL 属另一项产品范围决策，**暂缓、不在本轮**。校验方式参照 `AskController`：`IIdentityService.HasPermissionAsync(tenantId, userId, perm)`，tenantId/userId 从 JWT 声明解析（与现有 `ScopeTo` 并存，先 `ScopeTo` 再 `HasPermission`）。

## 6. 草稿可见性与发布后编辑（C0.5，P0，含详情防泄露）

- 普通查看者（`app:view`）只能读取**发布快照**（`render`）。
- 编辑者（`app:edit`）可读草稿（`preview`）；预览与运行**入口与权限分离**。
- 草稿修订与发布版本**独立记录**；「已发布 v1，但草稿已修改」：运行取 v1，预览取草稿（`DslJson`）。
- **详情响应防泄露（修正 P0）**：`GET /api/apps/{code}` 对 `app:view` 仅返回**发布数据**（`PublishedDslJson` + `PublishedVersion` + 只读概览）；`DslJson`（草稿）/`DraftRevision` **仅在调用者持有 `app:edit` 时返回**，由服务端按权限裁剪响应字段，**不得依赖前端隐藏**。前端对 `app:view` 不应能取到草稿 `DslJson`。

## 7. 幂等与并发（C0.4 已定，含本轮新增三规则）

| 规则 | 说明 |
|---|---|
| 创建幂等键 | `tenantId + turnId`；同键返回**同一应用**（复用 `code`），不新建。新增 `AppFromAsk(TenantId, TurnId, AppCode, RequestDigest)` 唯一约束 |
| 创建同键不同内容 | 同 `turnId` 但 `name/code/themeKey` 与已存 `RequestDigest` 不一致 → **409 `SB_APP_IDEMPOTENCY_CONFLICT`**（保存请求摘要，拒绝静默覆盖） |
| 另存为新应用 | `turnId` 是服务端查询结果标识，客户端**不得为复制应用而伪造新 turnId**。提供**固定路由** `POST /api/apps/{code}/copy`，使用独立幂等键生成新应用标识。**复制需来源读取权限**：复制发布快照要求调用者对来源应用具 `app:view`、复制草稿要求具 `app:edit`，同时须持 `app:create`；仅 `app:create` 不足以复制他人隐藏草稿（修正：删除"或 from-ask 传 sourceAppCode"备选，仅保留固定路由） |
| 发布幂等键 | 客户端 `Idempotency-Key` 头（UUID）。服务端 `AppPublishIdempotency(TenantId, AppCode, IdempotencyKey, ExpectedDraftRevision, PublishedVersion)`。同键 + 同期望草稿版本 → 返回既有 `PublishedVersion`，**不增版本**；不同期望版本 → 409 `SB_APP_IDEMPOTENCY_CONFLICT` |
| 发布响应丢失重试（修正） | 重试须先**重新校验 `app:publish` 权限**（JWT 有效不足够），再**先查既有幂等成功记录**：若已成功则返回既有 `PublishedVersion`（不误判失败）；否则比对当前 `ExpectedDraftRevision`，被后续编辑改动 → 409 `SB_APP_DRAFT_CHANGED` |
| 期望草稿版本 | `AppPlan` 新增 `DraftRevision`（每次 `PUT` 自增，作为**乐观并发令牌**）。发布携 `ExpectedDraftRevision`；不匹配 → 409 `SB_APP_DRAFT_CHANGED` |
| 事务与并发（修正） | 应用、版本记录、幂等记录**三者在同一数据库事务内原子提交**；`DraftRevision` 为并发令牌，编辑/发布并发冲突由令牌校验拒绝，**单事务本身不保证并发检查有效，须显式并发控制** |
| 发布版本更新保护（修正 P0） | `DraftRevision` 仅覆盖"编辑 vs 发布"冲突；**两个并发发布请求可能读取相同草稿修订号**。须额外保护发布版本更新：版本号递增在事务内以行级锁/乐观并发守卫，确保不同 `Idempotency-Key` 的并发发布、以及与回滚并发时**不产生重复版本号、不互相覆盖**；发布成功后写 `AppPublishIdempotency`，同键重入返回既有版本而不重发 |
| 同键重试 | 仍需重新鉴权（含当前 `app:*` 权限，非仅 JWT 有效） |
| 更新已有应用 | 改草稿（`DraftRevision+1`）+ 发布新版本，禁止静默覆盖 |

## 8. DSL 版本兼容（C0.6，最小兼容规则**随 C1/C2 同批完成**，非延后）

- DSL 升 `version: 1.1`：新增 `DataSourceId`/`Sort`(单字段)/`Metrics.ResultColumnName`/`Metrics.DisplayName`/`Filters.Values`/聚合 `none`。
- **区分静态文本组件与数据组件**（修正原"缺 binding 一律不可运行"）：
  - 纯静态组件（`Type == text` 且无 `Binding`）→ 合法，正常渲染，**不受绑定缺失影响**。首批静态组件仅 `text`（`markdown`/`heading` 不在白名单，使用即 422）。
  - 数据组件（`Type ∈ {chart, table, kpi}`，`Binding` 非空）→ 必须绑定合法数据源。
  - `filter`/`form` 类型已入白名单但交互行为本轮不实现，使用即 422 `SB_APP_NOT_SUPPORTED`。
  - 旧应用（DSL v1.0 数据组件无 binding）→ **端点级**（from-ask/DSL 校验）不支持时返回非 200（422）；**进入组件渲染后**，该数据组件以组件错误（`error`）写入、按 §10 统一整体规则返回（不整应用拒绝）；提示用户重新编辑；若全部数据组件均缺 binding 则整体无数据、仍返回页面骨架（含静态文本）。
- **最小兼容规则（本批交付）**：v1.0 应用导入 v1.1 运行时，缺字段按默认值推导（`DataSourceId` 缺失且无法解析 → 该数据组件 422；`Sort` 缺失 → 无排序；`ResultColumnName` 缺失 → 回退 `Field`）；禁止猜测数据源。

## 9. 可支持查询矩阵（C0.2 已定，本轮收紧）

**首批支持（允许案例）**
- 单数据源（`binding.DataSourceId` 唯一）。
- 明细查询：`Metrics` 字段 `Aggregation=none`（原始列投影），`Dimensions` 空，可选 `Filters`/`Sort`/`Limit`。
- 单表聚合：`Metrics`(sum/avg/count/min/max) + `Dimensions`(group by)。
- 排序：**仅单字段**（`binding.Sort.Count==1` → `QueryPlan.Orders` 单条）；多字段明确 422 拒绝。
- 筛选：操作符 `eq/neq/gt/lt/in/like`；`in` 使用**类型明确数组** `Values: List<string>`（如 `["上海,浦东","北京"]` 已是**无歧义数组**，**禁止把单个字符串按逗号拆值**；单值 `in` 须改为数组，标量字符串直接拒绝；数组元素按元数据列类型校验并参数化传参）。`between` **直接 422 拒绝**（模型无 `gte`/`lte`，不拆值）。`Distinct`（去重）**首批 422 拒绝**（避免去重语义丢失）。
- 聚合别名：每个 `Metric` 持稳定 `ResultColumnName`（缺省=Field）+ `DisplayName`（缺省=Field）；多聚合列各持独立标识，渲染以 `ResultColumnName` 取列、`DisplayName` 展示。

**首批拒绝（不得静默丢字段，统一 422 `SB_APP_NOT_SUPPORTED`）**
- **任意 JOIN（单库或多库）**：首批全部拒绝，后续再扩展（修正原"仅跨源拒绝"）。
- 多字段排序。
- `between` 筛选、无法映射为上述操作符的复杂比较。
- `Distinct=true`（去重）首批不支持。
- 无法确定 Entity（`Entity` 无法解析为租户元数据中的稳定 `MetadataTableId`）。
- 筛选/字段无法映射到具体列（语义名在元数据中无对应字段或类型不符）。
- 嵌套子查询 / 窗口函数 / 需 NLU 的自由文本模糊查询。
- 跨表关联聚合（多实体 join 聚合）。

## 10. 接口与响应模型（C0.3 已定）

### 10.1 创建（from-ask）
```
POST /api/apps/from-ask
{ "turnId": "guid", "name": "str", "code": "str?", "themeKey": "str?" }
→ 200 AppDetail(Status=Draft, DslJson 含导出 binding；仅当调用者含 app:edit 才含 DslJson/草稿字段)
→ 403（权限/归属）/ 409（快照过期/幂等冲突）/ 422（计划不支持）/ 400（参数）
```

### 10.2 预览（草稿）
```
POST /api/apps/{code}/preview        // 需 app:edit
→ 200 AppRenderModel
→ 403 未授权；草稿数据组件缺 binding → 该组件失败，整体 succeeded=false
```

### 10.3 运行（发布快照）
```
POST /api/apps/{code}/render         // 需 app:view
→ 200 AppRenderModel
→ 409 SB_APP_NOT_PUBLISHED（不回退草稿）
→ 403 安全/权限拒绝（结构化 ApiError body，无数据返回）
```

### 10.4 AppRenderModel（新增响应模型，补足呈现字段）
```
{
  code, status, publishedVersion?, dslVersion, layout, themeRef?,
  pages: [ {
    id, name,
    components: [ {
      id, type, title?,
      chartType?, axisFields?, series?,          // 图表呈现配置（白名单过滤后）
      text?,                                    // 静态文本组件内容
      columns?: [ { name, displayName, type } ],// 结果列：稳定 name + 展示 displayName + 类型
      rows?,
      resolved, decision?, error?
    } ]
  } ],
  succeeded: bool,                   // 任一数据组件失败/绑定缺失/不支持 → false
  componentErrors: [ { componentId, decision, error } ]
}
```
**多组件失败语义（统一，修正）**：
- 数据组件失败、静态文本组件正常 → HTTP **200** + `succeeded=false` + 逐组件 `componentErrors`（不误报完整成功），静态文本照常渲染。
- 整页无任何可渲染内容（无静态文本且所有数据组件被安全/权限拒绝）→ HTTP **403**，body 为结构化 `ApiError`（`decision` 非空），**不为可空**。
- 单组件 DSL/绑定错误 → 该组件 `error`，不阻断其余组件。
- **任一数据组件 `Blocked/Error/绑定缺失/不支持` → `succeeded=false`**（不限于 `Blocked`/`Error` 两种名称）。

## 11. C0–C4 施工顺序（C0 已冻结）

| 序 | 内容 | 关键交付 |
|---|---|---|
| **C0 契约收口** | §4/§5.1/§5.3/§7/§9/§10 具体选择定稿，本文冻结（第五轮） | 冻结评审通过 |
| C1 服务端导出 binding | 新增 `AskQuerySnapshots` + `turnId`；由快照 `QueryPlan` 反查完整 binding（含 ResultColumnName/DisplayName/Values/单字段 Sort）；DSL 升 1.1；明细/聚合/筛选/Limit/指定源不丢失；**最小兼容规则同批** | 支持范围外明确 422 拒绝 |
| C2 确定性安全运行 | 新增确定性分支 + 单源硬约束 + 实体 TableId 断言 + 安全验收（撤权拒绝 / 未发布拒绝 / SQL 调用=0 / 脱敏） | 真实取数成功且安全拒绝可证明 |
| C3 前端渲染器 | App 渲染组件 + `/apps/{code}/run` 与 `/preview`；`AppDetail` 保留治理视图 | 表格/图表真实行；部分失败不误报 |
| C4 发布接线 | Ask「from-ask → 发布」两段；扩展 `IAppApiClient`；UI 按真实阶段展示；`copy` 另存为新应用 | 发布失败不显成功；草稿可重试 |
| P0-D1/D2 验收 | 人工 + 自动化，关键用例实际执行、零跳过 | 截图 + E2E 零跳过报告 |

## 12. 环境事实（已核实）

- 建首位租户管理员 UI **已存在**（`Tenants.razor` 新建租户弹窗含用户名/显示名/邮箱/初始口令）。
- 元数据扫描端点 **已存在**（`MetadataController`：`POST /api/data-sources/{id}/metadata/scan`、`GET .../scan/{jobId}`）。
- E2E 目录 `tests/SuperBuilder_AI.E2E.Tests`：未配 `SB_E2E_BASE_URL` 跳过；已配地址但缺 Chromium 时 `Chromium.LaunchAsync()` 无兜底，夹具初始化失败。

## 13. 实施验收条件（与 C0 收尾并行推进，非文档冻结阻塞项）

- WMS MySQL `192.168.16.120:3306` 可连性与账号权限是否满足扫描（P0-B 实测）。
- 平台管理员登录、建租户、建租户管理员、切换租户管理员登录是否真实事务成功（P0-A 实测）。
- 自动化验收需「关键用例零跳过」（Playwright + `SB_E2E_BASE_URL` 就绪后执行）。

> 已关闭项：① 单源硬约束接口（`IQueryPlanBuilder.BuildAsync` 已支持 `requestedDataSourceId`/`authorizedDataSourceIds`）；② resolver "完全忽略确定性字段" 措辞已更正为"未确定性消费实体/源/排序/Limit/聚合"；③ 多字段排序承诺已撤销（首批单字段 + 422 拒绝）；④ `between`/`gte`/`lte` 模型缺口已承认（`between` 直接拒绝）；⑤ 组件静态类型收敛为仅 `text`（无 `markdown`/`heading`）；⑥ BIResponse 无顶层 `QueryPlan`，快照从执行链路 RLS 注入前截取。

## 14. 实现验收必过项（第六轮勘误，P0 —— 随 C1–C4 实现修正并测试，非设计变更）

以下三项为**验收门槛**，对应实现时发现偏差须修正后测试通过，不得绕过：

1. **复制来源权限（P0）**：`POST /api/apps/{code}/copy` 必须校验**来源读取权限**——复制发布快照要求调用者具来源 `app:view`、复制草稿要求具来源 `app:edit`，且须同时持 `app:create`；仅 `app:create` 不得复制他人隐藏草稿。路由固定，无 from-ask 备选。
2. **查询一致性断言完整（P0）**：除表（`MetadataTableId`）与字段，C2 断言必须覆盖聚合方式、筛选操作符+值、排序方向、Limit、结果列别名（`ResultColumnName`）、查询模式（`IsAggregate`/`Distinct`）；任一偏差 → 422 `SB_APP_NOT_SUPPORTED`。不得只比较"字段名+类型"。
3. **发布版本更新并发保护（P0）**：不同 `Idempotency-Key` 的并发发布、发布与回滚并发，均不得产生重复版本号或互相覆盖；版本递增在事务内以行级锁/乐观并发守卫，发布成功后写 `AppPublishIdempotency`，同键重入返回既有版本。

**建议首个固定用例（先证 C1+C2）**：单表明细、单字段倒序（`Sort.Count==1, OrderDirection=DESC`）、`Limit=10`、一个 `eq` 筛选。验证：① 原查询语义（表/字段/排序方向/Limit/筛选值）在 binding 与运行取数中无损保留；② 不同访问者按各自权限取数（撤权后拒绝、`SQL 调用=0`）；③ 一致性断言拒绝偏差计划。通过后再接 C3/C4。

**四处措辞/字段勘误（已内联修正）**：① 实际表标识字段为 `MetadataTableId`（非 `TableId`）；② `Distinct` 已列入首批 422 拒绝；③ `in` 禁止按逗号拆单串、`["上海,浦东","北京"]` 为合法数组并依列类型参数化；④ §8 与 §10 失败口径统一（端点级非 200，组件级按 §10 整体规则）。
