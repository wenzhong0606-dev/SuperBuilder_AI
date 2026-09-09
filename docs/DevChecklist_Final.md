> **治理声明**：本文档是 `Master_Development_Plan.md` 的**输入 / 审计基线**，**不是**独立执行计划。所有里程碑状态、优先级与验收以 `Master_Development_Plan.md`（唯一事实来源）及其 `milestones/` 拆分文档为准；本文与 MDP 冲突时以 MDP 为准。映射见 MDP §17。
>
> **文档角色**：安全/架构缺陷整改清单（SB-P* 映射到 M0/M2/M5/M6/M7/M9，见 MDP §17.2）

# SuperBuilder AI 缺陷整改开发清单（定稿）

> **版本**：v1.2　　**基线**：`master`　　**修订日期**：2026-09-01
> **性质**：`master` 唯一执行主清单。后续开发照此推进，每完成一项更新状态，不再另开路线。
> **来源**：《全量源码审计最终结论》经源码级核验后修订。修订点见文末「附录 C 修订记录」。
> **v1.2 变更**：纠正既有 `platform-admin` 全局全权限冲突；TenantManagement 全端点纳入治理鉴权；细化 DataSource/RLS/Security Gate 与缓存安全边界；明确 evaluation 生产隔离；测试门禁改为动态零失败。

---

## 〇、横切约束（适用于本清单全部条目，逐条强制）

**安全正确性优先于行为冻结**：若安全漏洞修复必须改变默认行为，可作为受控例外，但必须补充针对性回归测试，并保持 Golden 18/18 与全量单测通过。

### 约束 1：Golden 18/18 + 全量测试动态硬门禁

- 每完成一项（含 P2），必须复跑 Golden 回归 **18/18 PASS** + 全量单元测试**零失败**。当前记录基线为 **308 项**，后续以该项分支点的 `master` 测试总数为动态下限；新增整改测试后不得通过删除、跳过或降级既有测试降低门禁
- 任一项未通过即视为未完成，禁止合入 `master`
- 安全项测试不得只断言 HTTP 状态码；必须同时断言**无敏感数据返回、无越权副作用、拒绝审计已入账**。涉及缓存时必须覆盖高权限→低权限、策略变更、角色撤销后的缓存隔离
- Golden 运行时及现有 `/evaluation/*` 诊断/回归端点位于 `AuthMiddleware` 匿名白名单内（`AuthMiddleware.cs:103`），属 Development/Test Golden 基础设施。**P0-09 不得把它粗暴改成普通 Admin 端点**；Production 默认不映射，确需启用时使用独立内部监听/诊断进程或受控反向代理网络 ACL

### 约束 2：门控隔离（Gated Isolation）

- **优先保持默认路径稳定**：新增能力优先采用"非默认才启用"的门控隔离；若安全修复必须改变默认行为，**安全正确性优先**，须以新增回归测试证明变更边界
- **谨慎新增全局中间件分支**：优先使用显式调用、端点级特性或统一授权服务；仅当跨端点安全语义必须统一时才允许调整全局中间件，并要求专门回归测试
- **Golden 基线不得为"让测试通过"而修改**：业务契约未变时禁止改 Golden 期望。若某条 P0 确认改变正式业务契约，必须同时满足：① 指定 reviewer 书面确认；② 在 `docs/` 记录变更原因、影响范围与受影响用例清单；③ 单次基线调整只能对应一条 P0；④ 变更前后基线差异可审计
- **先加测试再改实现**

### 约束 3：Golden 爆炸半径 / 安全落点规则

- `GoldenDatasetRunner` 直接依赖 `IQueryPlanBuilder`、`IQueryPlanContextBuilder`、`IQueryPlanValidationPipeline`（`GoldenDatasetRunner.cs:20-22`），并在 Validation / Confidence 处结束，**不经过 `IBIConversationService` 之后的 SQL 生成与执行阶段**
- 因此 **Builder / ValidationPipeline 属 Golden 爆炸半径**；`BIConversationService` 的"QueryPlan 管线之后、SqlQueryBuilder 之前"属**天然 Golden 隔离缝**
- **SB-P0-01**：允许为 `IQueryPlanBuilder` 增加可选数据源约束（默认保持旧路径），只由 `BIConversationService` 传入，Golden Runner 不传
- **SB-P0-06 / SB-P0-07**：RLS 注入与 Security Gate 固定落在 `BIConversationService` 的管线后、SQL 前；默认不得注入 `IQueryPlanValidationPipeline`
- 任何改动若必须进入 Builder / ValidationPipeline，PR 必须标注"Golden 爆炸半径变更"，并按约束 2 的基线变更治理流程执行

### 约束 4：平台治理角色边界（适用 SB-P0-11）

平台超级管理员**仅是治理角色，不是跨租户数据访问者**：

- ✅ 可以：租户创建/停用、平台共享定义治理、诊断面访问、平台级审计与配额查看
- ❌ 不可以：**读取任何租户的业务数据**（不进入 BI 数据链路，不受理 `RequestedTenantId` 切换）、绕过审计
- **管理面与数据面使用不同授权语义**：治理操作中的目标租户记为 `ManagementTargetTenantId`，由 `platform:*` 权限授权；它不是 `EffectiveTenantId` 切换，也不得写入 BI 数据上下文。数据面仍严格执行 `EffectiveTenantId := AuthenticatedTenantId`
- 一旦平台管理员可读取租户业务数据，**SB-P0-06 的 RLS 即形同虚设**——这是硬性红线
- **既有实现冲突必须先消除**：当前全局 `platform-admin`（`TenantId=0`）包含 Dashboard/Metadata 等业务权限，且可被普通租户用户分配；SB-P0-11 必须重定义/废弃该角色及既有测试，不能在其旁边另加一套角色后保留旧越权通道
- 平台治理角色只能授予“平台租户”用户；普通租户的 Identity 管理员无权授予、撤销或枚举该角色。平台治理用户不得获得 `dashboard:*`、`metadata:view/scan`、DataSource 或其他进入 BI 数据链路的权限

---

## 一、P0 清单（安全与运行时正确性）

> **第一阶段目标**：不会因为身份、租户、数据源和数据权限问题查错库、越权或泄露数据。
>
> **2026-09-02 实施进度快照**：P0 阶段全部条目已闭合。最新门禁：全量测试 **411/411**、Golden **18/18 PASS**。

| ID | 优先级 | 模块 | 开发任务 | 核心改造点 | 验收标准 | 状态 |
|---|---|---|---|---|---|---|
| **SB-P0-01** | P0 | Query Runtime | 修复 DataSourceId 全链路与缓存同源 | 区分 `RequestedDataSourceId` / `ResolvedDataSourceId`。**施工落点写死**：给 `IQueryPlanBuilder` 增加可选数据源约束（默认值保持旧路径），仅由 `BIConversationService` 在显式指定时传入；约束必须在 Metadata Search / Table Selection 前生效，禁止在 Pipeline 结束后覆盖 `plan.DataSourceId`。Golden Runner 不传该参数。**✅ 现成先例**：`IQueryPlanBuilder` 已有 `BuildAsync(intent, resolution?)`（"Resolution 为 null 时保持原有行为"），照抄该范式即可 | ① 指定 A 时 Metadata 搜索/选表仅在 A 内，QueryPlan.DataSourceId=A、执行连接=A、缓存键=A；② 不出现"B 表 + plan.DataSourceId=A"的 `TABLE_DATASOURCE_MISMATCH`；③ 未指定时默认/Golden 行为不变，允许推断 B；④ **指定 A 但 A 内无匹配表时返回明确错误「所选数据源下未找到相关表」，不得静默回退 B**；⑤ 重复提问命中缓存仍与 `ResolvedDataSourceId` 同源；⑥ Golden 18/18 | ✅ 已完成｜369/369｜Golden 18/18｜DataSource 约束 + 执行/缓存同源 |
| **SB-P0-02** | P0 | Tenant | Tenant Authorization / Effective Tenant（业务数据面单租户恒等） | 默认租户来自 Token `tid`（`AuthenticatedTenantId`）。**本规则限定于数据面**（BI 查询、业务元数据读取、DataSource/SQL）：当前用户恒属单租户，故 `EffectiveTenantId := AuthenticatedTenantId`；任何数据面 `RequestedTenantId != AuthenticatedTenantId` → **403**。平台治理管理面不进行 EffectiveTenant 切换，按 P0-02A 的独立 `ManagementTargetTenantId + platform:*` 授权处理。**不建成员表、不实现数据面切换、零迁移** | ① A 租户 Token 请求读取 B 的业务数据 → **403 且不返回 B 数据**；② 治理用户也不能通过 RequestedTenantId 进入 B 的 BI/Metadata/DataSource 链路；③ 无数据面“合法切换”成功路径（登记为 SB-P1-16）；④ 管理面跨租户成功路径仅按 P0-02A 验收 | ✅ 已完成｜数据面单租户恒等｜跨租户请求 403 |
| **SB-P0-02A** | P0 | Tenant/API | 平台 Controller Scope 收口 + TenantManagement 全面鉴权 | 七类平台 Controller（Dashboard/Identity/AppBuilder/Agent/Theme/Quota/Audit）统一调用 `TenantScopeResolver`；禁止直接 `ScopeTo(request.TenantId)` 后写业务 `PlatformContext`。`TenantManagementController` 的全部端点按 `platform:tenant:view/manage`、`platform:tenant-settings:manage` 分级。**管理面例外**：治理角色可跨租户操作，但请求中的租户仅作为 `ManagementTargetTenantId`，不得改写 `EffectiveTenantId`、不得开启目标租户的 EF 业务数据 Scope、不得复用 BI 数据面切换通道 | ① 全仓不存在未经授权检查直接以请求 `TenantId` 设置有效租户上下文的入口；② 治理角色可管理目标租户 B，且其 `AuthenticatedTenantId/EffectiveTenantId` 仍为平台租户；③ 非治理角色访问任一 TenantManagement 端点均 403 且无泄露/副作用；④ 请求体、路由、查询串入口均有测试；⑤ 管理调用结束后上下文不污染后续请求；⑥ 具体资源执行统一 403/404 防枚举策略 | ✅ 已完成｜364/364｜Controller Scope 收口 + TenantManagement 鉴权 |
| **SB-P0-02B** | P0 | Observability/Audit | 修正租户审计与可观测上下文 | Observability/Audit 不再以 query/header 作为“实际租户”事实源。数据面记录 `AuthenticatedTenantId`、`RequestedTenantId`、`EffectiveTenantId`、`TenantSwitchAuthorized`；管理面另记 `ManagementTargetTenantId`、`ManagementAction`、`ManagementActionAuthorized`，禁止把管理目标伪装成 EffectiveTenant。当前数据面无合法切换，故 `TenantSwitchAuthorized=false` 且 Effective=Authenticated | ① 伪造 tenantId/header 不会污染实际执行租户；② 数据面越权意图及结果入账；③ 治理角色管理 B 时同时记录平台租户身份与目标 B，但不记录为 TenantSwitch；④ 管理拒绝和成功均有结构化审计 | ✅ 已完成｜376/376｜双上下文审计/可观测事实源收口 |
| **SB-P0-02C** | P0 | Frontend | 移除 `ApiClient` 的 `X-Tenant-Id` 自动头 | `ApiClient.CreateClient()` 不再根据 `AppState.TenantId` 自动发送 `X-Tenant-Id`。**因不存在合法跨租户切换，移除后无需替代通道** | ① 普通请求不再携带 `X-Tenant-Id`；② 移除后 Ask/Web/MAUI 既有正常链路无回归 | ✅ 已完成｜376/376｜Web/MAUI 共享 ApiClient 已移除自动头 |
| **SB-P0-03** | P0 | Auth | 禁止非开发环境默认 SigningKey | 只有 Development 可使用**显式配置**的开发密钥；Production、Staging、未知/自定义托管环境均不得回退默认值。启动时校验非空、最小长度/强度并 fail-fast | ① 非 Development 缺失、空值或弱 SigningKey 均无法启动；② Development 显式开发配置可启动；③ 日志不得输出密钥 | ✅ 已完成｜`AuthSigningKeyPolicy` fail-fast｜默认回退已删除 |
| **SB-P0-04A** | P0 | Auth | 正式密码认证 | 使用成熟的带随机盐密码哈希方案，补充密码设置/校验；制定现有 `PasswordHash=null` 用户的禁用、初始化或一次性重置策略；保留 OIDC/SSO 扩展口。`User.PasswordHash` 已建模，但是否需要非空约束迁移由兼容策略决定 | ① 错误密码与空 Hash 账号无法登录；② 密码不明文存储且日志/审计不泄露；③ 登录失败限流并入审计；④ 旧账号处理有自动化测试 | ✅ 已完成｜387/387｜PBKDF2 正式口令认证 |
| **SB-P0-04B** | P0 | Auth | Token 生命周期与撤权生效 | 明确 Access Token 过期、禁用账号、角色撤销/权限变更后的失效语义；优先采用 `SecurityStamp/TokenVersion` 或服务端会话版本校验，禁止仅依赖 Token 内长期静态 `perms`。Refresh Token/OIDC 可后续扩展 | ① 过期 Token 返回 401；② 用户禁用、角色撤销或权限降低后，在规定的最大窗口内旧 Token 不再授权；③ Token 重放/撤销策略有测试；④ 现有 API Token 兼容边界有记录 | ✅ 已完成｜387/387｜SecurityStamp 撤权/旧 Token 失效 |
| **SB-P0-05** | P0 | Authorization | DataSource 显式权限控制 | 先确定并落库用户/角色→DataSource 授权模型（含迁移、唯一约束与撤权）；同时校验 DataSource 属于 `EffectiveTenantId`。覆盖 Metadata Search/Table Selection、元数据扫描、QueryPlan Security Gate、SQL 执行层兜底；所有拒绝使用稳定错误码 | ① 无权限或跨租户 DataSource 不能查询、扫描元数据或执行 SQL；② 不能通过伪造 Plan/缓存命中绕过；③ 执行层不盲信上游；④ 撤权后缓存不复用旧结果；⑤ 授权模型迁移与回退可验证 | ✅ 已完成｜最终门禁 408/408｜DataSource 授权 + PermissionFingerprint |
| **SB-P0-06** | P0 | Authorization | Row-Level Security | 明确策略主体（用户/角色/属性）、DataSource/Table/Column 绑定、组合优先级与 deny-by-default 规则；**固定落点为 Pipeline 之后、SqlQueryBuilder 之前**，对已定型 Plan 注入不可被用户条件覆盖的强制过滤。策略值必须参数化，禁止拼 SQL；覆盖 JOIN、子查询、聚合、Refine。默认不得改 Builder/ValidationPipeline | ① 自然语言、`/api/ask/refine`、手写条件、JOIN/子查询均不能绕过；② 注入条件参数化且与用户条件强制 AND；③ 无适用策略时按定义拒绝而非放宽；④ 策略变更立即使旧缓存失效；⑤ Golden 18/18 | ✅ 已完成｜最终门禁 408/408｜RLS + DataPolicyVersion｜Golden 18/18 |
| **SB-P0-07** | P0 | Security | 最终 QueryPlan Security Gate | 在 RLS 注入后、SqlQueryBuilder 前验证**最终 Plan**的 EffectiveTenant、ResolvedDataSource、Table、Column、Row Policy 与策略版本；失败必须短路，SQL Builder 和 Execution 均不可被调用。执行层继续保留 DataSource/租户兜底，形成纵深防御 | ① 未通过 Gate 的 Plan 永不进入 SQL Builder/Execution；② 篡改、缺失/过期策略、跨租户表列、RLS 注入异常均拒绝并审计；③ 测试验证下游 mock 零调用；④ Golden 18/18 | ✅ 已完成｜最终门禁 408/408｜最终 Plan Security Gate｜Golden 18/18 |
| **SB-P0-08** | P0 | Security | 非开发环境 CORS 收口 | Production、Staging、未知/自定义托管环境禁止无配置时 `AllowAnyOrigin`；允许无浏览器跨域需求的服务采取 deny-by-default | ① 非 Development 未设置白名单时启动失败或拒绝跨域；② Development 联调配置显式可见；③ 不允许 Origin 反射或通配凭据组合 | ✅ 已完成｜`CorsOriginPolicy`｜非 Development fail-closed |
| **SB-P0-09** | P0 | Diagnostics | 诊断/管理接口分级隔离 | ① `/test/*` 与 `/metrics` 立即纳入鉴权：无令牌 401、非治理角色 403；② `api/metadata-vector` 补平台治理权限；③ `/evaluation/*` 保持 Golden 调用契约，但 **Production 默认不映射路由**。显式启用时必须使用独立内部监听端口/独立诊断进程，或由受控反向代理按路径实施网络 ACL；不得用可伪造 Header 判断“内网”。若依赖来源 IP，须配置可信代理与 Forwarded Headers。权限绑定到 SB-P0-11 | ① `/test/*`、`/metrics` 无令牌 401、普通 Token 403；② metadata-vector 非治理角色 403；③ Production 默认访问 `/evaluation/*` 为 404；④ 显式启用后仅内部监听或网络 ACL 可达；⑤ Development/Test Golden 18/18 不变；⑥ 部署配置与回滚方式有文档 | ✅ 已完成｜DiagnosticsAccessPolicy + ProductionEvaluationRouteConvention｜Golden 18/18 |
| **SB-P0-10** | P0 | Audit | 安全事件审计 | 登录失败、越权拒绝、租户切换失败、QueryPlan 拒绝、**平台治理角色全部操作**进入审计；敏感请求体、密码、Token、连接串必须脱敏/禁止入账，审计存储采用追加写约束 | 高风险拒绝含 UserId / AuthenticatedTenantId / EffectiveTenantId / TraceId / Reason；数据面切换意图记录 RequestedTenantId；管理面操作记录 ManagementTargetTenantId/Action/Authorized；普通租户不可读取平台治理审计或其他租户审计 | ✅ 已完成（2026-09-02）：`AuditMiddleware` 前移并包裹鉴权、限流和端点，401/403 与端点异常均能按真实状态入账；登录失败、Token 失效、缺权限、跨租户、治理身份数据面拒绝及 `SB_SECURITY_001` QueryPlan 拒绝均记录稳定原因。高风险事件结构化保存 UserId、Authenticated/Requested/EffectiveTenantId、TraceId、ReasonCode/Reason 及管理目标/动作/授权结果；治理角色所有请求独立分类。审计链不读取请求体、Authorization 或连接串，外部手工记录强制绑定认证租户/用户并丢弃调用方快照、Actor 和 Message。租户查询严格排除平台与他租户日志；`SuperBIContext` 禁止修改或删除既有 `AuditLog`，保留追加写语义。全量 **411/411**，Golden **18/18 PASS**。 |
| **SB-P0-11** | P0 | Identity | 重构平台超级管理员为纯治理角色 | Seed 正 Id 的平台租户（如 `TenantCode=platform`），治理用户归属该租户；定义最小 `platform:*` 权限目录。**必须重定义或废弃当前 `TenantId=0`、拥有业务全权限且可分配给普通租户用户的 `platform-admin`**：移除 Dashboard/Metadata/DataSource 等业务权限，阻止普通租户 Identity API 枚举/授予/撤销治理角色，并迁移既有绑定与修正测试。首个治理账号只能通过受控部署 Seed/CLI/密钥管理初始化，不提供匿名 bootstrap API | ① 治理角色可执行租户、诊断、平台审计/配额治理；② 普通租户用户不能发现或获得治理角色；③ 既有全权限绑定已迁移/撤销；④ 治理账号请求 Ask、元数据业务读取或任意租户 DataSource 均 403；⑤ 所有治理操作入审计且不可绕过；⑥ 初始化可重复、凭据不进源码/日志；⑦ 不改 `User` 模型、不加成员表 | ✅ 已完成｜`platform-admin` 已重定义为纯治理角色｜业务权限剥离 |

---

## 二、P1 清单（企业级能力补齐）

| ID | 优先级 | 模块 | 开发任务 | 核心改造点 | 验收标准 | 状态 |
|---|---|---|---|---|---|---|
| **SB-P1-01** | P1 | Semantic | 建立 Canonical Semantic Model | 统一 Entity、Metric、Dimension、Filter、PhysicalBinding 的唯一 ID 与定义 | LLM、Metadata、Validator、Dashboard 引用同一语义对象 | ⬜ |
| **SB-P1-02** | P1 | Semantic | 统一字段解析规则 | 消除 QueryUnderstanding、Builder、Validator 各自解析字段的逻辑分叉 | 同一业务术语在各阶段解析结果一致 | ⬜ |
| **SB-P1-03** | P1 | QueryPlan | QueryPlan Pipeline Stage 化 | 抽象 `IQueryPlanStage`，拆解 Metadata / Semantic / Security / Repair / Confidence / Decision 阶段 | 新增一个 Stage 无需大改主流程 | ⬜ |
| **SB-P1-04** | P1 | QueryPlan | 收缩 QueryPlanBuilder 职责 | Builder 只负责构造，不承担语义判断、权限、安全策略 | Builder 可单元测试，职责边界清楚 | ⬜ |
| **SB-P1-05** | P1 | Governance | Column-Level Security | 敏感字段支持禁止访问、脱敏、按角色授权 | 无权限字段不出现在 QueryPlan、SQL、结果中 | ⬜ |
| **SB-P1-06** | P1 | Governance | Query Cost Governance | 执行前估算 Limit、扫描范围、Join 数、复杂度、模型成本 | 高风险/高成本查询可拒绝或降级 | ⬜ |
| **SB-P1-07** | P1 | Governance | Decision Gate 扩展 | 由 bool 扩展为 `ALLOW / REJECT / ASK_CLARIFICATION / REQUIRE_APPROVAL / LIMITED_EXECUTION` | 低置信度查询不会直接执行 | ⬜ |
| **SB-P1-08** | P1 | Audit | AI Decision Audit | 记录 Question、Intent、QueryPlan、Repair、Confidence、Decision、SQL、模型版本 | 能完整追溯一次问数为何得到当前结果 | ⬜ |
| **SB-P1-09** | P1 | Cache | Ask Cache 完整上下文版本化 | P0-01 校正为 `ResolvedDataSourceId`；**安全维度不得等到 P1**：`PermissionFingerprint` 随 P0-05 落地，`DataPolicyVersion` 随 P0-06 落地。本项再补 `SemanticVersion`、`MetadataVersion`，统一稳定摘要算法、版本发布与淘汰机制，禁止拼接整串权限文本 | 权限、RLS/数据策略、语义模型或 metadata 任一变化后不会复用旧缓存；高权限结果不会返回给低权限主体 | ⬜ |
| **SB-P1-10** | P1 | Golden | Production Feedback 闭环 | 用户反馈 → Golden Candidate → Review → Baseline → Regression | 线上错误问题可进入回归集 | ⬜ |
| **SB-P1-11** | P1 | Testing | AI BI E2E 测试 | NL → API → QueryPlan → SQL → Test DB → Result 完整链路 | 至少覆盖单表、多表、权限、租户、错误修复 | ⬜ |
| **SB-P1-12** | P1 | Database | Migration / Upgrade 体系 | 明确 EF Migration、Seed、Schema Version、升级与回退策略 | 新环境可自动初始化；旧环境升级可验证 | ⬜ |
| **SB-P1-13** | P1 | Dashboard | Dashboard 生命周期 | Draft、Version、Publish、Rollback | 已发布版本与草稿隔离，可回滚 | ⬜ |
| **SB-P1-14** | P1 | App Builder | App 生命周期 | Draft、Version、Publish、Rollback、Permission | App 发布可追踪版本，不直接覆盖线上 | ⬜ |
| **SB-P1-15** | P1 | Agent | Agent Runtime 基础 | Tool Registry、权限、执行状态、Retry、Approval | Agent 不再只是 Planner，能安全执行受控工具 | ⬜ |
| **SB-P1-16** | P1 | Identity | 多租户成员关系（一用户多租户） | **前置：SB-P0-02 完成**。当前用户恒属单租户，本项引入 `UserTenant` 成员表 + 迁移 + 切换授权 + `EffectiveTenantId` 的真正切换语义 + 前端切换 UI。属产品能力而非安全修复，故不阻塞 P0 | 合法成员可在其所属多租户间切换；非成员切换仍 403；切换全程进审计 | ⬜ |

---

## 三、P2 清单（架构治理）

| ID | 优先级 | 模块 | 开发任务 | 状态 |
|---|---|---|---|---|
| **SB-P2-01** | P2 | Frontend | 拆分 God `ApiClient` 为 BI、Dashboard、App、Agent、Identity、Admin 客户端 | ⬜ |
| **SB-P2-02** | P2 | Architecture | 统一 Application / Infrastructure / Api 的 Namespace 与目录边界 | ⬜ |
| **SB-P2-03** | P2 | Semantic | 将 BusinessTerm 从字符串逐步升级为强类型语义引用 | ⬜ |
| **SB-P2-04** | P2 | Diagnostics | 生产 API 与 Internal Diagnostics API 分区 | ⬜ |
| **SB-P2-05** | P2 | Observability | Metrics 增加 QueryPlan latency、LLM latency、DB latency、Repair rate、Reject rate | ⬜ |
| **SB-P2-06** | P2 | Error Handling | 统一前后端错误码体系，前端不再依赖错误字符串 | ⬜ |
| **SB-P2-07** | P2 | Config | Development / Test / Production 配置校验统一化 | ✅ 已完成｜M9-07 统一配置校验框架（fail-fast + 不泄密）｜`StartupConfigurationValidator` 接入 `Program.cs` |
| **SB-P2-08** | P2 | Testing | 增加 Web / Blazor UI 关键链路自动化测试 | ✅ |
| **SB-P2-09** | P2 | Dashboard | DSL Schema Version 与兼容升级 | ✅ 已完成｜M9-09 Dashboard DSL 版本兼容（旧 DSL 可加载/升级/回滚，Upgrade 归一化 + 回滚快照）｜`DashboardDslSerializer.Upgrade` |
| **SB-P2-10** | P2 | App / Agent | DSL Schema Version 与兼容升级 | ⬜ |

---

## 四、推荐开发顺序

### Sprint 1 — Security Perimeter & Trusted Runtime Context
`SB-P0-03 → SB-P0-08 → SB-P0-09 → SB-P0-11 → SB-P0-02 → SB-P0-02A → SB-P0-02B → SB-P0-02C → SB-P0-01 → SB-P0-05`

- `P0-03/P0-08` 先关闭配置型默认放行；`P0-09` 先关闭匿名诊断暴露，并可先用 Feature Flag 默认不映射，随后接入 `P0-11` 细粒度治理权限
- `P0-11` 必须先消除既有全权限 `platform-admin`，再为 `P0-02A` 与诊断面提供权限承载
- `P0-01` 的缓存同源验收须在本 Sprint 内完成，不可延后
- `P0-05` 落地时同步加入 `PermissionFingerprint` 缓存隔离，不等待 P1-09

### Sprint 2 — Authentication & Data Security
`SB-P0-04A → SB-P0-04B → SB-P0-06 → SB-P0-07 → SB-P0-10`

`P0-06` 落地时同步加入 `DataPolicyVersion` 缓存隔离；执行顺序固定为：DataSource 授权 → Plan 成型 → RLS 注入 → 最终 Security Gate → SQL 构建 → 执行层兜底。

### Sprint 3 — Semantic & QueryPlan
`SB-P1-01 → SB-P1-02 → SB-P1-03 → SB-P1-04 → SB-P1-07`

### Sprint 4 — Governance & Quality
`SB-P1-05 → SB-P1-06 → SB-P1-08 → SB-P1-09 → SB-P1-10 → SB-P1-11`

### Sprint 5 — 平台产品化
`SB-P1-12 → SB-P1-13 → SB-P1-14 → SB-P1-15 → SB-P1-16`

### 未纳入本轮（P3 长期项，不阻塞）

Distributed Cache、Message Bus、Event Sourcing、Data Lineage、Data Quality、Metric Marketplace、Semantic Graph、Model Routing、Prompt Versioning、AI Cost Attribution、AI Observability、Multi-Agent Collaboration。

---

## 附录 A：源码落点（已核验）

| 任务 | 关键文件与行号 | 现状 |
|---|---|---|
| P0-01 | `AskController.cs:81` | `_bi.AskAsync(question, tenantId)` **未传递** dataSourceId |
| P0-01 | `AskController.cs:72 / :83` | 缓存 Get/Set 使用**前端** `request.DataSourceId` |
| P0-01 | `BIConversationService.cs:240-244` | 执行使用**管道推断的** `plan.DataSourceId` → 与缓存键不同源 |
| P0-01 | `QueryPlanValidator.cs:151 / :187 / :195` | 已要求 `DataSourceId` 有效且与表一致 → 指定数据源必须在搜索/选表前约束 |
| **P0-01（先例）** | **`IQueryPlanBuilder.cs:13 / :19-21`** | ✅ 已有 `BuildAsync(intent)` 与 `BuildAsync(intent, resolution?)`，注释"Resolution 为 null 时保持原有行为"——**"可选入参 + null 保持旧行为"是本接口既有范式，直接沿用** |
| P0-02（模型） | `User.cs:18`、`SuperBIContext.cs:343` | `TenantId` 为标量；注释"用户恒归属某一租户"；无 `UserTenant` 成员表 → 单租户恒等 |
| P0-02 | `AskController.cs:164-167` | ✅ 读令牌 `tid`，**已正确** |
| P0-02 | `AuthMiddleware.cs:70 / :77` | 写入 `tid` 声明与 `Items["TenantId"]` |
| P0-02A | `DashboardController.cs:70/99`、`AppBuilderController.cs:62/86`、`AgentController.cs:67/88`、`IdentityController.cs:56/73`、`ThemeController.cs:49/70`、`QuotaController.cs:30/38/47/56`、`AuditController.cs:36/38` | ⚠️ `ScopeTo(request.TenantId)` 零成员校验 |
| **P0-02A（新）** | **`TenantManagementController.cs:47 / :59 / :65`** | 🔴 租户创建 `[HttpPost]` **全文件无 `HasPermissionAsync`**，任何合法令牌可创建租户 |
| P0-02B | `ObservabilityMiddleware.cs:104`、`AuditMiddleware.cs:64` | 以 query `tenantId`/`X-Tenant-Id` 打标，存在审计污染 |
| P0-02C | `ApiClient.cs:32 / :36` | 前端自动发送 `X-Tenant-Id` 头（待移除） |
| P0-03 | `TokenService.cs:54`、`Program.cs:207-209` | 缺失时回退 `dev-insecure-signing-key-P11-change-in-prod` |
| P0-04A/04B | `AuthController.cs:24`、`User.cs:23`、`TokenService.cs:16-18/:47-108` | 登录不校验口令；`PasswordHash` 已建模；Token 内携带静态权限并仅按固定过期时间失效 |
| P0-08 | `Program.cs:215-218` | 缺 `Cors:AllowedOrigins` 即 `AllowAnyOrigin()` |
| P0-09 | `Program.cs:285`、`AuthMiddleware.cs:54-63 / :100-121` | `/metrics` 非 `/api/*`，走放行分支 → **完全匿名** |
| P0-09 | `AITestController.cs`（`[Route("test")]`） | `understand`/`metadatasearch`/`sql` 直连正式 QueryUnderstanding、语义检索、SQL 链，且当前匿名 |
| P0-09 | `MetadataVectorController.cs`（`api/metadata-vector`） | 真实类名，**非 `QdrantController`**；受 `/api` 令牌保护但缺 Admin |
| P0-09 / Golden | `Api/Diagnostics/*` 的 `/evaluation/*` | Development/Test Golden 基础设施；Production 应默认不映射，例外启用走独立内部边界 |
| P0-11 | `AskController.cs:64`（用法范例）、`Program.cs:243`（`SeedAsync`） | `HasPermissionAsync` 既有用法；种子入口位置 |
| **P0-11（既有冲突）** | **`IdentityConstants.cs:6/:55-105`、`IdentityService.cs:188-203`、`P10AcceptanceTests.cs:71-74`** | 🔴 已存在 `TenantId=0` 的 `platform-admin`，含 Dashboard/Metadata 等业务全权限；全局角色可解析给租户用户，测试明确按“全权限”验收，必须重定义/废弃并迁移绑定 |
| Golden 边界 | `GoldenDatasetRunner.cs:20-22` | 依赖 Builder/ContextBuilder/ValidationPipeline，**不经 `BIConversationService` 与 SQL** |

---

## 附录 B：十三条易错提醒（施工前必读）

1. **P0-02 的越权入口不是 `X-Tenant-Id`**。真实风险是请求体/查询串 `TenantId` 被平台 Controller 直接采纳；前端头清理（P0-02C）不能替代成员授权整改。
2. **P0-02B 不能只把日志租户改成 token `tid`**。需同时保留 `RequestedTenantId` 与授权结果，否则无法追溯越权尝试。
3. **P0-01 的缓存同源不可延后**。键与执行不同源会导致"B 的结果缓存到键 A 下"，是静默错误答案，比报错更危险。
4. **P1-09 不能只做权限字符串拼键**。须用稳定 `PermissionFingerprint` 并纳入 `DataPolicyVersion`。
5. **P0-09 不得把 `/evaluation/*` 一刀切加普通 Admin 鉴权**，它属 Development/Test Golden 基础设施；Production 默认不映射，例外启用必须依赖真实网络边界，不能信任自报 Header。
6. **P0-09 的高危匿名面不仅是 `/metrics`，还有 `/test/*`**；尤其 `/test/sql` 会走到 SQL 构建与执行链。真实控制器名是 `MetadataVectorController`，不是 `QdrantController`。
7. **P0-01 不能在 Pipeline 之后覆盖 `plan.DataSourceId`**，必须在 Metadata Search / Table Selection 前形成约束。
8. **P0-06 / P0-07 的默认落点固定在 `BIConversationService` 的 Pipeline 后、SQL 前**，不要把 RLS/权限塞进 Golden 直接依赖的 Builder/ValidationPipeline。
9. **不要再花时间保护租户 0**。租户 0 写入已被现有 guard 系统性拦截（`IdentityController.cs:70/195/241`、`ThemeController.cs:63/156/178/249`、`AppBuilderController.cs:74/185/215`、`AgentController.cs:79/184/213`、`QuotaController.cs:32/40/49/58`、`AuthController.cs:51`）。真正的洞是**跨真实租户（A→B）**与**租户创建无鉴权**。
10. **平台治理角色绝不能读取租户业务数据**。既有全局 `platform-admin` 的业务全权限与此冲突，必须重定义/废弃并迁移存量绑定；不能仅新增 `platform:*` 后保留旧角色。
11. **P0-01 直接沿用 `IQueryPlanBuilder` 既有的"可选入参 + null 保持旧行为"范式**，不要另起炉灶设计新接口。
12. **缓存安全维度跟随 P0 落地**：P0-05 同步 `PermissionFingerprint`，P0-06 同步 `DataPolicyVersion`；否则撤权或 RLS 变化后仍可能命中旧的高权限结果。
13. **管理目标不是有效业务租户**：治理角色跨租户管理时使用 `ManagementTargetTenantId`；不得把它赋给 `EffectiveTenantId`、`PlatformContext` 的业务租户 Scope 或 BI/DataSource 链路。P0-02 的跨租户 403 限定于数据面，不能反过来阻断已授权治理操作。

---

## 附录 C：修订记录

| 版本 | 编号 | 原表述 | 修订 | 依据 |
|---|---|---|---|---|
| v1.0 | SB-P0-02 | Tenant 由 `X-Tenant-Id` 直接决定 | 拆为 02/02A/02B/02C；真实入口是请求体/查询串 `TenantId` 无成员校验 | `AskController.cs:164-167`、`DashboardController.cs:70/99` |
| v1.0 | SB-P0-01 | 未提缓存与请求/解析数据源区分 | 区分 `Requested/Resolved`；QueryPlan、执行、缓存统一以 `Resolved` 为准 | `AskController.cs:72/83` vs `BIConversationService.cs:240-244` |
| v1.0 | SB-P1-09 | Key 需加入 DataSourceId | 已在键中，改为 `PermissionFingerprint`/`SemanticVersion`/`MetadataVersion`/`DataPolicyVersion` | `AskController.cs:72/83` |
| v1.0 | SB-P0-09 | 诊断接口笼统按 Admin 隔离，误指 `QdrantController` | 三级处置；真实类名 `MetadataVectorController`；`/evaluation/*` Golden 豁免 | `AITestController.cs`、`MetadataVectorController.cs` |
| v1.0 | SB-P0-04 | 完成正式账号认证 | 补：`User.PasswordHash` 已建模，无需迁移 | `User.cs:23` |
| v1.0 | 全局 | 门控隔离被写成绝对约束 | 改为"优先保持稳定；安全修复可受控改变默认行为" | 安全整改优先级 |
| v1.0 | SB-P0-01/06/07 | 未写死 Golden 落点 | 新增约束 3 爆炸半径规则 | `GoldenDatasetRunner.cs` |
| **v1.1** | **SB-P0-02** | 设计含"合法成员切换 B → EffectiveTenantId=B" | **简化为单租户恒等**：`EffectiveTenantId := AuthenticatedTenantId`，跨租户一律 403，零迁移；多租户成员登记为 SB-P1-16 | `User.cs:18`、`SuperBIContext.cs:343`、无 `UserTenant` DbSet |
| **v1.1** | **SB-P0-02A** | 仅覆盖七类 ScopeTo Controller | **增补 `TenantManagementController` 租户创建鉴权**（当前零校验，任何令牌可创建租户） | `TenantManagementController.cs:47/59/65` |
| **v1.1** | **新增 SB-P0-11** | （无） | 平台超级管理员（治理角色）：平台租户 + `platform:*` 权限码，零新增表；红线为不得读取租户业务数据 | `AskController.cs:64`、`Program.cs:243` |
| **v1.1** | **SB-P0-02B** | 验收含"合法切换时同时记录 A 与 B" | 因无合法切换，改为恒等 + `TenantSwitchAuthorized=false`；未来由 SB-P1-16 扩展 | 同上 |
| **v1.1** | **SB-P0-01** | 未定义"指定 A 但 A 内无匹配表" | 明确返回「所选数据源下未找到相关表」，禁止静默回退 B | `QueryPlanValidator.cs:151/187/195` |
| **v1.1** | **Sprint 1/2** | P0-03 在 Sprint 2 | P0-03 前移 Sprint 1；P0-11 置入 Sprint 1 为 02A/09 提供权限承载；Sprint 2 由 7 项降为 6 项 | 工作量与依赖排序 |
| **v1.2** | **约束 1** | 全量单测固定写为 308/308 | 改为当前记录基线 + `master` 动态下限 + 零失败；安全测试必须验证无泄露、无副作用和审计 | 新增测试后固定总数会立即过期 |
| **v1.2** | **SB-P0-11** | 新增平台租户与 `platform:*`，未处理既有全权限角色 | 明确重定义/废弃现有全局 `platform-admin`、迁移绑定、隔离授予入口与安全初始化 | `IdentityConstants.cs`、`IdentityService.cs`、`P10AcceptanceTests.cs` |
| **v1.2** | **SB-P0-02A** | 仅强调租户创建/停用 | TenantManagement 列表、详情、设置读写等全部端点分级鉴权 | `TenantManagementController.cs` 全文件 |
| **v1.2** | **SB-P0-04/05/06/07** | 认证、DataSource、RLS 与 Gate 可施工细节不足 | 拆分密码与 Token；补授权模型/迁移、参数化 RLS、最终 Plan Gate、执行层兜底与稳定错误码 | 撤权时效、策略绕过与纵深防御要求 |
| **v1.2** | **SB-P0-09** | “内网/localhost + Feature Flag”实现含糊 | Production 默认不映射；例外采用独立内部监听/进程或可信反代 ACL，禁止伪造 Header | 单服务无法天然按路径绑定 localhost |
| **v1.2** | **SB-P1-09 / Sprint** | 权限与策略版本全部推迟到 P1 | `PermissionFingerprint` 随 P0-05、`DataPolicyVersion` 随 P0-06 前移 | 防止撤权/RLS 变化后复用高权限缓存 |
| **v1.2** | **SB-P0-02/02A/02B** | `RequestedTenantId != AuthenticatedTenantId` 一律 403，会阻断平台治理角色管理目标租户 | 显式分离数据面 EffectiveTenant 恒等与管理面 ManagementTargetTenant 授权；管理目标不得进入业务数据 Scope，并补充双上下文审计 | 平台治理用户归属平台租户，但需依法管理其他租户 |
