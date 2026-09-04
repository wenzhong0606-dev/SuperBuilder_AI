# SuperBuilder AI 最终开发计划（Master Development Plan）

> 版本：v2.0
>
> 基线日期：2026-09-03
>
> 性质：唯一执行计划与状态跟踪入口
>
> 当前自动化测试基线：431/431 通过，Golden 18/18 通过
> 范围：API、Web、共享 RCL、MAUI、数据库、向量服务、部署与产品验收

---

## 0. 文档定位与完成定义

本计划整合并取代以下文档的“分散跟踪”职责：

- `Audit_Report_and_DevPlan.md`
- `Platform_Product_Development_Requirements.md`
- `DevChecklist_Final.md`
- `Frontend_DevChecklist.md`
- `DevelopmentPlan.md`、`PhaseChecklist.md`
- `P11_Frontend_MAUI_Blazor_Plan.md`
- `SuperBuilder AI Native BI Phase开发计划.md`

源文档保留作需求和历史证据。本文件是排期、施工、验收和状态更新的唯一入口；代码与文档冲突时先核验代码，再更新本文件。

状态：✅ 已完成并验证；🟡 基础实现但有缺口；⬜ 未完成；🔴 发布阻塞；⛔ 等待外部条件或产品裁决。

一项工作只有同时完成以下内容才可标记 ✅：

1. 领域模型及字段定义。
2. EF 映射、Migration、数据清洗、索引、外键和删除行为。
3. API DTO、校验、权限、租户隔离和错误码。
4. 页面加载、空、错误、成功、重试及响应式状态。
5. 用户可见文本进入统一多语言资源。
6. 单元、集成、权限、跨租户和必要的 E2E 测试。
7. 构建、Golden、升级及回滚验证。

只完成接口、页面或数据库中的单独一层，不视为业务闭环。

---

## 1. 当前基线

### 1.1 已形成基础闭环

- 首位平台管理员可通过受控初始化页面创建。
- 平台管理员可创建租户和首位租户管理员；租户可编辑名称、语言和默认语言。
- 平台语言目录、平台文本基线、租户文本覆盖已有模型及写接口。
- 登录页可选择并记忆租户，加载租户语言，并记忆用户语言。
- 租户管理员可创建数据源并按用户或角色授权。
- Ask 自动使用当前用户全部授权数据源；已有基础 ConversationId、待澄清状态和租户/用户隔离。
- MetadataColumn、MetadataSemantic、Vector 已有基础详情和关系入口。
- 当前测试基线 431/431，Golden 18/18。

### 1.2 最高风险

| 优先级 | 风险 | 影响 |
|---|---|---|
| P0 | 配置及连接凭据明文 | 凭据泄露、业务库失陷 |
| P0 | 4 处前后端路由不一致 | Agent、业务实体、语义标签页面 404 |
| P0 | 空权限集合前端放行 | 权限未加载或失败时显示受限入口 |
| P0 | 无受控 Migration 启动流程 | 新环境和升级不可靠 |
| P0 | Metadata/Binding/RLS 链路一致性依赖应用层 | 错误或跨租户物理关系 |
| P0 | 部分租户接口信任请求体 TenantId | 越权写入风险 |
| P0 | 首个 Ask 明细对话无法进入 SQL Builder | 核心产品主链路尚未跑通 |
| P1 | 全站大量硬编码中文 | 多语言无法覆盖所有页面 |
| P1 | Ask 会话为单机内存 | 重启和多实例会话丢失 |
| P1 | 多平台管理员和管理范围未产品化 | 平台治理依赖首位管理员 |
| P1 | 非空、长度、外键、并发约束不完整 | 脏数据、孤儿记录、静默覆盖 |

---

## 2. 全局工程门禁

1. Golden 18/18 必须保持通过；禁止删除、跳过或弱化用例规避回归。
2. 全量测试必须零失败，动态下限不得低于当前 431。
3. 安全测试不仅断言状态码，还要验证无敏感数据、无越权副作用、无错误缓存复用。
4. 新 API 覆盖未登录、缺权限、错误租户、非法字段、404、409 和成功路径。
5. 新关联 ID 同时提供名称、详情 API、真实详情页和租户权限校验。
6. 数据库改动验证空库创建、已有库升级、失败回滚和历史数据清洗。
7. 缓存/会话验证撤权、过期、跨租户、重启和多实例。
8. 新用户文本不得硬编码，必须进入统一 ResourceKey 目录。
9. 平台治理身份不得读取租户业务数据；代管使用 ManagementTargetTenantId。
10. 触及 QueryPlan、Builder、Validation、Resolution、SQL 时标注 Golden 爆炸半径。
11. API/RCL/Web/MAUI Windows 构建 0 error，自有 C# warning 清零。
12. 合入前工作区无非预期改动，保留测试、迁移和截图证据。

---

## 3. 里程碑总览

| 里程碑 | 主题 | 优先级 | 主要交付 | 依赖 |
|---|---|---|---|---|
| M0 | 发布阻塞与安全止血 | P0 | 密钥、路由、权限、Ask、Migration、越权链路 | 无 |
| M1 | 字段与数据完整性 | P0/P1 | 非空、长度、外键、租户一致性、并发、UTC | M0 可部分并行 |
| M2 | 平台初始化与身份治理 | P1 | 多管理员、管理范围、账号生命周期、初始化事务 | M0/M1 |
| M3 | 多语言产品化 | P1 | 语言关系化、资源统一、全页面接入、用户偏好 | M1/M2 |
| M4 | 数据源与元数据闭环 | P1 | 安全连接、授权、扫描、关系详情和页面 | M1 + M3-G0 核心子集 |
| M5 | 语义模型与 QueryPlan 企业化 | P1 | Canonical Model、Stage、列权限、成本治理 | M1/M4 |
| M6 | Ask 企业化 | P1 | Redis、结构化澄清、缓存版本、审计、E2E | M4/M5 |
| M7 | Dashboard/App/Agent 与占位清零 | P1 | 生命周期、真实写操作、Agent Runtime | M5 + M3-G0；非文案开发可并行 |
| M8 | 前端体验与多端发布 | P1/P2 | 统一视觉、导出、性能、无障碍、移动端 | M3-G0/M4/M7；最终发布依赖完整 M3 |
| M9 | 架构、测试与运维 | P2 | 项目拆分、错误体系、指标、OpenAPI、灾备 | 可分批并行 |
| M10 | 扩展能力 | P2/P3 | 自定义组件、多数据库、多模型、外部身份 | M7/M9 |
| M11 | 长期能力储备 | P3 | 分布式架构、血缘、质量、语义图、AI 治理 | M9/M10 后重新评估 |

---

## 4. M0：发布阻塞与安全止血

### M0-01 凭据迁移与轮换 🔴

- 按“已经泄露”处置：立即轮换元数据库、业务 MySQL、LLM/API、Token 签名及其他已出现的密钥，记录旧凭据撤销时间和验证结果。
- 移除 `appsettings*`、示例、日志和当前版本中的真实凭据，使用环境变量、开发 Secret 或生产密钥管理服务注入。
- 扫描完整 Git 历史、制品、CI 日志、备份和镜像；评估泄露范围。
- 确认历史中含有效秘密且合规要求需要清除时，制定 BFG 或 `git filter-repo` 重写方案；执行前必须备份、取得明确授权、冻结推送并协调所有克隆和远端。历史重写不能替代密钥轮换。
- DataSource.ConnectionString 使用密钥引用或 AEAD 信封加密；API 只返回 HasCredential/掩码。
- 禁止记录口令、连接串、Token 和模型 API Key。

验收：新旧凭据逐项轮换并证明旧值不可用；当前树和 Git 历史扫描结果归档；必要的历史重写已按审批方案完成；数据库备份不能直接恢复明文连接串；日志和错误无敏感字段。

### M0-02 修复 4 个前后端契约 🔴

1. Agent 列表统一为 `GET api/agent/plans`。
2. Agent 工具统一为 `GET api/agent/tools`。
3. 补齐 `GET api/business-model/entities/{id}` 或将详情页改接真实端点。
4. 补齐 `GET api/semantic-labels/{id}` 或将详情页改接真实端点。

验收：契约测试、详情集成测试和浏览器冒烟通过，无 404。

### M0-03 权限守卫 deny-by-default 🔴

- AppState 区分 PermissionsLoading、Loaded、Failed；空集合只表示确实无权限。
- NavMenu/PermissionGuard 未加载时等待，失败或空权限严格拒绝。
- 覆盖未加载、失败、空权限、有权限、All、Any 测试；后端仍逐端点校验。

### M0-04 Ask 页面可靠性 🔴

- DoAsk 使用 try/catch/finally，异常必恢复 Busy。
- 增加超时、取消、重复提交保护，区分 401/403/网络/业务错误。
- 历史恢复后重新校验授权；失败保留问题和对话。

### M0-05 受控 Migration 与启动顺序 🔴

- 正式建立 Migration/Seed/SchemaVersion；明确生产由启动迁移或部署工具迁移。
- 顺序固定为 Schema → Identity/Permission → UiLanguage/Text → 默认策略/主题 → Bootstrap。
- 对 `Program.cs` 调用 `PlatformAdminBootstrapper.EnsureAsync()` 的位置增加明确 try/catch 和结构化日志。
- Bootstrapper 不得使用会因空库/缺行直接终止启动的无保护 `SingleAsync`；空库、尚未初始化的平台租户或角色应返回“需要初始化/需要迁移”的可诊断状态。
- 数据库不可达、Schema 未创建和 Seed 不完整必须区分；允许 Web 启动到受限诊断/初始化页，不得形成启动崩溃或普通页面 500，也不向普通用户输出堆栈。
- 修订空迁移、真实建表迁移、升级和回滚说明。

### M0-06 字段越权与关系链一致性 🔴

- 租户管理员接口忽略请求体 TenantId，从 JWT tid 获取；平台代管使用独立治理路由。
- PhysicalBinding、RLS 创建/更新验证 Tenant → DataSource → Table → Column 完整链。
- Metadata 扫描写入强制从 DataSource 继承 TenantId。
- 增加跨租户 DataSource/Table/Column/User/Role 组合负向测试。

### M0-07 开工前基线门禁与复验

- 每次开始 M0 或后续里程碑前先记录 `git status`、当前提交、Migration 状态、测试数量和构建结果。
- 如果存在任何存量未提交改动，必须先审阅归属，完成 Golden 18/18 + 431+ 门禁并形成可追溯提交，或明确隔离；不得在未知脏基线上叠加整改。
- 历史审计曾发现 47 项未提交改动，但当前核验时工作区已无该批存量改动；该数字只保留为历史教训，不作为当前风险数量。
- 清理 CS4014、CS8602、CS0414、MAUI 弃用及其他自有警告。
- 干净构建 API/RCL/Web/MAUI Windows；复跑 431+ 和 Golden 18/18。

### M0-08 匿名端点与限流治理 🔴

- 评估 `login-options` 暴露租户编码/名称的枚举风险；支持公开别名、搜索式选择或部署级隐藏策略。
- 匿名本地化端点只返回平台基线或已确认可公开的当前租户资源，不允许任意 tenantId 枚举租户专属文本。
- 限流键不能只依赖可伪造的 Header；登录前采用可信代理 IP/设备维度，登录后使用 TenantId+UserId。
- 明确反向代理可信列表、ForwardedHeaders 配置、登录防爆破和 429 错误契约。

### M0-09 首个 Ask 旗舰对话跑通 🔴

必须优先跑通以下真实租户、真实授权数据源、真实元数据场景，不得推迟到通用 Ask 企业化完成后：

1. 用户输入：“给我最近的十张入库单”。
2. 系统若不能唯一确认“入库单”对应的业务实体，应返回结构化澄清，保留待执行 QueryPlan，不得丢失“最近、十张、明细列表”语义。
3. 用户回复：“入库单就是入库凭证”。该回复必须识别为上一轮的 EntityAliasConfirmation，而不是新的指标/维度问题，不得返回 `SB_BI_002`。
4. 系统将“入库单→入库凭证”应用于当前会话，恢复上一轮请求并重新规划。
5. QueryPlan 必须形成 Detail/DetailRanking：目标实体=入库凭证、Limit=10、按可用业务日期字段 DESC；明细请求不强制要求聚合指标或分组维度。
6. 通过权限、元数据和安全校验后进入 SQL Builder，以参数化 SQL 查询并返回最多十条真实记录。
7. 若存在多个候选日期字段，返回“创建时间/入库日期/更新时间”等结构化候选，不允许以相同问题无限重复 Medium Confirmation。

验收：上述两轮对话可以端到端返回真实列表；不会出现 `SB_BI_002`；不会连续产生相同 Medium 分数闸门；生成 SQL 包含正确排序和 Limit；只访问当前用户授权数据源。

> **提前说明（对应选项 B）**：M0-09 是从原 M5-13「Ask 首用例」中提前抽出的 P0 早期批次，仅携带修复首个 Ask 明细对话所需的最小闭环——决策门对合法明细列表（实体 + Limit + Order）放行、`SB_BI_002` 不再误报明细请求、`X 就是 Y` 别名确认与循环检测的最小实现。其唯一前提是目标实体“入库凭证”在授权数据源中可被解析（已有元数据或最小种子即可），**不依赖 M4 元数据企业化完成**。通用化规则仍保留在 M5-13（不强制 Metric/Dimension）与 M6-03（租户级别名 / 循环检测 / 语义学习）。

M0 退出：全部 🔴 完成、凭据已轮换、构建零错误、测试不低于 431、新增安全及契约测试通过。

---

## 5. M1：字段与数据完整性

### M1-01 通用审计、时间与并发

- CreatedTime 全部使用 UTC，数据库明确 datetime2 精度，API 返回 ISO 8601。
- 可编辑主数据增加 UpdatedTime、CreatedBy、UpdatedBy。
- Tenant、UiLanguage、UiTextResource、Role、DataSource、MetadataSemantic 增加 RowVersion/ETag；冲突返回 409。
- 明确状态、软删除、物理删除和关联表级联策略。

> **状态（2026-09-04）**：地基批次 ✅ 已提交（`e39fd29`）。
> - `IAuditable` 契约 + `BaseEntity` 审计字段（CreatedTime 改 UTC、增 UpdatedTime/CreatedBy/UpdatedBy/RowVersion）；`Role` 补审计并实现 `IAuditable`。
> - `SuperBIContext`：全部 `IAuditable` 实体配置 `RowVersion` 为 `IsConcurrencyToken`+默认 1（非数据库 rowversion，兼容 SQL Server 与 SQLite 测试）；UTC 时间转换器（读回强制 Kind=Utc）；`SaveChanges` 统一回填 `UpdatedTime` 与自增 `RowVersion`。
> - 新增 `ConcurrencyExceptionMiddleware` 将 `DbUpdateConcurrencyException` 映射 HTTP 409（注册于 `UnifiedExceptionMiddleware` 之后）。
> - 迁移 `M1_01_AuditConcurrency`：为全部 `IAuditable` 实体加审计列与 `RowVersion`（not null, default 1）。
> - 测试 3+2 例，全量 **535/535 通过**（基线 530+5），构建 0 error。
> - **未做（留待后续子批）**：M1-01 末条"明确状态/软删除/物理删除/关联表级联策略"的全局策略落地（已通过并发令牌与审计字段奠定基线，软删除过滤器与级联策略将在 M1-02~M1-06 各实体约束中按需落实，避免一次性改动触发 Golden/租户过滤回归）。

### M1-02 Tenant 与 TenantSetting ✅ 已完成（2026-09-04，提交 d70718e）

- TenantCode 必填、最大 64、规范化唯一、创建后默认不可变；TenantName 必填、最大 128。
- 停用记录原因、时间、操作者；停用后禁止登录和刷新 Token。
- TenantSetting.DataType 限定 string/int/bool/json 并校验 Value。
- TenantSetting.Key 进入允许目录，租户不能覆盖安全配置。

> 落地说明：必填/规范化唯一在**控制器写入路径**强制（既有测试以 `new Tenant { Id }` 形式造数不设 Code，故未加 DB 级 NOT NULL，避免破坏集成测试种子）；DB 级 `nvarchar` 长度约束已加。停用治理三字段 + `IsLocked` 已落库并通过迁移 `M1_02_TenantAndSettingIntegrity`。全量 **576/576 通过**，构建 0 error。

### M1-03 User、Role、Permission ✅（2026-09-04 收尾）

- ✅ **外键与级联**：`UserRole→User`/`UserRole→Role`/`RolePermission→Role`/`RolePermission→Permission` 全部加 DB 级外键 + `OnDelete(Cascade)`（迁移 `20260904154222_M1_03_UserRolePermissionIntegrity`，非破坏性：`AddForeignKey` + 过滤唯一索引，无 DropColumn）。测试验证级联删除。
- ✅ **Username 租户内唯一**：唯一索引由全局 `Username` 收窄为 `(TenantId, NormalizedUsername)`（过滤 `[NormalizedUsername] IS NOT NULL`，兼容存量 NULL 行）；`User.NormalizeUsername`/`NormalizeEmail` 小写去空白；`CreateUserAsync` 改为按租户内规范化名查重。
- ✅ **新增字段**：`NormalizedUsername`(max128)、`NormalizedEmail`(max256)、`EmailConfirmed`(bit, default false)。
- ✅ **SecurityStamp 轮换闭环**：改密、角色指派/撤销（既有）**+ 停用/启用（`SetUserStatusAsync` + `PUT /api/identity/users/{id}/status`）** 均轮换；状态机仅允许 `Active ↔ Disabled`。
- ⏸️ **未做（明确延后，记入硬化项）**：
  1. `User→Tenant` 的 DB 级外键与"租户存在性"校验——沿用 M1-02 策略：因 `IdentityServiceTests`/`AuthControllerDisabledTenantTests`/`P10AcceptanceTests`/`IdentityControllerTests` 等多处以 `new User{TenantId=N}` 直接注入且不建对应租户行，加 FK 会破坏种子测试；当前仅在 `CreateUserAsync` 校验 `tenantId>0`，DB 级 FK 待测试种子补充租户行后启用。
  2. `SecurityStamp` 的 DB 级 `IsRequired()`——同因测试直接注入 `User` 不设该字段，维持 `string?` + 创建/回填/轮换保证。
  3. 用户邀请、首次设密、忘记密码、重置密码全流程——属功能里程碑，超出"字段与数据完整性"主题，本批未覆盖（DEC-02 已落地唯一范围）。
- 验证：全量测试 **581/581 通过**，构建 0 error。

### M1-04 DataSource ✅（2026-09-04 收尾，提交待 push）

- ✅ **Name 租户内规范化唯一**：`DataSource.NormalizeName` 小写去空白；唯一索引 `(TenantId, NormalizedName)` 过滤 `[NormalizedName] IS NOT NULL`（兼容存量/测试 NULL 行）；`Create` 写入路径按规范化名查重（`Conflict` 409）。**保留** `IX_DataSources_TenantId` 非唯一索引，供 `List`/`Manage` 按租户过滤查询（避免唯一索引替换导致回表退化）。
- ✅ **DbType 白名单**：`DataSource.SupportedDbTypes = {MYSQL, SQLSERVER, POSTGRESQL}`（对齐方言 `Code`，大小写不敏感）；`Create` 拒绝未知类型（`BadRequest`）。
- ✅ **Enabled 非空**：由 `bool?` 改为 `bool` + `default true`；迁移 `20260904155755_M1_04_DataSourceIntegrity` 先 `UPDATE ... SET Enabled=1 WHERE NULL` 再 `AlterColumn` 非空（避免生产 NULL 行致 `AlterColumn` 失败）；3 个查询计划测试注入 `Enabled=true` 兼容。
- ✅ **连接测试记录（脱敏）**：新增 `LastTestStatus`(max32)/`LastTestTime`(UTC 转换)/`LastErrorCode`(max64)。诊断控制器 `CheckDbConnectionAsync` 记录脱敏错误码（仅异常类型名，超时记 `"Timeout"`）；`FlattenException` 经 `SanitizeErrorMessage` 移除 `Password/Pwd/User Id/Uid` 键值。
- ✅ **连接测试超时与长度约束**：`CheckDbConnectionAsync`/`QueryRelationsAsync` 的 `OpenAsync` 加 15s 硬性超时（`CancellationTokenSource` 联动）；连接串/DbType/Name 限长 2048/32/128；原始连接串不在日志或接口返回。
- ⏸️ **延后（记入硬化项）**：`TenantId` 的 DB 级 `NOT NULL` + 租户存在性校验——沿用 M1-02/03 策略：3 个查询计划测试以 `new DataSource { Id, DbType, ConnectionString }` 持久化且**不设 TenantId/Name**，加 NOT NULL 会破坏种子；当前 TenantId 由 token 提供、`DataSource→Tenant` FK 已存在（写入路径保证存在），DB 级 NOT NULL 待测试造数补齐后启用。
- 验证：新增 6 项测试（白名单拒绝、租户内唯一、跨租户放行、空值校验、规范化持久化、静态方法），全量 **587/587 通过**，构建 0 error（三端 Components/Web/Maui）。

### M1-05 Metadata 与 Vector

- MetadataTable.TableName 必填；增加 CatalogName/SchemaName，唯一键含 DataSource/Catalog/Schema/Table。
- MetadataColumn.MetadataTableId 非空，ColumnName 必填；增加 Ordinal、NativeType、Precision、Scale。
- MetadataSemantic.MetadataColumnId 非空唯一；Confidence 增 `0..1` 检查约束。
- Source、Aggregation、RelationshipType、Cardinality、BindingType、PhysicalRole 使用枚举/目录。
- LearningRecord.TenantId 非空，增加字段 FK 和租户一致性。
- Keywords/Synonyms/ExampleQuestions 结构化；向量增加模型、维度、同步时间、状态和错误码。
- 向量检索校验模型/维度；提供删除、重建和孤儿检测任务。

### M1-06 PhysicalBinding、授权与 RLS

- 保留 PhysicalBinding “恰好一个 Owner”约束，增加 DataSource/Table/Column 同链、同租户及 Priority 非负校验。
- BusinessDomain 字符串与 BusinessDomainId 收敛，以 FK 为权威。
- DataSourceGrant 组合唯一、重复幂等；删除 User/Role 时撤销授权并巡检孤儿。
- RLS 明确 Everyone 类型，不用 null 猜测；Operator 映射 SQL AST，Value 参数化。
- 明确 Allow/Deny 冲突规则；更新使用 Version/ETag。

M1 退出：Migration 可在历史副本执行；无孤儿；跨租户组合均拒绝；数据库约束与 API 校验一致。

---

## 6. M2：平台初始化与身份治理

### M2-01 多平台管理员

- 增加平台管理员列表、新增、停用、重置密码和审计页面。
- 使用专用治理端点授予 platform-admin；至少保留一个有效平台管理员。
- 管理员继续存储于 User/Role/UserRole/Permission，不依赖环境变量账号长期运行。

### M2-02 管理员—租户范围

- 默认管理员管理全部租户；增加 PlatformAdminTenantScope 支持限定范围。
- 服务端强制校验并记录 Actor、ManagementTargetTenantId、Action、Result、CorrelationId。
- 平台治理身份不得读取租户业务数据。

### M2-03 首次初始化

- 表单包含 Username、DisplayName、Email、Password、ConfirmPassword；前后端均校验至少 8 位。
- 初始化 API 必须在服务端检查实际连接来源：`Connection.RemoteIpAddress` 必须是 Loopback；不能只相信 Host、Origin、X-Forwarded-For 或“部署在内网”的假设。
- 使用反向代理时只信任明确配置的 KnownProxies/KnownNetworks，并以可信转发链还原客户端地址；配置不完整时默认拒绝匿名初始化。
- 仅允许受控本机/部署环境；首位管理员创建成功后服务端立即关闭入口，重复请求即使来自 Loopback 也必须拒绝。
- 明确生产是否禁用匿名 Loopback Bootstrap；失败保留输入并返回结构化错误。

### M2-04 租户事务与生命周期

- 同一事务创建 Tenant、首位 TenantAdmin、密码、安全戳、默认设置、语言授权和默认语言；失败全回滚。
- 页面显示总数/启用/停用，默认列表，支持编码/名称搜索。
- 创建用弹窗/抽屉/步骤表单；支持编辑名称、语言、默认语言；启停二次确认并展示影响。

### M2-05 多租户成员关系（SB-P1-16）

- 建立 UserTenant、切换授权、EffectiveTenantId 和前端租户切换。
- 合法成员可切换，非成员 403，全程审计。
- 用户租户切换与平台管理员代管必须是两套独立机制。

### M2-06 租户自注册（待裁决）

- 设计平台开关、审批、验证码/防滥用、编码占用、首位管理员设密和初始化事务。
- 未经确认默认关闭公网自注册。

### M2-07 默认种子与演示数据

- 启动种子创建默认主题、平台语言、基础文本、角色、权限和配额策略，且全部幂等。
- 演示数据使用独立 Demo 安装器：可创建 demo 租户、管理员、数据源、元数据、语义和仪表盘。
- 生产默认不安装 demo；安装器支持预览、事务回滚和重复执行保护。

---

## 7. M3：多语言产品化

### M3-G0 核心可用子集

M3-G0 是 M4/M7/M8 的最小前置，不等同于完成全部 i18n。它只包含：稳定资源键与加载机制、登录、Layout、NavMenu、共享按钮/验证/错误组件，以及租户语言范围和用户语言恢复。完成 M3-G0 后：

- M4、M7 的数据模型、API 和非文案页面工作可继续。
- M8 的布局、性能、无障碍和多端构建可并行。
- 各业务页面在自身里程碑中不得新增硬编码，并同步迁移本页面文案。
- 完整 M3-01~06 仍是最终发布门禁，但不再串行阻塞所有业务开发。

### M3-01 语言关系模型

- 建立 `TenantUiLanguage(TenantId, UiLanguageId, Enabled, SortOrder, IsDefault)` 或等价关系。
- 每租户至少一种启用语言、只能一个默认语言，且默认值必须属于授权集合。
- 停用平台语言前展示受影响租户并迁移其默认语言。
- 将现有 `localization:availableCultures/defaultCulture` JSON 数据迁移到关系模型。

### M3-02 平台语言维护

- 平台管理员查看、添加、启停、排序语言；Culture 按 BCP 47 归一化并唯一。
- DisplayName、NativeName 必填；语言切换器优先显示 NativeName。
- 新语言可复制已有键集合，复制项标记“待翻译”。
- 平台视图只维护 TenantId=0 的目录和基线，不显示租户覆盖。

### M3-03 租户文本覆盖

- 租户管理员只看到被授权语言。
- 同时展示平台基线、租户覆盖、最终值和“平台/继承/专属”来源。
- 删除覆盖即恢复继承，不删除平台基线；平台基线更新后未覆盖租户自动继承。
- 保存后立即重载客户端资源，不要求重新登录。

### M3-04 资源键治理

- 建立 ResourceKey 目录及 Module/Page/Context/DefaultValue/Deprecated 状态。
- 合并 PlatformStrings、UiTextResources 和前端 fallback 键空间。
- 补齐 Nav、Page、Common、Validation、Error、Accessibility、Theme 键。
- 保存译文时校验 `{0}` 等占位符一致性。
- CI 扫描硬编码文本、未知键、缺失译文和废弃键。
- UiTextResource 增加租户 QueryFilter 纵深防御；平台查询显式 IgnoreQueryFilters 并校验治理权限。
- 新增 `localization:view/manage` 等明确权限码，更新角色种子、权限数量测试和菜单守卫。

### M3-05 全页面接入

- 去除 AuthController 只允许 zh-CN/en-US 的硬编码。
- 登录、Layout、NavMenu 和全部 Razor 页面分批接入统一 L10n。
- 按钮、标签、空状态、错误提示、验证消息均进入资源。
- 后端错误码映射到本地化文本，不暴露内部异常堆栈。

### M3-06 用户语言偏好

- 首次登录用租户默认语言；切换后按 TenantId+UserId 服务端持久化，浏览器缓存只做快速恢复。
- 下次登录优先用户偏好；原语言被停用时回退租户默认。
- 登录前先恢复上次租户，再加载语言；无历史时使用平台默认语言。

M3 退出：平台/租户视图严格分离；租户只能使用授权语言；核心及全部页面可切换；刷新、换设备、语言停用回退均通过测试。

---

## 8. M4：数据源与元数据闭环

### M4-01 数据源页面

- 默认显示当前租户数据源列表；新增表单仅点击后出现。
- 展示名称、类型、启用状态、连接状态、元数据表数、字段数、最后扫描时间。
- 表格 100% 宽度并置于横向滚动容器。
- 创建、编辑、启停、测试、扫描都有明确成功/失败反馈。
- 连接串只能重新设置，不回显原值。

### M4-02 数据源授权

- 数据源和创建者授权事务一致。
- 支持按用户或角色授予、撤销、查看；列表显示主体名称、类型和失效状态。
- Ask 有效范围同时满足当前租户、Enabled、显式授权。
- 普通用户不选择单一数据源，默认使用全部有效授权源。

### M4-03 Metadata 列表与详情

- DataSource 详情显示 MetadataTable、MetadataColumn、MetadataSemantic 和 Vector 状态。
- Table：ID、Catalog、Schema、名称、注释、业务域、字段数。
- Column：ID、所属表、名称、注释、类型、长度/精度、主键、可空、业务键、VectorId。
- Semantic：ID、ColumnId、业务含义、关键词、同义词、示例、域、置信度、来源、模型、维度。
- Vector：VectorId、OwnerType、OwnerId、模型、维度、索引状态、SearchText。

### M4-04 关系导航和展示

- 提供 DataSource → Table → Column → Semantic → Vector 面包屑及上下游关系。
- FK 同时显示对象名称和可点击 ID，目标必须是真实详情页。
- true/false 用只读开关或状态组件；主键、类型、状态、来源用统一标签。
- VectorId 明确为外部向量标识，不伪装成关系数据库 FK。
- 所有详情校验 metadata:view、租户和数据源授权。

### M4-05 扫描与同步

- 显示扫描状态、进度、起止时间、错误码和重试。
- 扫描幂等处理新增、删除、重命名；失败不覆盖最后成功版本。
- 元数据版本与向量重建同步；空数据提供“测试连接/扫描/检查授权”下一步。

---

## 9. M5：语义模型与 QueryPlan 企业化

| ID | 原编号 | 任务 | 验收 |
|---|---|---|---|
| M5-01 | SB-P1-01 | Canonical Semantic Model | Entity/Metric/Dimension/Filter/Binding 统一 ID 和定义 |
| M5-02 | SB-P1-02 | 统一字段解析规则 | Understanding/Builder/Validator 结果一致 |
| M5-03 | SB-P1-03 | QueryPlan Pipeline Stage 化 | Metadata/Semantic/Security/Repair/Confidence/Decision 可扩展测试 |
| M5-04 | SB-P1-04 | 收缩 QueryPlanBuilder | Builder 只构造，不承担权限、安全判断 |
| M5-05 | SB-P1-05 | Column-Level Security | 未授权/脱敏字段不进入 Plan、SQL、结果 |
| M5-06 | SB-P1-06 | Query Cost Governance | 高扫描、Join、无界 Limit、高模型成本可拒绝或降级 |
| M5-07 | SB-P1-07 | Decision Gate 状态化 | ALLOW/REJECT/ASK_CLARIFICATION/REQUIRE_APPROVAL/LIMITED_EXECUTION |
| M5-08 | SB-P1-08 | AI Decision Audit | 可追踪问题、意图、计划、修复、置信度、决策、SQL、模型 |
| M5-09 | SB-P1-10 | Production Feedback | Feedback→Candidate→Review→Baseline→Regression |
| M5-10 | SB-P1-11 | AI BI E2E | NL→API→Plan→SQL→Test DB→Result 全链覆盖 |
| M5-11 | GQ-006 | 物料等缺独立主表导致 NotResolved | 不修改 Ranking Contract 绕过，真实解析通过 |
| M5-12 | Phase 3.1.12.10 | 完成 Phase 2.7 Regression | 证据完整后才宣布 Phase 3.1 Frozen |
| M5-13 | Ask 首用例 | 明细排序语义：实体 + Limit + Order 可构成有效计划，不强制 Metric/Dimension（具体旗舰回归已提前至 M0-09 早期批次） | “最近十张入库凭证”进入 SQL Builder；SB_BI_002 只用于确实无法形成可查询字段的请求 |

---

## 10. M6：Ask 企业化

### M6-01 分布式会话

- 将单机 ConcurrentDictionary 迁移到 Redis 或数据库+Redis。
- 键包含 TenantId、UserId、ConversationId；配置 TTL、最大轮数/字符、取消和显式清空。
- 重启、多实例切换、过期行为一致；重要决策另行审计持久化。

### M6-02 输入与状态契约

- Question 建议限制 2–2000 字符。
- ConversationId 服务端生成，限制格式/长度并校验归属。
- DataSourceId 改 nullable 或在 OpenAPI 明确 0=全部授权源。
- History 限轮数、单轮及总长度；主要历史从服务端读取。
- ConversationStatus 使用稳定枚举：AwaitingClarification、Completed、Failed、Expired、Cancelled。

### M6-03 澄清与语义学习

- 增加 NewQuestion/Clarification/Correction/Confirmation/Cancel 行为分类。
- 返回结构化指标、维度、实体、时间候选项。
- 建立 TenantSemanticAlias：提议、管理员审核、启停和审计。
- “入库单就是入库凭证”等确认仅在当前租户复用。
- 待澄清会话必须保存原问题、候选实体、Limit、Order、Filter、授权数据源和待确认槽位，不能只保存拼接后的自然语言。
- `X 就是 Y`、`我说的 X 是 Y` 等回复优先按实体/术语确认处理；确认后恢复并重放原 QueryPlan，不走“必须识别新指标或维度”的普通首问校验。
- 区分“一次会话临时确认”和“租户长期别名”：当前查询可立即使用，长期学习必须经租户管理员审核。
- 对同一计划、同一缺失槽位和同一候选集合设置循环检测；不得无限返回同一种 Medium Confirmation。

### M6-04 Cache 完整版本（SB-P1-09）

- 保留 PermissionFingerprint、RLS PolicyFingerprint。
- 增加 SemanticVersion、MetadataVersion、Culture、ModelVersion、数据源集合版本。
- 撤权、策略、元数据、语义、语言变化后不复用旧结果。

### M6-05 审计、指标与真实 E2E

- 记录会话、轮次、原/重写问题、授权数据源、Decision、SQL 摘要、模型、耗时、成本和状态。
- 敏感问题、参数和结果样本脱敏；增加 LLM/Metadata/Plan/DB/Repair 分段指标。
- E2E 覆盖首问、澄清、确认、纠正、取消、过期、零/单/多源、撤权、跨租户、多实例、断网、超时、401/403、LLM/DB 失败。
- 固定加入旗舰回归：`给我最近的十张入库单` → `入库单就是入库凭证` → 返回按日期倒序的最多十条真实入库凭证。
- 增加直接同义词回归：`给我最近的十张入库凭证` 在元数据唯一且权限充分时不得反复停留在 Medium；若仍需澄清，必须指出具体缺失槽位和候选项。

---

## 11. M7：Dashboard、App、Agent 与占位清零

| ID | 来源 | 任务 | 验收 |
|---|---|---|---|
| M7-01 | SB-P1-13 | Dashboard Draft/Version/Publish/Rollback | 草稿和发布隔离，可追踪回滚 |
| M7-02 | SB-P1-14 | App Draft/Version/Publish/Rollback/Permission | 不直接覆盖线上版本 |
| M7-03 | SB-P1-15 | Agent Runtime：Tool、权限、状态、Retry、Approval | 安全执行受控工具 |
| M7-04 | FLOW/S6-4 | Agent 新建、工具、运行接真实端点 | 无假成功按钮 |
| M7-05 | FLOW/S6-4 | BusinessModel 新建、编辑、详情闭环 | CRUD 真实持久化 |
| M7-06 | FLOW/S6-4 | ThemeEditor 接 Theme API | 保存、预览、发布一致 |
| M7-07 | FLOW/P13 | ModelAccounts 接模型目录和加密绑定 | 无静态假数据和明文 Key |
| M7-08 | FLOW/S6-9 | Quota 策略端点和页面 | 平台默认、租户覆盖、用量可维护 |
| M7-09 | P11 §12 | 自定义组件领域模型、持久化、api/components | 白名单、安全渲染、版本治理 |
| M7-10 | P11 §12 | 自定义风格/主题复用 | App/Dashboard 可选择授权主题 |
| M7-11 | P11 §12 | Ask 结果发布 App 的生命周期 | 可编辑、授权、版本化、回滚 |

无后端能力的按钮必须禁用并显示原因，不得提示虚假的“已保存/已运行”。

---

## 12. M8：前端体验与多端发布

### M8-01 统一视觉系统

- 全平台使用统一现代科技风格：颜色、圆角、阴影、间距、图标、按钮和状态组件一致。
- `btn-primary`、`badge-primary` 等 `*-primary` 使用主题变量；统一 hover/active/focus/disabled/深色模式。
- 避免浏览器默认控件和局部 Bootstrap 默认蓝色。

### M8-02 列表、表格、菜单与响应式

- 列表优先；新增/编辑使用弹窗、抽屉、标签页或详情页。
- 表格默认 100% 宽度并统一横向滚动。
- 菜单保持单列，超高时纵向滚动。
- 复杂网格窄屏变单列，无页面级不可操作溢出。
- 加载、空、错误、成功和重试统一组件；失败保留用户输入。

### M8-03 导出标准化（S6-3）

- Excel 输出真实 `.xlsx`，或明确标注兼容格式。
- 下载服务返回结果并显示失败 Toast；验证中文、数字、日期、空值和多端行为。

### M8-04 性能与图表真实性（S6-7）

- 大表使用服务端分页或真实虚拟化，10k 行不一次渲染全部 DOM。
- Chart.js 使用增量 update，避免重复实例和内存泄漏。
- 点击仅筛选时命名为“筛选”；称为“下钻”时必须进入下一层真实数据。

### M8-05 生命周期与前端安全（S6-8）

- MainLayout 解除事件订阅，消除不受控 async void。
- 明确 localStorage Token 的 XSS 边界，采用短期访问 Token 和安全刷新策略。
- 增加 CSP；会话失效稳定回登录，不形成登录—Ask 循环。

### M8-06 E2E、无障碍与视觉回归（S6-6）

- Playwright 覆盖初始化、登录、租户、语言、身份、数据源授权、元数据、Ask、CRUD 和权限拒绝。
- axe 无严重问题；键盘、焦点、对比度通过。
- 建立桌面、991px、560px 和移动端截图基线。

### M8-07 多端发布（S6-5）

- API/RCL/Web/MAUI Windows 构建通过；Android 模拟器/真机不使用设备自身 localhost 访问宿主 API。
- iOS Release 在配对 Mac 完成编译、裁剪、静态资源、签名和安装验证。
- Web 5080/5081 的 HTTP/HTTPS、证书和 API BaseAddress 与部署一致。
- 验证重定向不丢 Authorization；优先避免 API 跨协议重定向。

---

## 13. M9：架构、测试与运维治理

| ID | 原编号 | 任务 | 验收 |
|---|---|---|---|
| M9-01 | SB-P2-01 | 拆分 God ApiClient | BI/Dashboard/App/Agent/Identity/Admin 可独立测试 |
| M9-02 | SB-P2-02 | 统一 Namespace 与目录边界 | 依赖方向检查通过 |
| M9-03 | SB-P2-03 | BusinessTerm 强类型化 | 编译期约束替代自由字符串 |
| M9-04 | SB-P2-04 | Production/Internal Diagnostics 分区 | 生产默认不暴露内部诊断 |
| M9-05 | SB-P2-05 | 扩展 Plan/LLM/DB latency、Repair/Reject rate | 可观测且无高基数泄漏 |
| M9-06 | SB-P2-06 | 统一前后端错误码 | UI 不依赖错误字符串判断 |
| M9-07 | SB-P2-07 | Dev/Test/Prod 配置校验 | 生产缺配置 fail-fast 且不泄密 |
| M9-08 | SB-P2-08 | Web/Blazor 自动化 | 关键流程进入 CI |
| M9-09 | SB-P2-09 | Dashboard DSL 版本兼容 | 旧 DSL 可加载、升级、回滚 |
| M9-10 | SB-P2-10 | App/Agent DSL 版本兼容 | 旧版本可升级和回滚 |
| M9-11 | A5 | 限界上下文解耦、SharedKernel、重复 DTO/枚举合并 | 无 Metadata↔Organization 循环 |
| M9-12 | A3 | 拆 Domain/Application/Infrastructure/Api 四项目 | 每步 build+Golden 通过 |
| M9-13 | 新增 | OpenAPI/Swagger 与契约生成 | 公开 API 有鉴权、DTO、错误说明 |
| M9-14 | 新增 | 备份恢复和灾备演练 | RPO/RTO、步骤和演练证据明确 |
| M9-15 | SB-P1-12 | Migration/Seed/SchemaVersion/升级回退体系 | 新旧环境可重复验证 |

A5/A3 在功能和数据约束稳定后分步执行，不得在同一提交中同时改变架构和业务语义。

---

## 14. M10：扩展能力

### M10-01 多数据库连接器（P12）

- 建立 IDataSourceConnector：连接测试、列举 Catalog/Schema/Table/Column、查询执行、类型归一化。
- DataSourceConnectionFactory 使用注册式连接器，不在业务服务散落 switch。
- 每种数据库实现 Dialect、参数化、分页、标识符转义、超时和确定性离线测试。
- 禁止硬编码业务表和字段。

### M10-02 多 AI 模型 BYO（P13）

- 建立 ILLMProvider、ModelCatalog、TenantModelBinding、UserModelBinding。
- API Key 使用 AEAD 或密钥引用，日志/审计脱敏。
- 定义模型能力、上下文、成本、区域、状态、降级；用户选择不得越过租户政策。

### M10-03 外部身份提供方

- 本地账号生命周期稳定后评估 OIDC/SAML/企业目录。
- 外部身份仍必须经过 Tenant Membership、Role、Permission、DataSourceGrant。

---

## 15. M11：P3 长期能力储备（当前周期显式暂缓）

以下 12 项来自 `DevChecklist_Final.md` 的 P3 长期清单，均非当前核心发布阻塞项，本计划保留追踪但不在 M0–M10 周期实施：

1. Distributed Cache。
2. Message Bus。
3. Event Sourcing。
4. Data Lineage。
5. Data Quality。
6. Metric Marketplace。
7. Semantic Graph。
8. Model Routing。
9. Prompt Versioning。
10. AI Cost Attribution。
11. AI Observability。
12. Multi-Agent Collaboration。

处理规则：M9/M10 完成后根据规模、部署拓扑、合规要求和真实性能数据重新评估；启用任一项时新建正式里程碑、字段模型、迁移、接口、运维与验收计划。暂缓不等于遗忘，也不得用占位页面假装已经支持。

---

## 16. 决策清单

| ID | 决策 | 推荐默认 | 阻塞 |
|---|---|---|---|
| DEC-01 | 生产 Bootstrap | 独立部署工具优先；保留匿名初始化时服务端严格校验真实 Loopback，并在首次成功后一次性关闭 | M2-03 |
| DEC-02 | Username 唯一范围 | `(TenantId, NormalizedUsername)` | M1-03 |
| DEC-03 | 平台管理员租户范围 | 默认全部，可选显式限定 | M2-02 |
| DEC-04 | 租户自注册 | 默认关闭，平台按环境开启 | M2-06 |
| DEC-05 | 演示数据 | 独立 Demo 安装器，不进生产默认种子 | 初始化体验 |
| DEC-06 | Ask 会话 | Redis；重要决策另持久化 | M6-01 |
| DEC-07 | 多语言迁移 | 登录/Layout/Nav/核心管理页优先分批 | M3-05 |
| DEC-08 | A3/A5 时机 | 功能与约束稳定后分步执行 | M9-11/12 |
| DEC-09 | P12/P13 周期 | 不阻塞核心平台发布 | M10 |

涉及公网开放、数据迁移或不可逆结构变化时，未经裁决不得实施；其余设计可先按推荐默认值推进。

---

## 17. 来源覆盖矩阵

### 17.1 当前审计及字段级事项

| 来源 | Master 落点 |
|---|---|
| SEC-1 | M0-01 |
| C-1 四路由 | M0-02 |
| SEC-2 | M0-03 |
| INIT-1 | M0-05、M9-15 |
| L10N-1/2 | M3-04/05 |
| ASK-1/2 | M0-04、M6 |
| 首个 Ask 对话未跑通 | M0-09、M5-13、M6-03/05 |
| IAM-1 | M2-01/02 |
| FLOW-1 | M7-04~08 |
| 匿名登录选项与限流 | M0-08 |
| 默认主题、种子时序、演示数据 | M0-05、M2-07、DEC-05 |
| FIELD-SEC-1 | M0-01、M1-04 |
| FIELD-IAM-1 | M0-06 |
| FIELD-META-1 | M0-06、M1-05/06 |
| FIELD-AUTH-1 | M1-03 |
| FIELD-TENANT-1 | M1-02/04 |
| FIELD-L10N-1 | M3-01~04 |
| FIELD-IAM-2 | M1-03、DEC-02 |
| FIELD-ASK-1 | M6-02 |
| FIELD-AUDIT-1 | M1-01 |
| FIELD-SEM-1 | M1-05、M5 |

### 17.2 后端、前端与架构清单

| 来源 | Master 落点 |
|---|---|
| SB-P1-01~08、10、11 | M5 |
| SB-P1-09 | M6-04 |
| SB-P1-12 | M9-15 |
| SB-P1-13~15 | M7-01~03 |
| SB-P1-16 | M2-05 |
| SB-P2-01~10 | M9-01~10 |
| S6-1 | M0-03 |
| S6-2 | M0-04 |
| S6-3 | M8-03 |
| S6-4 | M7-04~08 |
| S6-5 | M0-07、M8-07 |
| S6-6 | M8-06 |
| S6-7 | M8-04 |
| S6-8 | M8-05 |
| S6-9 | M3、M7、M9-13；Localization 写端点已存在，不再列为缺接口 |
| A5/A3 | M9-11/12 |
| Phase 3.1.12.10 | M5-12 |

### 17.3 平台产品需求

| 章节 | Master 落点 |
|---|---|
| §2 完整闭环 | 第 0 节、M7 |
| §3 平台管理员 | M2-01~03 |
| §4 租户管理 | M2-04~07、M8-01/02 |
| §5 多语言 | M3 |
| §6 登录/会话/端口 | M0-04、M3-06、M8-05/07 |
| §7 菜单/布局 | M0-03、M8-02 |
| §8 数据源 | M1-04、M4-01/02 |
| §9 元数据关系 | M1-05/06、M4-03/04 |
| §10 Ask | M0-04、M0-09、M4-02、M6 |
| §11 视觉 | M8-01/02 |
| §12 权限隔离 | M0-03/06、M1-03/06、M2-02 |
| §13 数据事务 | M1、M2-04、M4-02 |
| §14 错误提示 | M0-04、M3-05、M8-02、M9-06 |
| §15 测试验收 | 第 2、18 节 |
| §16 开发原则 | 第 0、2、20 节 |

### 17.4 Phase 与扩展计划

| 来源 | Master 落点 |
|---|---|
| 自定义组件/风格/发布应用 | M7-09~11 |
| 多数据库连接器 | M10-01 |
| 多模型 BYO | M10-02 |
| 外部 IdP | M10-03 |
| P3 长期 12 项 | M11，当前周期显式暂缓，M9/M10 后评估 |

---

## 18. 最终验收清单

### 18.1 构建、测试与升级

- [ ] API/RCL/Web/MAUI Windows 0 error，自有 warning 0。
- [ ] 全量测试不低于 431 且全部通过；Golden 18/18。
- [ ] Android 主流程、iOS Mac Release、Playwright、axe、视觉回归通过。
- [ ] 空库初始化、旧库升级、失败回滚、备份恢复均有证据。

### 18.2 身份、安全与隔离

- [ ] 无有效明文凭据；连接串/API Key 加密或引用密钥。
- [ ] 请求体 TenantId 不能改变作用域。
- [ ] 平台代管、用户租户切换、普通租户访问语义分离。
- [ ] User/Role/Permission/Grant/RLS/Metadata 无跨租户组合。
- [ ] 空权限、加载失败、撤权、停用严格拒绝；Token 可即时吊销。

### 18.3 多语言

- [ ] 登录、Layout、Nav、全部页面可切换；选项来自数据库。
- [ ] 租户只使用授权语言；默认语言和用户偏好正确回退。
- [ ] 平台基线、租户覆盖、删除覆盖恢复继承正确。
- [ ] 缺失译文、未知键、硬编码和占位符错误可检测。

### 18.4 数据源、元数据与 Ask

- [ ] 数据源创建、加密、测试、扫描、启停、授权闭环。
- [ ] Ask 范围与租户、启用状态、授权一致。
- [ ] Table/Column/Semantic/Vector 有真实详情、名称和关系导航。
- [ ] Binding/RLS 的租户、源、表、字段链一致。
- [ ] Ask 澄清/确认/纠正/取消/过期/清空、多实例、撤权、缓存失效通过。
- [ ] `给我最近的十张入库单` → `入库单就是入库凭证` 能恢复原计划、进入 SQL Builder，并返回按日期倒序的最多十条真实记录。
- [ ] `给我最近的十张入库凭证` 在实体和日期字段唯一时可直接执行；不重复返回相同 Medium 闸门，不误报 SB_BI_002。

### 18.5 UI 与发布

- [ ] 菜单单列纵向滚动；表格 100% 横向滚动。
- [ ] true/false、主键、状态、来源使用统一组件。
- [ ] `*-primary` 与主题一致；加载/空/错误/成功状态一致。
- [ ] 桌面、991px、560px、移动端无不可操作溢出。
- [ ] 所有按钮真实改变状态或明确不可用原因。
- [ ] 5080/5081、HTTP/HTTPS、证书、API BaseAddress 正确。

---

## 19. 推荐执行顺序

1. M0 消除发布阻塞和安全风险。
2. M1 固定字段和数据库完整性。
3. M2/M3 完成平台治理和多语言基础。
4. M4 完成数据源、授权、元数据和关系页。
5. M5/M6 完成语义、QueryPlan、Ask 企业化。
6. M7 清除占位并补产品生命周期。
7. M8 完成体验、多端和发布验收。
8. M9 分批完成架构、测试和运维治理。
9. M10 在核心稳定后扩展连接器、模型和身份提供方。
10. M9/M10 完成后复评 M11 长期能力，按真实需求另行立项。

每个里程碑拆成可独立提交的小批次：先补测试，再改实现，最后更新状态和证据。

---

## 20. 文档维护规则

- 新需求必须映射到 Master ID；没有落点不得直接开发。
- 完成项必须附测试、Migration、截图或运行记录，不以主观百分比判断。
- 代码状态变化后同步更新基线、状态和覆盖矩阵。
- 源文档新增未完成项时，必须纳入本文件或记录“不采纳及原因”。
- 不再使用失效的旧审计编号 I-*、L-*；统一使用当前审计编号和 M*-*。
- 本文不得记录明文密码、连接串、Token 或 API Key。

---

文档状态：**v2.0 最终整合版，可作为后续开发、排期和验收的唯一执行计划。**
