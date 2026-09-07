# SuperBuilder AI 最终开发计划（Master Development Plan）

> 版本：v2.1
>
> 基线日期：2026-09-06
>
> 性质：唯一执行计划与状态跟踪入口
>
> 当前自动化测试基线：854/854 通过，Golden 18/18 基线保持
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
- M1 物理完整性收尾已完成：核心租户键、必填字段、父子关系和数据源同租户关系已下沉到数据库约束。
- M3 本地化收尾已建立可执行门禁：未知资源键、格式占位符漂移和 Razor 用户可见硬编码中文均可检测。
- 当前测试基线 854/854，Golden 18/18 基线保持。

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

> **2026-09-06 配置优先级硬化**：`appsettings.Local.json` 仅作本地默认值，加载后重新应用环境变量和命令行，保证部署注入始终具有最高优先级，避免迁移或启动误连本地库。

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

> **提前说明（对应选项 B）**：M0-09 是从原 M5-14「Ask 首用例」中提前抽出的 P0 早期批次，仅携带修复首个 Ask 明细对话所需的最小闭环——决策门对合法明细列表（实体 + Limit + Order）放行、`SB_BI_002` 不再误报明细请求、`X 就是 Y` 别名确认与循环检测的最小实现。其唯一前提是目标实体“入库凭证”在授权数据源中可被解析（已有元数据或最小种子即可），**不依赖 M4 元数据企业化完成**。通用化规则仍保留在 M5-13（不强制 Metric/Dimension）与 M6-03（租户级别名 / 循环检测 / 语义学习）。

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

> 落地说明：写入路径与 DB 双重强制必填/规范化唯一。2026-09-06 收尾迁移 `M1_ClosureIntegrity` 将 `TenantCode`/`TenantName` 收紧为 DB `NOT NULL`，历史空值按主键确定性回填，唯一索引不再依赖 nullable 过滤条件。

### M1-03 User、Role、Permission ✅（2026-09-04 收尾）

- ✅ **外键与级联**：`UserRole→User`/`UserRole→Role`/`RolePermission→Role`/`RolePermission→Permission` 全部加 DB 级外键 + `OnDelete(Cascade)`（迁移 `20260904154222_M1_03_UserRolePermissionIntegrity`，非破坏性：`AddForeignKey` + 过滤唯一索引，无 DropColumn）。测试验证级联删除。
- ✅ **Username 租户内唯一**：唯一索引由全局 `Username` 收窄为 `(TenantId, NormalizedUsername)`（过滤 `[NormalizedUsername] IS NOT NULL`，兼容存量 NULL 行）；`User.NormalizeUsername`/`NormalizeEmail` 小写去空白；`CreateUserAsync` 改为按租户内规范化名查重。
- ✅ **新增字段**：`NormalizedUsername`(max128)、`NormalizedEmail`(max256)、`EmailConfirmed`(bit, default false)。
- ✅ **SecurityStamp 轮换闭环**：改密、角色指派/撤销（既有）**+ 停用/启用（`SetUserStatusAsync` + `PUT /api/identity/users/{id}/status`）** 均轮换；状态机仅允许 `Active ↔ Disabled`。
- ✅ **2026-09-06 物理约束收尾**：测试 Fixture 先补齐租户根数据，再启用 `User→Tenant` DB 外键（`Restrict`）和 `SecurityStamp NOT NULL`；新增数据库负向测试验证孤儿 User 被拒绝。
- ⏸️ **功能性后续项，不属于 M1 数据完整性退出条件**：用户邀请、首次设密、忘记密码和重置密码产品流程应在身份产品化后续里程碑单独跟踪。
- 验证：全量测试 **581/581 通过**，构建 0 error。

### M1-04 DataSource ✅（2026-09-04 收尾，已推送 origin/master）

- ✅ **Name 租户内规范化唯一**：`DataSource.NormalizeName` 小写去空白；唯一索引 `(TenantId, NormalizedName)` 过滤 `[NormalizedName] IS NOT NULL`（兼容存量/测试 NULL 行）；`Create` 写入路径按规范化名查重（`Conflict` 409）。**保留** `IX_DataSources_TenantId` 非唯一索引，供 `List`/`Manage` 按租户过滤查询（避免唯一索引替换导致回表退化）。
- ✅ **DbType 白名单**：`DataSource.SupportedDbTypes = {MYSQL, SQLSERVER, POSTGRESQL}`（对齐方言 `Code`，大小写不敏感）；`Create` 拒绝未知类型（`BadRequest`）。
- ✅ **Enabled 非空**：由 `bool?` 改为 `bool` + `default true`；迁移 `20260904155755_M1_04_DataSourceIntegrity` 先 `UPDATE ... SET Enabled=1 WHERE NULL` 再 `AlterColumn` 非空（避免生产 NULL 行致 `AlterColumn` 失败）；3 个查询计划测试注入 `Enabled=true` 兼容。
- ✅ **连接测试记录（脱敏）**：新增 `LastTestStatus`(max32)/`LastTestTime`(UTC 转换)/`LastErrorCode`(max64)。诊断控制器 `CheckDbConnectionAsync` 记录脱敏错误码（仅异常类型名，超时记 `"Timeout"`）；`FlattenException` 经 `SanitizeErrorMessage` 移除 `Password/Pwd/User Id/Uid` 键值。
- ✅ **连接测试超时与长度约束**：`CheckDbConnectionAsync`/`QueryRelationsAsync` 的 `OpenAsync` 加 15s 硬性超时（`CancellationTokenSource` 联动）；连接串/DbType/Name 限长 2048/32/128；原始连接串不在日志或接口返回。
- ✅ **2026-09-06 物理约束收尾**：`TenantId`/`Name`/`NormalizedName`/`DbType`/`ConnectionString` 均收紧为 DB `NOT NULL`，`DataSource→Tenant` 为必需 `Restrict` 外键；历史名称与规范化名称确定性回填，`DbType` 归一化后通过白名单预检。
- 验证：新增 6 项测试（白名单拒绝、租户内唯一、跨租户放行、空值校验、规范化持久化、静态方法），全量 **587/587 通过**，构建 0 error（三端 Components/Web/Maui）。

### M1-05 Metadata 与 Vector ✅（2026-09-05 收尾，已推送 origin/master）

- ✅ **MetadataTable 字段与唯一键**：`TableName` 必填(max128)；新增 `CatalogName`(max128)、`SchemaName`(max128)、向量字段（`VectorId`/`EmbeddingModel`(max128)/`VectorDimension`/`VectorSyncTime`(UTC)/`VectorStatus`(max16)/`VectorErrorCode`(max64)）。租户内唯一键由 `(DataSourceId, TableName)` 升级为 `(DataSourceId, CatalogName, SchemaName, TableName)` 并加过滤 `WHERE [CatalogName] IS NOT NULL AND [SchemaName] IS NOT NULL`，保留 `IX_MetadataTables_DataSourceId` 供按租户过滤；**存量 NULL Catalog/Schema 同名表可共存**（兼容旧数据与测试种子）。迁移 `20260905004213_M1_05_MetadataVectorIntegrity` 先 `UPDATE ... SET TableName=N'table_'+Id WHERE NULL` 再 `AlterColumn` 非空（写入路径保证）。
- ✅ **MetadataColumn 字段**：`ColumnName` 必填(max128)；新增 `Ordinal`(default 0)、`NativeType`(max64)、`Precision`、`Scale`、`EmbeddingModel`(max128)、`VectorId`/`VectorDimension`/`VectorSyncTime`/`VectorStatus`/`VectorErrorCode`；唯一键 `(MetadataTableId, ColumnName)`。
- ✅ **MetadataSemantic 约束与结构化**：`MetadataColumnId` 非空唯一；`Confidence` 增 `decimal(5,4)` + 检查约束 `CK_MetadataSemantics_Confidence`（`0..1`，NULL 放行）；`Source` 由 `string?` 收敛为枚举 `SemanticSource`（`HasConversion<string>()` 存储 max16，默认 `Manual`）；`Keywords`/`Synonyms`/`ExampleQuestions` 经 `MetadataSemantic.ParseList`/`FormatList` 规范化（逗号/分号/换行/制表拆分、去空白、去空、忽略大小写去重，逗号分隔存储 max2048）；`SearchText`/`Vector*` 字段补齐。
- ✅ **受控词表（枚举化）**：新增 `MetadataVocabularies.cs`，定义 `SemanticSource`/`AggregationType`/`RelationshipKind`/`CardinalityKind`/`BindingKind`/`PhysicalRoleKind` 六枚举 + `MetadataVocabularyValidator`（大小写不敏感校验）；`MetadataCsvFixtureService` 经 `ParseSource` 装载语义来源（空/解析失败回退 `Manual`），`MetadataSemanticService` 新建语义 `Source=AI` 且经规范化与 `Math.Clamp(c,0,1)` 收敛置信度。
- ✅ **LearningRecord 租户化**：`TenantId` 由 `long?` 改为 `long`（必填）+ `Tenant` 导航 FK（写入路径保证存在）；新增 `MetadataColumn` 导航（`OnDelete SetNull`）；新增静态 `IsTenantConsistent(recordTenantId, columnTenantId)`；using 由 `Models.Identity` 修正为 `Models.Organization`（修复 CS0246）。迁移先 `UPDATE ... SET TenantId=1 WHERE NULL` 再非空。
- ✅ **向量状态闭环**：`MetadataVectorService.IndexAsync` 三段（Table/Column/Semantic）`try` 成功写 `VectorId`/`VectorDimension`/`VectorStatus="Synced"`/`VectorSyncTime=UtcNow`/`VectorErrorCode=null`，`catch` 写 `VectorStatus="Failed"`/`VectorErrorCode=异常类型名`（脱敏）；无 `SearchText` 置 `Pending`。
- ✅ **孤儿检测与维度校验**：`MetadataVectorIndexService` 构造函数注入 `IOptions<QdrantOptions>`；新增 `DetectOrphansAsync`（`IQdrantService.ListPointIdsAsync` 滚动列出全部 Point Id，与 DB `VectorId` 比对）与 `ValidateVectorsAsync`（期望维度 `(int)QdrantOptions.VectorSize`，不符标 `Stale`）；`RebuildAsync` 去掉 `AsNoTracking` 并 `SaveChangesAsync` 持久化状态；诊断控制器新增 `GET /api/metadata-vector/orphans`、`GET /api/metadata-vector/validate`。
- ✅ **2026-09-06 物理约束收尾**：`MetadataTable→Tenant` 为必需 `Restrict` 外键；`MetadataColumn.MetadataTableId` 收紧为必填并建立必需父外键；`MetadataTable(DataSourceId,TenantId)→DataSource(Id,TenantId)` 复合外键在 DB 层拒绝跨租户数据源/表组合。
- 验证：新增 16 项 `MetadataVectorIntegrityTests`（受控词表/结构化辅助、DB 约束：表唯一键/同目录放行/空 Catalog 兼容/列必填/新字段持久化/置信度 CHECK/学习记录租户 FK、向量状态 Synced/Failed、孤儿检测），**16/16 通过**；全量回归构建 0 error（三端 Components/Web/Maui）。

### M1-06 PhysicalBinding、授权与 RLS ✅（2026-09-05 收尾，本地提交待推送）

- ✅ **PhysicalBinding 约束收敛**：移除 P3 遗留硬 CHECK `CK_PhysicalBindings_ExactlyOneOwner`（既有 0-owner 种子不兼容，按 M1-02~M1-05 约定仅在写入路径强制），改由 `BusinessEntityService.ValidateBindingsAsync` 按父集合归属判定「恰好一个 Owner」（修复 detached-graph FK 未 fixup 导致的回归）；新增 DB 级 `CK_PhysicalBindings_PriorityNonNeg`（`[Priority] >= 0`）+ 写入路径双保险；新增两个组合索引（DataSource/Table/Column、四个 Owner 列）。
- ✅ **BusinessDomain 收敛**：`MetadataSemantic.BusinessDomainId` 为权威 FK（`OnDelete SetNull`），原 `BusinessDomain` 字符串保留作展示兼容；`BusinessDomains.TenantId` 索引。
- ✅ **DataSourceAccessGrant 治理**：`(TenantId, DataSourceId, SubjectType, SubjectId)` 组合唯一 + 重复幂等；新增 `TenantId→Tenants` 级联 FK（此前仅建至 DataSources）；`RevokeBySubjectAsync`（删除 User/Role 时跨数据源撤销其全部授权）；`DetectOrphanGrantsAsync`（`IgnoreQueryFilters` 巡检指向不存在 User/Role 的孤儿授权）。
- ✅ **RLS 硬化**：`CK_RlsPolicies_SubjectConsistency`（Everyone 类型 SubjectId/SubjectKey 均空；User/Role 必填 SubjectId；Attribute 必填 SubjectKey）+ `CK_RlsPolicies_Operator`（受控词表 `RlsVocabularyValidator.AllowedOperators`，与 `NormalizeToSymbol` 对齐）；Deny-wins 逐列；`Version` ETag 递增；Value 参数化。
- ✅ **迁移与快照一致**：`20260905020528_M1_06_PhysicalBindingAuthorizationRls` 先 DROP `CK_PhysicalBindings_ExactlyOneOwner` 再新增上述约束/索引/FK/列；CHECK 字面量统一为 SQLite 与 SQL Server 兼容的纯 `'...'`（去除 SQL Server `N'...'` 前缀）；Designer 快照与 ModelSnapshot 同步去除旧约束，避免未来迁移重复 DROP。
- 验证：新增 16 项 `M1_06_PhysicalBindingAuthorizationRlsTests`（RlsVocabularyValidator 单元、RevokeBySubject/DetectOrphanGrants 行为、写入路径 Priority 校验、RLS/PhysicalBinding CHECK、BusinessDomain/MetadataSemantic 收敛），**16/16 通过**；全量回归 **619/619 通过**，构建 0 error（三端 Components/Web/Maui）。

> **M1 Closure Batch 验收（2026-09-06）**：迁移 `20260906075147_M1_ClosureIntegrity` 在隔离 SQL Server 历史副本上完成 `Up → Down → Up`；执行 51001–51007 预检后再回填和收紧，且不留下永久默认约束。新增 5 个 `M1ClosureIntegrityTests`，覆盖必填列、孤儿 User/DataSource/MetadataColumn 和跨租户 MetadataTable；全量 854/854 通过。

M1 退出：✅ 已达成。Migration 可在历史副本执行并可回滚；孤儿核心关系和跨租户组合由数据库拒绝；数据库约束与 API 校验一致。

---

## 6. M2：平台初始化与身份治理

### M2-01 多平台管理员（后端治理 + Blazor 管理页面 ✅）

- ✅ 新增 `PlatformAdminService`（列表/新增/停用/启用/重置密码）+ 专用治理端点 `api/platform-admin`（仅 `platform:*` 治理主体可访问）。
- ✅ `platform-admin` 角色通过直接授予 `UserRole` 实现（被 `IdentityService.ResolveRoleId(s)Async` 刻意排除，防普通身份流越权），与 `PlatformAdminBootstrapper` 一致。
- ✅ 不变量：始终保留至少一名有效（Active）平台管理员，`DisableAsync` 在降至最后一名时拒绝。
- ✅ 治理动作写入 append-only `AuditLog`（Actor/Action=platform.admin.*/EntityType=PlatformAdmin）。
- ✅ 新增 10 例 `M2_01_PlatformAdminTests`；全量回归 629/629 通过（619+10），构建 0 error。
- ✅ Blazor 管理页面 `Admin/PlatformAdmins.razor`（列表 / 新建 / 停用·启用 / 重置口令 + 操作审计页签），经 `PermissionGuard(PlatformAdminManage)` 守卫；`NavMenuItems` 已登记「平台管理员」入口；RCL+Web+Maui 三端构建 0 error，全量回归 629/629 通过。
- 新增客户端权限码 `PermissionCodes.PlatformAdminManage = "platform:admin:manage"`，并补入 `IdentityCatalog`（PlatformAdmin 角色 + PermissionDef），种子幂等授予。

### M2-02 管理员—租户范围 ✅

- 默认管理员管理全部租户；增加 PlatformAdminTenantScope 支持限定范围。
- 服务端强制校验并记录 Actor、ManagementTargetTenantId、Action、Result、CorrelationId。
- 平台治理身份不得读取租户业务数据。

**实现（2026-09-05）：**
- 新增实体 `PlatformAdminTenantScope`（AdminUserId / TenantId / GrantedAt / GrantedBy），语义：表中无记录 = 默认管理全部租户；有记录 = 仅所列租户。迁移 `M2_02_PlatformAdminScopeAudit`（SQL Server）建表并对 `AuditLogs` 增加 `ManagementTargetTenantId`(bigint?)、`CorrelationId`(nvarchar(128)?) 两列。
- 范围服务 `IPlatformAdminScopeService` + `PlatformAdminScopeService`：`CanManageAsync` / `GetScopeAsync` / `SetScopeAsync`（幂等替换；空列表=恢复默认全部）。
- `TenantManagementController` 注入范围服务，对所有租户级操作（Get/Enable/Disable/Update/ListSettings/UpsertSetting）调用 `RequireInScopeAsync` 强制校验，越权返回 403；`List` 按范围过滤。
- 审计：扩展 `AuditLog` / `AuditLogEntry` 的 `ManagementTargetTenantId` 与 `CorrelationId`；`AuditMiddleware` 将治理操作目标租户与请求关联 Id 写入这两列（可查询专用列）。
- 平台治理身份不得读取租户业务数据：由 `AuthMiddleware` 既有 `GovernanceDataPlanePolicy.IsForbiddenDataPlane`（白名单 `tenant-management/localization/quota/audit/metadata-vector/auth/me`，其余 `/api` 路径对治理主体返回 403）结构性保证，本次仅确认并文档化，未改动其逻辑。
- 前端：`Admin/PlatformAdminScopes.razor`（gated by `PlatformAdminManage`），列出管理员及其范围摘要，弹窗以多选租户设定范围；导航项「管理员租户范围」。
- 测试：`TenantManagementControllerTests` 新增越权 403 / 范围内成功 / 全范围可见全部租户。

### M2-03 首次初始化 ✅

- 表单包含 Username、DisplayName、Email、Password、ConfirmPassword；前后端均校验至少 8 位。
- 初始化 API 必须在服务端检查实际连接来源：`Connection.RemoteIpAddress` 必须是 Loopback；不能只相信 Host、Origin、X-Forwarded-For 或“部署在内网”的假设。
- 使用反向代理时只信任明确配置的 KnownProxies/KnownNetworks，并以可信转发链还原客户端地址；配置不完整时默认拒绝匿名初始化。
- 仅允许受控本机/部署环境；首位管理员创建成功后服务端立即关闭入口，重复请求即使来自 Loopback 也必须拒绝。
- 明确生产是否禁用匿名 Loopback Bootstrap；失败保留输入并返回结构化错误。

**实现要点（M2-03 ✅）**：`PlatformBootstrapController.Create` 先查 `PlatformBootstrap:AllowAnonymous`（默认 true）——false 时匿名 `POST` 立即 403 并提示改用部署配置；随后校验 `Connection.RemoteIpAddress.IsLoopback`（依赖 `Program.cs` 受控 `ForwardedHeaders`：仅消费配置内 `KnownProxies/KnownNetworks` 的 `X-Forwarded-*`，默认不消费，规避伪造客户端 IP）。`PlatformAdminBootstrapper.CreateAsync` 校验 用户名非空、口令≥8；交互式（携带 confirmPassword）还需口令一致与邮箱含 '@' 格式；配置路径（不传 confirmPassword）邮箱允许为空。`Status` 新增 `anonymousAllowed` 标志。`EnsureAsync` 部署配置路径不受匿名开关影响。重复初始化由 `CreateAsync` 幂等守卫抛 `InvalidOperationException` → 控制器返回 409（入口即关闭）。前端 `Login.razor` 初始化表单补齐 Email 字段、前后端校验、失败保留输入，并按 `anonymousAllowed=false` 隐藏表单改提示部署配置。新增 15 例测试（bootstrapper 校验 4 + 控制器匿名开关/Loopback/一次性关闭/状态标志 5，扩展既有 bootstrapper 用例至 10）全部通过；四端构建 0 error。

### M2-04 租户事务与生命周期 ✅

- 同一事务创建 Tenant、首位 TenantAdmin、密码、安全戳、默认设置、语言授权和默认语言；失败全回滚。
- 页面显示总数/启用/停用，默认列表，支持编码/名称搜索。
- 创建用弹窗/抽屉/步骤表单；支持编辑名称、语言、默认语言；启停二次确认并展示影响。

**实现要点（M2-04 ✅）**：`TenantManagementController.Create` 已在单一 `BeginTransactionAsync` 内依次落库 Tenant → 两个锁定 `TenantSetting`（localization:availableCultures / defaultCulture）→ `IIdentityService.CreateUserAsync`（首位 TenantAdmin + 安全戳）→ `SetPasswordAsync`（口令哈希 + 安全戳轮换）；任意一步失败即 `RollbackAsync` 并回冲突/BadRequest，且依赖同一注入 `SuperBIContext` 的 `IdentityService` 参与该环境事务，保证「全有或全无」。页面 `Tenants.razor` 已完整：统计（总数/已启用/已停用）、`SbListPage` 编码/名称搜索、创建/编辑弹窗（含语言授权与默认语言）、启停 `SbConfirm` 影响提示、设置弹窗。`TenantManagementControllerTests` 新增 2 例事务证据：`Create_WhenAdminCreationFails_RollsBackEntireTenantCreation`（FailingIdentityService 桩：管理员创建失败 → 返回 Conflict 且 Tenants/TenantSettings 均为 0 行，证明原子回滚）；`Create_Success_CreatesTenantAdminUserAndSettingsAtomically`（真实 IdentityService 经 SeedAsync 注入全局角色目录后：提交后租户 + 2 条默认设置 + 管理员 User（TenantAdmin 角色、pbkdf2 口令、非空安全戳）均落库，证明原子提交）。全量回归 643/643 通过、四端构建 0 error。

### M2-05 多租户成员关系（SB-P1-16） ✅

- 建立 UserTenant、切换授权、EffectiveTenantId 和前端租户切换。
- 合法成员可切换，非成员 403，全程审计。
- 用户租户切换与平台管理员代管必须是两套独立机制。

**实现要点（M2-05 ✅）**：用户—租户成员关系以 `UserTenant` 实体（含 `UserId/TenantId/IsDefault/CreatedAtUtc/CreatedByUserId`）承载，主租户（`User.TenantId`）恒为隐式成员、不落 `UserTenant`；可切换租户 = { 主租户 } ∪ { `UserTenant.TenantId` }。`ITenantMembershipService`/`TenantMembershipService` 提供 `AddMemberAsync`（幂等、主租户免记录）、`RemoveMemberAsync`（主租户不可移除）、`SetDefaultAsync`、`IsMemberAsync`、`GetMembershipsAsync`、`GetSwitchableTenantIdsAsync`、`ListAllAsync`（治理面：跨租户汇总全部显式成员关系，刻意 `IgnoreQueryFilters` 以绕过租户作用域过滤器）。切换采用「重签 JWT」机制（用户确认项）：`TenantMembershipController.Switch` 校验 `IsMemberAsync` → 加载主租户下用户行（安全戳）→ 以 `tid=目标、htid=主租户` 重签令牌并解析目标租户语言；`AuthMiddleware` 改为依 `htid`（主租户）做用户/安全戳查找，并新增 `htid` 声明与「生效租户是否启用」守卫，规避切换后误判 token 失效。`TokenService` 新增 `HomeTenantId`/`Htid` 贯通链路。前端：RCL 新增 `TenantSwitcher`（顶栏切换、主租户标记 `·`、切换后刷新会话态并 `Nav.NavigateTo(forceLoad)`），并完整实现 `TenantMembers.razor` 治理页（PlatformTenantManage 守卫，列出/添加/移除成员关系，camelCase 解析与默认/启用徽章）。新增 3 例测试（`TenantMembershipTests`：ListAllAsync 仅含显式成员、无权限 403、有权限 200），全量回归 **646/646** 通过、四端构建 0 error。

### M2-06 租户自注册（默认关闭骨架 ✅）

- 设计平台开关、审批、验证码/防滥用、编码占用、首位管理员设密和初始化事务。
- 未经确认默认关闭公网自注册（DEC-04：默认关闭，平台按环境开启）。
- **裁定结论（M2-06 收尾）**：按用户确认以「默认关闭」形式落地骨架——平台开关 + 受控注册端点（默认 off，不对外暴露），待裁决子特性（审批 / 验证码）以配置开关占位、开启即拒绝，避免不经治理评估对外开放公网。

**实现要点（M2-06 ✅）**：`SelfRegistrationOptions`（配置节 `SelfRegistration`，`Enabled` 默认 false；`ApprovalRequired`/`RequireCaptcha`/`AllowedEmailDomains`/`DefaultCulture`/`DefaultAvailableCultures`/`DefaultQuotaPolicyCode` 占位）由 `Program.cs` 经 `Configure<>` 绑定。`ISelfRegistrationService`/`SelfRegistrationService` 实现 `RegisterAsync`：开关关闭→`disabled`；待裁决特性开启→`feature_not_implemented`（拒绝，防不安全开放）；通过校验后以与 M2-04 一致的事务原子创建「租户 + 租户设置(localization) + 首位租户管理员(IdentityRoles.TenantAdmin) + 口令(pbkdf2)」，读取真实 `SecurityStamp` 后以 `tid=新租户、htid=新租户` 重签令牌（注册即登录），并写审计 `tenant.self-register`。`SelfRegistrationController` 暴露 `GET status`(匿名，仅回 `enabled`)、`POST register`(匿名，受开关守卫，映射 disabled→403 / feature_not_implemented→501 / conflict→409 / ok→200)、`GET config`(PlatformAdminManage 守卫，只读配置)。前端：RCL 新增 `SelfRegistration.razor` 公开注册页（BlankLayout，先查 status，关闭时显「未开放」，开放时表单校验后注册并自动登录跳转 `/ask`）、`SelfRegistrationAdmin.razor` 治理查看页（PlatformAdminManage 守卫，只读展示状态/域名白名单/语言，并说明开放方式与环境配置约束）；登录页新增「申请开通」入口，管理菜单新增「自助注册」项。新增 4 例测试（`SelfRegistrationTests`：关闭→disabled 无数据、审批开启→feature_not_implemented 无数据、开启→原子创建租户+管理员+设置且令牌非空、编码冲突→conflict），全量回归 **650/650** 通过、四端构建 0 error。


### M2-07 默认种子与演示数据 ✅

- 启动种子创建默认主题、平台语言、基础文本、角色、权限和配额策略，且全部幂等。
- 演示数据使用独立 Demo 安装器：可创建 demo 租户、管理员、数据源、元数据、语义和仪表盘。
- 生产默认不安装 demo；安装器支持预览、事务回滚和重复执行保护。

**实现要点（M2-07 ✅）**：默认主题种子 `ThemeSeedService`（`IThemeSeedService`）在启动序列第 4 步（配额之后）幂等写入 `Theme(TenantId=0, Key="default", DslJson=BuiltInThemes.DefaultDsl())`，补齐此前缺失的内置默认主题。演示数据采用**完全独立、按需触发**的 `DemoDataInstaller`（`IDemoDataInstaller`，DEC-05：不接入启动序列、生产默认不安装）：`PreviewAsync` 返回 8 项计划（Tenant/User/DataSource/MetadataTable/MetadataColumn×5/MetadataSemantic×5/BusinessEntity/Dashboard）并带重复安装保护；`InstallAsync` 在 `BeginTransactionAsync` 内事务原子创建「租户 + 2 条本地化租户设置 + 管理员(IdentityRoles.TenantAdmin, pbkdf2) + 数据源(MySQL) + 元数据表(sales_order) + 5 字段(各带 Manual 语义) + 业务实体 + 已发布仪表盘(ThemeKey=default, DSL 序列化)」，失败整体回滚，成功后写审计 `demo.seed`。`DemoDataController`(`api/demo-data`：`GET preview` / `POST install`) 受 `IdentityPermissions.PlatformAdminManage` 守卫（→403）。前端：RCL 扩展 `IApiClient`/`ApiClient`（`GetDemoDataPlanAsync`/`InstallDemoDataAsync` + DTO `DemoPlanItem`/`DemoInstallPlan`/`DemoInstallResult`）、新增 `DemoData.razor` 管理页（AuthGuard + PermissionGuard）、`NavMenuItems` 新增「演示数据」入口。新增 3 例测试（`DemoDataInstallerTests`：预览 8 项、事务原子安装幂等落库、重复安装返回 already_installed 无重复），全量回归 **653/653** 通过、四端构建 0 error。

---

## 7. M3：多语言产品化

### M3-G0 核心可用子集 ✅（2026-09-05 收尾，本地提交待推送）

M3-G0 是 M4/M7/M8 的最小前置，不等同于完成全部 i18n。它只包含：稳定资源键与加载机制、登录、Layout、NavMenu、共享按钮/验证/错误组件，以及租户语言范围和用户语言恢复。完成 M3-G0 后：

- M4、M7 的数据模型、API 和非文案页面工作可继续。
- M8 的布局、性能、无障碍和多端构建可并行。
- 各业务页面在自身里程碑中不得新增硬编码，并同步迁移本页面文案。
- 完整 M3-01~06 仍是最终发布门禁，但不再串行阻塞所有业务开发。

> **状态（2026-09-05）**：M3-G0 全部交付并验证；测试 661/661 全绿、四端构建 0 error、PlatformAdmin 治理不变量保留（`localization:*` 视为平台治理面）。本地提交待推送 `origin/master`。

**交付清单（G0-A ~ G0-H）：**
- ✅ **G0-A 权限码**：新增 `localization:view`/`localization:manage`（`IdentityPermissions`），并入 `IdentityCatalog.Permissions`（全局权限 32→34），`PlatformAdmin` 与 `TenantAdmin` 角色种子已含；`LocalizationController` 守卫由 `PlatformTenantManage`/`IdentityManage` 切换为 `LocalizationManage`/`LocalizationView`；`PermissionCodes.cs`、`NavMenuItems.cs` 同步；`IdentityServiceTests` 权限数量断言 32→34。
- ✅ **G0-B 用户语言偏好实体**：新增 `UserLanguagePreference(TenantId,UserId,Culture)`，唯一索引 `(TenantId,UserId)`，`Culture` 限长 16；迁移 `20260905060434_M3G0UserLangPref`。
- ✅ **G0-C 偏好服务**：`IUserLanguagePreferenceService`/`UserLanguagePreferenceService` 按租户 `localization:availableCultures`/`defaultCulture` 校验请求文化（越界回退 available[0]），upsert 并写审计 `user.language.set`。
- ✅ **G0-D 偏好控制器**：`UserPreferenceController`（`api/user/preferences/language` GET/PUT），匿名返回 401、越界文化回退但仍 200，返回服务端生效文化。
- ✅ **G0-E 前端偏好集成**：`IApiClient`/`ApiClient` 新增 `GetUserLanguageAsync`/`SetUserLanguageAsync`；`LocalizationService.InitializeAsync` 改为「服务端偏好 → localStorage → 租户默认」回退链，`SetCultureAsync` 同步持久化（仅登录用户）。
- ✅ **G0-F 共享语言切换器**：`LanguageSwitcher.razor`（仅当 `AvailableCultures>1` 显示，原生名）；登录与 `MainLayout` 内联 `<select>` 已替换为该组件。
- ✅ **G0-G 共享校验/错误组件**：`FieldError.razor`、`ValidationSummary.razor`（role=alert / validation-summary）。
- ✅ **G0-H 资源键注册表 + 五语言种子**：`ResourceKeys`（Common/Login/App/Document/Validation/Error/Empty 权威键集合，`All()` 反射枚举）；`LocalizationSeedService.defaults` 由 2 语言扩至 5（zh-CN/zh-TW/en-US/ja-JP/ko-KR），每语言 17 键；新增 `EnsureSeedAsync_Covers_Registered_ResourceKeys` 测试。

### M3-01 语言关系模型 ✅（2026-09-05 收尾，本地提交待推送）

- 建立 `TenantUiLanguage(TenantId, UiLanguageId, Enabled, SortOrder, IsDefault)` 关系（替代 `TenantSetting` 中 `localization:availableCultures/defaultCulture` JSON）。
- 每租户至少一种启用语言、只能一个默认语言，且默认值必须属于授权集合（事务内强制）。
- 停用平台语言前展示受影响租户并迁移其默认语言（含单语言租户回退到首个其他启用平台语言）。
- 现有 `localization:availableCultures/defaultCulture` JSON 数据在启动播种与租户创建/自助注册/演示安装时迁移到关系模型（`TenantSettingPolicy` 锁定键保留但进入休眠，向后兼容读仅在播种阶段使用）。

> **状态（2026-09-05）**：M3-01 全部交付并验证；测试 674/674 全绿（新增 `TenantLanguageServiceTests` 13 项）、四端构建 0 error。本地提交待推送 `origin/master`。

**交付清单（M3-01-A ~ M3-01-D）：**
- ✅ **M3-01-A 实体与迁移**：`TenantUiLanguage`（唯一索引 `(TenantId,UiLanguageId)`、索引 `(TenantId,IsDefault)`，级联删除租户、限制删除语言），迁移 `20260905064209_M3_01_TenantUiLanguage`。
- ✅ **M3-01-B 服务与约束**：`ITenantLanguageService`/`TenantLanguageService`/`TenantLanguageException`；`SetLanguagesAsync` 强制（≥1 启用、恰 1 默认、默认须启用、禁用平台语言不可启用）；`GetAvailableCulturesAsync`/`GetDefaultCultureAsync`/`GetLanguagesForTenantsAsync`（批量）；`DisablePlatformLanguageAsync` 迁移默认租户并停用所有租户侧该语言行。
- ✅ **M3-01-C 启动播种迁移**：`Program.cs` 启动序列在主题种子之后调用 `EnsureAllTenantsLanguagesAsync`（幂等，按 JSON 或平台默认 zh-CN 迁移）。
- ✅ **M3-01-D 消费者改走服务**：`AuthController`(登录/LoginOptions)、`TenantMembershipController`、`TenantManagementController`(List/Create/Update)、`LocalizationController.AllowedCultures`、`UserLanguagePreferenceService`、`SelfRegistrationService`、`DemoDataInstaller` 全部改经 `ITenantLanguageService`；`TenantSettingPolicy` 锁定键（含 `localization:*`）保持不变以兼容既有治理测试。

### M3-02 平台语言维护 ✅（2026-09-05 收尾，本地提交待推送）

- 平台管理员查看、添加、启停、排序语言；Culture 按 BCP 47 归一化并唯一。
- DisplayName、NativeName 必填；语言切换器优先显示 NativeName。
- 新语言可复制已有键集合，复制项标记“待翻译”。
- 平台视图只维护 TenantId=0 的目录和基线，不显示租户覆盖。

> **状态（2026-09-05）**：M3-02 全部交付并验证；测试 691/691 全绿（新增 `PlatformLanguageServiceTests` 13 项 + `LocalizationControllerAdminTests` 4 项）、后端与三端（Components/Web/Maui）构建 0 error。本地提交待推送 `origin/master`。

**交付清单（M3-02-A ~ M3-02-E）：**
- ✅ **M3-02-A 基础与迁移**：`LocaleContext.NormalizeCulture` 公开 BCP 47 归一化；`UiTextResource.IsTranslated` 列 + 迁移 `20260905072839_M3_02_UiTextResource_IsTranslated` + 种子置 `IsTranslated=true`。
- ✅ **M3-02-B 平台语言服务**：`IPlatformLanguageService`/`PlatformLanguageService`/`PlatformLanguageException`；列表（含停用与翻译进度统计）、按 Id 查看、新建（归一化+唯一性+必填名+复制键集合标记待翻译）、改名/排序、启用/停用（停用委托 `DisablePlatformLanguageAsync` 完成租户默认语言迁移）。
- ✅ **M3-02-C 控制器与 DI**：`LocalizationController` 新增 `admin/languages`、`admin/languages/{id}`、`languages/{id}`(PUT)、`languages/{id}/enabled`、`languages/reorder` 五个受 `localization:manage` 守卫端点；`Program.cs` 注册 `IPlatformLanguageService`；`PublicTexts`/`Texts` 投影 `IsTranslated`。
- ✅ **M3-02-D 前端接入**：`IApiClient`/`ApiClient` 新增 6 个方法 + DTO（`AdminLanguageView`/`AdminLanguageCreate`/`AdminLanguageUpdate`/`PublicLanguageView`）；`Localization.razor` 改用强类型目录（启停/排序/改名/待翻译展示）；`LanguageSwitcher` + `LocalizationService` 从 `public/languages` 动态获取 `NativeName`，移除硬编码 5 语言映射。
- ✅ **M3-02-E 测试与验证**：`PlatformLanguageServiceTests`（13 项：归一化、唯一性、必填、复制待翻译、列表/查看统计、启停委派、排序）、`LocalizationControllerAdminTests`（4 项：声明守卫、包含停用、复制待翻译、非法 Culture 400）；全量回归 691/691 通过。

### M3-03 租户文本覆盖 ✅（2026-09-05 收尾，本地提交待推送）

- 租户管理员只看到被授权语言（核心不变量由 `Languages` 端点经 `ITenantLanguageService.GetAvailableCulturesAsync` 按 `TenantUiLanguage` 关系过滤，已由 M3-01 落地并测试锁定）。
- 同时展示平台基线、租户覆盖、最终值和“平台/继承/专属”来源（`LocalizationController.Texts` 合并平台(tenantId=0)与租户行；前端非平台视图展示 `platformValue`/`value`/`isOverridden`）。
- 删除覆盖即恢复继承，不删除平台基线（`DELETE texts/{culture}/{key}/override` 仅删租户行）；平台基线更新后未覆盖租户自动继承。
- 保存后立即重载客户端资源（网格刷新，不要求重新登录）。

> **状态（2026-09-05）**：M3-03 核心能力由 M3-G0/M3-01 已交付并本次补测试锁定；测试 693/693 全绿、四端构建 0 error。本地提交待推送 `origin/master`。

**交付清单（M3-03-A ~ M3-03-B）：**
- ✅ **M3-03-A 租户授权语言过滤**：`LocalizationController.Languages` 对租户用户仅返回其 `TenantUiLanguage` 授权且启用的文化（平台层启用的非授权语言不出现）；新增 `LocalizationControllerTenantTests`（租户仅见授权语言 / 平台见全部启用语言）。
- ✅ **M3-03-B 覆盖合并与生命周期**：`Texts` 合并平台基线 + 租户覆盖并标注来源；`SaveText` 写入租户覆盖并置 `IsTranslated=true`；`ResetText` 删除租户覆盖恢复继承。前端 `Localization.razor` 非平台分支展示平台默认/租户显示值/“租户专属”徽标与“恢复继承”操作。

### M3-04 资源键治理 ✅（2026-09-05 收尾，本地提交待推送）

> **状态（2026-09-05）**：M3-04 治理基础闭环已交付并验证；测试 698/698 全绿（新增 `LocalizationControllerTextsTests` 3 项 + `UiTextResourceQueryFilterTests` 2 项）、后端与三端（Components/Web/Maui）构建 0 error。本地提交待推送 `origin/master`。

**交付清单（M3-04-A ~ M3-04-D）：**
- ✅ **M3-04-A 资源键目录元数据**：`ResourceKeys` 新增 `Nav`（Dashboard/Ask/DataSources/Admin）、`Accessibility`（SkipToContent）、`Theme`（Light/Dark）分组；新增 `ResourceKeyMeta` 记录（Module/Page/DefaultValue/Deprecated）与 `Catalog` 字典，为后续 CI 键空间扫描提供权威注册表。
- ✅ **M3-04-B 键覆盖与种子补齐**：`LocalizationSeedService` 在上述 7 个新键为 zh-CN/zh-TW/en-US/ja-JP/ko-KR 五语言补齐默认译文，与 `Catalog.DefaultValue` 一致；种子完整性测试 `EnsureSeedAsync_Covers_Registered_ResourceKeys` 仍全绿。
- ✅ **M3-04-C 占位符一致性校验**：新增 `LocalizationPlaceholderValidator`（提取 `{n}` 占位符并比对基线与译文一致性）；`LocalizationController.SaveText` 在租户覆盖（targetTenant>0）写入前强制校验，不一致返回 400（中文错误文案），平台基线（tenantId=0）免于校验。
- ✅ **M3-04-D UiTextResource 租户过滤纵深防御**：`SuperBIContext` 为 `UiTextResource` 增加全局 QueryFilter（`!_tenantFilterEnabled || TenantId==_scopedTenantId || TenantId==0`），与 `SemanticLabel`/`Dashboard` 同模式；`Texts`/`SaveText`/`ResetText` 经 `ApplyTenantScope(targetTenant)` 显式启用作用域（传 0 即 no-op，保留平台基线视图）。

**范围说明（递延至 M3-05/06 或后续治理迭代）：** PlatformStrings/UiTextResources/前端 fallback 键空间合并、CI 硬编码/未知键/缺失译文/废弃键扫描，以及 `localization:view/manage` 显式权限码与角色种子/菜单守卫统一，本轮未纳入，由 M3-05（全页面接入）与后续迭代承接。

### M3-05 全页面接入 ✅

- 登录语言切换器改为由 `/api/localization/public/languages` 动态驱动（`Login._loginCultures` 不再硬编码 zh-CN/en-US 初值）；AuthController 文化解析本就由 DB + `TenantLanguageService` 驱动，无需改动。
- 登录、Layout、NavMenu 与核心外壳（错误边界、菜单、主题切换、无障碍跳转）已分批接入统一 L10n；内容密集页（Ask/Dashboards/Apps/DataSources/Admin/*）留待后续批次。
- 按钮、标签、空状态、错误提示、验证消息均进入资源键；RCL 自持 `Keys.cs` 镜像（避免引用 EF 重型后端），离线回退 `Keys.Defaults`（zh-CN/en-US）修复此前小写键跨语言回退错误。
- 后端错误码映射到本地化文本（ApiError），不暴露内部异常堆栈。

交付清单（M3-05-A~D）：
- M3-05-A 资源键镜像：RCL `Keys.cs`（嵌套常量 + `Defaults` zh-CN/en-US），与后端 `ResourceKeys` 字符串值一一对应。
- M3-05-B 前端取数重构：`LocalizationService.T(key)` 优先运行时字典，回退 `Keys.Defaults`，再回退页面 fallback；登录语言列表改为 API 动态驱动。
- M3-05-C 资源键登记与种子：后端 `ResourceKeys` 增补 Common/Login/Nav/Error/Theme 全量键 + `Catalog` 元数据；`LocalizationSeedService` 重构为「键驱动」种子（zh-CN 取内置 `ZhCnDefaults`、en-US 取 `Catalog.DefaultValue`、zh-TW/ja/KO 保留母语基线并回退 en-US），覆盖 `ResourceKeys.All()` 全部键，通过 `EnsureSeedAsync_Covers_Registered_ResourceKeys`。
- M3-05-D 后端错误本地化基础：`LocalizationController` 4 处裸 `BadRequest(ex.Message)` 与 3 处内联中文改为返回 `ApiError`（稳定 code `Error.Localization.*` + 服务端中文文案，不泄漏内部细节）；新增后端测试验证 `ApiError` 形状与 code 稳定性。
- M3-05-E 防漂移护栏：新增 `ResourceKeyRegistryTests` 六例，校验「后端注册表 ↔ Catalog 元数据 ↔ zh-CN 基线 ↔ RCL `Keys`/`Defaults`」四处严格一致；任一处键集合或中英双语文案漂移直接变红（上一轮种子脱节正因缺此类护栏而潜伏）。
- M3-05-F 共享反馈组件本地化：`ErrorState`/`LoadingState`/`SbEmptyState`/`SbAlert`/`PageHead` 接入统一 L10n（错误码、追踪 ID、重试、关闭、加载中、暂无数据等），全部保留原中文 fallback，无回归。
- M3-05-G 页头稳定键：`PageHead` 新增稳定 `Key` 参数解析 `Page.Title.{Key}`（旧 `Page.Title.{中文标题}` 约定会退化成中文键、必然查不到，作为未迁移页回退）；登记 `Page.Title.*` 13 个键（五处同步：后端常量 + `Catalog`、zh-CN `ZhCnDefaults`、RCL `Keys`/`Defaults`），并迁移工作台/管理后台 13 个页面 `<PageHead Key=...>`。
- M3-05-H 递延内容页页头接入：登记 `Page.Title.*` + `Page.Desc.*` 共 18 个键（BusinessModel/BusinessModelEntityDetail/SemanticLabelDetail/ComponentGallery/ThemeEditor/DataSources/ModelAccounts/MetadataEntityDetail/DataSource），五处同步；迁移 8 个静态标题内容页 `<PageHead Key=...>`（动态标题详情页 DataSourceDetail/AgentPlanDetail/AppDetail/DashboardDetail 本批不迁）。
- M3-05-I 共享动词与样例页内文案：`_Imports.razor` 全局注入 `LocalizationService L10n`（后续页面批次免逐页注入）；新增 `Action.*`（New/Create/Save/Cancel/Refresh/Delete/Edit/Search/Close/Reset）与 `Page.ThemeEditor.*` 键集（五处同步）；接入 ThemeEditor（浅色/深色/重置/保存主题/实时预览/调色板）、BusinessModel（新建实体）、DataSources（刷新/取消/保存并接入）。
- M3-05-J 重内容页正文批次（BusinessModel / DataSources 家族）：新增后端 `ResourceKeys.Content` 扁平常量类（约 101 键）并在 `Catalog`/`ZhCnDefaults` 与 RCL `Keys.Content`/`Keys.Defaults` 五处严格同步；将以下四个页面的**静态正文、表格列、状态标签、空状态、模态框字段与主要 Toast** 全部改为 `L10n.T(Keys.Content.X, fallback)`：`BusinessModel`（统计块/页签/搜索框/空状态/无匹配/详情按钮/编辑器未就绪提示）、`BusinessModelEntityDetail`（加载/面板/空状态/返回）、`DataSources`（指标卡/连接资产面板/搜索/加载/空状态/**表格列 ID·名称·类型·状态·元数据表·字段**/**状态标签 运行中·已停用**/租户专属/管理连接/接入模态框/连接器描述/字段/保存并接入/各 Toast）、`DataSourceDetail`（元数据·访问授权·行级安全三页签全部标签、表头、状态文案、授权/RLS 空状态与增删改 Toast）。`ResourceKeyRegistryTests` 六例护栏全绿，验证新增键在五处零漂移。

- M3-05-K 重内容页正文批次（续：剩余重型内容页）：在 M3-05-J 的 `Content` 扁平常量类基础上，新增 93 个键（单级扁平，页前缀 ModelAccounts / SemanticLabel / ComponentGallery / ThemeEditor 补充 / MetadataEntity），五处严格同步（后端常量 + `Catalog` + `ZhCnDefaults`、RCL `Keys.Content` + `Keys.Defaults`）；将以下五个页面的页内正文、表格列、状态标签、空状态、模态框字段与 Toast 全部改为 `L10n.T(Keys.Content.X, fallback)`：`ModelAccounts`（绑定/未绑定徽标、默认徽标、设为默认、绑定面板、模型/API Key/备注字段与占位符、保存绑定、设为默认与提交绑定两个 Toast）、`SemanticLabelDetail`（加载态、详情/标签信息面板、未找到空状态、返回列表）、`ComponentGallery`（分区标签、按钮/徽标/表单/进度/分段/告警/统计/表格列/功能卡/页签/弹窗/确认/Toast/Guard 巡展文案）、`ThemeEditor` 补充（`示例指标`、主要按钮、次要、已发布·草稿徽标、保存 Toast）、`MetadataEntityDetail`（加载态、引言、未找到提示）。`ResourceKeyRegistryTests` 六例护栏全绿，验证新增键五处零漂移；四端构建 0 错误，全量回归 708/708 通过。

- M3-05-L 分析域末批（Ask/Dashboards/Apps/AppDetail + AskTurnCard）全量 L10n 与 ApiError 友好提示层：在 M3-05-J/K 的 `Content` 扁平常量类基础上新增 170 个键（页前缀 Ask / Dashboards / Apps / AppDetail / AskTurn，单级扁平，五处严格同步：后端常量 + `Catalog` + `ZhCnDefaults`、RCL `Keys.Content` + `Keys.Defaults`），并补登遗漏键 `DashboardsDeleteFailed`（五处）；将四个详情/分析页的页内正文、表格列、状态标签、空状态、模态字段与 Toast 全部改为 `L10n.T(Keys.Content.X, fallback)`，共享组件 `AskTurnCard` 全量接入（`错误码`/`追踪 ID` 改用 `Keys.Error.CodeLabel`/`TraceIdLabel`，柱状/折线/饼图等指令识别逻辑保留中文硬编码不本地化）；友好提示层按 `err is null ? L10n.T(具体键,...) : L10n.Friendly(code, err)` 全量透传后端 `ApiError.Code`（4 元组 `Code` 字段），取代原 `err ?? "中文兜底"`。`ResourceKeyRegistryTests` 六例护栏全绿（含新增键），四端（后端 net10.0 / RCL / Web / Maui-Windows）构建 0 错误，全量回归 708/708 通过。
批次范围说明（本末批已完成）：Ask / Dashboards / Apps / AppDetail 详情页正文与共享组件 `AskTurnCard` 的全量 L10n，以及前端按 `L10n.T("Error."+code, serverMessage)` 经 `IApiClient`/`ApiClient` 4 元组 `Code` 字段透传消费 `ApiError` 的友好提示层（全量透传），已于本批次（M3-05-L）实现。ModelAccounts / SemanticLabelDetail / ComponentGallery / ThemeEditor / MetadataEntityDetail 的页内正文与表格列已在 M3-05-K 全量本地化（BusinessModel/DataSources 家族见 M3-05-J）。

- M3-05-M 残余硬编码中文扫描与本地化（增量批次）：扫描 `SuperBuilder_AI.Components` 全部 razor，去噪（剔除 `@* *@`/`//`/`/// <summary>` 注释）后共 **614 处**用户可见中文串，分布于 53 文件；其中 **Admin 区域约 355 行（12 文件，最大缺口）** 为最高优先级。采用「逐文件批次」策略，键名遵循既有扁平 camelCase 约定（如 `AdminIdentityTitle`、`CommonRefresh`），通用 CRUD 文案（取消/保存/创建/刷新/名称/描述/角色/权限…）复用 `Common*` 键以压低键数。Batch 1 完成 `Admin/Identity.razor`：新增 61 个键（44 `AdminIdentity*` + 17 `Common*`，五处严格同步：后端常量 + `Catalog` + `ZhCnDefaults`、RCL `Keys.Content` + `Keys.Defaults`），页内正文、表单字段与占位符、模态/确认框、统计磁贴、空状态与全部 `@code` 中 Toast/错误提示改为 `L10n.T(...)`/`string.Format(L10n.T(...))`；页标题经既有 `Page.Title.Identity` 机制本地化，描述经 `AdminIdentityDesc` 键本地化。RCL 构建 0 错误，`ResourceKeyRegistryTests` 6/6，全量回归 708/708 通过。后续批次顺序：PlatformAdmins / Tenants / Localization / TenantMembers / DemoData / PlatformAdminScopes / SelfRegistrationAdmin / Audit / SystemStatus / Quota / Themes，再续 Analysis 余页（SemanticLabels / Agent / AgentPlanDetail / DashboardDetail）与 Account/Platform 页面（SelfRegistration / DataSources / ModelAccounts）。 Batch 2 完成 `Admin/PlatformAdmins.razor`：新增 46 个 `AdminPlatformAdmins*` 键（五处严格同步：后端常量 + `Catalog` + `ZhCnDefaults`、RCL `Keys.Content` + `Keys.Defaults`），用户可见文本（页描述经 `AdminPlatformAdminsDesc`、页签/面板标题、空状态、模态/确认框、表格列、行操作与全部 `@code` 中 Toast/错误提示）全部接入 `L10n.T`/`string.Format`，审计操作类型标签（`platform.admin.*`→`AdminPlatformAdminsAct*`）亦本地化；`ActionLabel` 由 `static` 改为实例方法以接入 `L10n`。RCL 0 错误、护栏 6/6、全量回归 708/708、四端构建 0 错误均通过。 Batch 3 完成 `Admin/Tenants.razor`：新增 67 个 `AdminTenants*` 键（五处严格同步：后端常量 + `Catalog` + `ZhCnDefaults`、RCL `Keys.Content` + `Keys.Defaults`，`Catalog.Page` 统一为 `Tenants`）；用户可见文本（页描述经 `AdminTenantsDesc`、列表标题/搜索框/空状态/加载态、新建/编辑/设置三套模态框字段与占位符/提示、统计磁贴、行操作、SbConfirm 标题/正文/确认按钮、表格列与全部 `@code` 中 Toast/错误提示）全部接入 `L10n.T`/`string.Format`，通用 CRUD 复用 `CommonCancel`/`CommonSave`；`Page.Title.Tenants` 已注册故页标题自动本地化，SbConfirm 标题/正文分别用 `AdminTenantsEnableTitle`/`DisableTitle` 与带 `{0}{1}` 插值的 `AdminTenantsConfirmMsg`；`租户编码已存在`/`配置 Key 已存在` 等 409 冲突与通用 `保存失败` 分别落到专属键与复用键。RCL 0 错误、护栏 6/6、全量回归 708/708、四端构建 0 错误均通过。
 Batch 4 完成 `Admin/Localization.razor`：新增 51 个 `AdminLocalization*` 键（五处严格同步：后端常量 + `Catalog` + `ZhCnDefaults`、RCL `Keys.Content` + `Keys.Defaults`，`Catalog.Page` 统一为 `Localization`）；用户可见文本（页描述经 `AdminLocalizationDesc`、语言目录侧栏/待翻译/已停用芯片与上下移/编辑/启停图标 title、添加语言按钮、系统文本区说明/徽章/加载态、文本资源行的平台默认/平台文本/租户显示文本/租户专属/恢复继承/基线/已继承、添加/编辑语言与新增文本键三套模态框字段与占位符、以及全部 `@code` 中 Toast/错误提示）全部接入 `L10n.T`/`string.Format`；通用 取消/保存 复用 `CommonCancel`/`CommonSave`；`Page.Title.Localization` 已注册故页标题自动本地化，`租户专属` 等全角引号在五处均正确保留。`TEXT RESOURCE MATRIX` 为英文装饰 eyebrow，维持原样。RCL 0 错误、护栏 6/6、全量回归 708/708、四端构建 0 错误均通过。
 Batch 5 完成 `Admin/TenantMembers.razor`：新增 39 个 `AdminTenantMembers*` 键（五处严格同步：后端常量 + `Catalog` + `ZhCnDefaults`、RCL `Keys.Content` + `Keys.Defaults`，`Catalog.Page` 统一为 `TenantMembers`）；用户可见文本（页描述经 `AdminTenantMembersDesc`、列表标题/搜索框/空状态/加载态、添加成员按钮、三个统计磁贴、行操作、添加成员模态框字段与占位符/提示、SbConfirm 标题/正文/确认按钮、表格列与单元格状态徽标，以及全部 `@code` 中校验错误与 Toast 提示）全部接入 `L10n.T`/`string.Format`，通用 取消/添加 复用 `CommonCancel`/`CommonAdd`；`Page.Title.TenantMembers` 已注册故页标题自动本地化，SbConfirm 正文用带 `{0}{1}{2}` 插值的 `AdminTenantMembersRemoveMsg`。RCL 0 错误、护栏 6/6、全量回归 708/708、四端构建 0 错误均通过。

- M3-05-M **L1 层（共享组件默认值，ROI 最高）**：由「逐文件批次」改为「按密度分层」策略后的第一批。范围 = `Shared/UI`、`Shared/Guard`、`Shared/BI`、`Shared/Analysis`、`Layout` 下 13 个共享组件，新增 **31 个 `Shared*` 键**（五处严格同步：后端常量 + `Catalog` + `ZhCnDefaults`、RCL `Keys.Content` + `Keys.Defaults`，`Catalog.Page` 统一为 `Shared`）。**核心技术陷阱**：`[Parameter] public string Title { get; set; } = "确认操作";` 属字段初始化器，无法引用实例属性 `L10n`（CS0236，与 `AppDetail._title` 同源）；统一采用「默认值改 `null` + 类型改 `string?` + 渲染处 `?? L10n.T(...)` 兜底」模式化解，涉及 SbConfirm（Title/ConfirmText/CancelText）、SbDataTable（EmptyTitle）、SbListPage（SearchPlaceholder/EmptyTitle）、SbSearch（Placeholder）、PermissionGuard（DenyText）、DataSourcePicker（Label）6 个组件。另发现 `SbListPage` 会把未解析的 `EmptyTitle` 透传给 `SbDataTable`，故两层均需兜底。覆盖文案：确认框标题/确认/取消（`SharedConfirmTitle`/`SharedConfirm`/复用 `CommonCancel`）、加载中/操作列/暂无数据/刷新/搜索占位符/关闭/上一页/下一页/分页摘要（`SharedPageSummary` 带 `{0}{1}{2}` 插值）/搜索/清空、租户切换器（aria-label/当前租户标题/切换失败）、登录守卫（校验会话/需要登录/前往登录）、权限守卫（无访问权限/返回首页）、数据源标签、结果表空态、SQL 查看器（查看生成的 SQL/复制 SQL/已复制/复制失败）、面包屑 aria-label。**保留项**：`AskTurnCard` L231-238 的 `cmd.Contains("柱状")` 等中文指令识别属后端语义约定，非 UI 文案，不做本地化。RCL 0 错误、护栏 6/6、全量回归 708/708、四端构建 0 错误均通过。

- M3-05-M **L2 层（低密度页面）**：处理残余 ≤7 行的页面共 11 个，新增 **32 个键**（五处严格同步，`Catalog.Page` 按页面分设：Errors/Quota/Themes/ModelAccounts/DataSourceDetail/Login/Dashboards/Apps）。**关键收益：先查键再接线，避免重复登记**——`DataSourcesConnSqlServer`/`Mysql`/`Postgres`/`Oracle`/`Clickhouse`/`Mongodb` 六个连接器描述键的 zh 默认值正是页面硬编码文案，此前为「已登记未接线」，本批**零新增键直接接线覆盖 6 行**。覆盖范围：错误三页（403/404/500 的标题·描述·返回工作台·返回上一页·去提问·重新加载，`ErrBackToWorkbench` 三页复用）、`Admin/Quota`（页描述·加载态·面板标题×2·空状态）、`Admin/Themes`（页描述·面板标题·空状态·打开编辑器）、`ModelAccounts`（5 条模型描述）、`DataSourceDetail`（页头无 `Key` 参数故须显式本地化，`DataSourceDetailTitle` 带 `{0}` 插值）、`Login`（口令说明长文本·Bootstrap 显示名）、`Dashboards`/`Apps`（三处 `SbModal` 标题）。**识别并剔除的误报（重要）**：① 带 `Key` 参数且 `Page.Title.*`/`Page.Desc.*` 已注册的 `PageHead`，其 `Title`/`Desc` 字面量是**回退值**，PageHead 内部按键解析，外部再包 `L10n.T` 属重复——据此剔除 BusinessModel/ComponentGallery/SemanticLabelDetail/BusinessModelEntityDetail/MetadataEntityDetail/DataSources/ModelAccounts/PlatformAdmins 共 8 处；② `AppDetail._title` 已在 `OnInitialized` 用 `AppDetailTitleDefault` 覆盖，字段初始值属已处理；③ `Ask.razor:337` 的 `name = "结果"` 是 widget 数据定义而非 UI 文案。**字段初始化器陷阱的新变体**：`_models`(`ModelAccounts`)、`_connectors`(`DataSources`) 为集合字段初始化器，`BootstrapDisplayName`(`Login`) 参与 `@bind` 双向绑定——三者均**不可**改为只读计算属性（`_models` 在 `SetDefault` 中有写入，改属性会丢修改），统一采用「字段声明置空 + `OnInitialized` 内填充」化解。RCL 0 错误、护栏 6/6、全量回归 708/708、四端构建 0 错误均通过。

- M3-05-M **L3-A1 层（高密度 Analysis 页·第一批）**：L3 为残余裸中文最多的 13 页（实测 `SuperBuilder_AI.Components` razor 去噪后仍约 1121 处裸中文）。本批处理 Analysis 域 `Agent.razor` + `DashboardDetail.razor`，新增 **46 个键**（五处严格同步：后端常量 + `Catalog` + `ZhCnDefaults`、RCL `Keys.Content` + `Keys.Defaults`，`Catalog.Page` 分设 `Agent`/`Dashboards`）。`Agent.razor`：页头标题/描述/搜索占位符/空状态/加载态、新建按钮、两个统计磁贴、行操作（详情/运行/删除）、删除确认框（标题/正文/确认）、可用工具面板与空状态、表格三列（名称/描述/状态）、状态列 `草稿` 兜底，以及全部 `@code` 中 Toast/错误提示接入 `L10n.T`/`string.Format`；**修复 CS0120 同源陷阱**：`AgentColumns` 原为 `static` 属性含中文列名（prerender 访问未就绪实例报错），改为实例属性后接 `L10n`。`DashboardDetail.razor`：页头描述/返回列表、概览·基本信息·组件·数据 页签与面板标题、空状态/加载态、类型·标识插值（`DashboardDetailTypeAndId` 带 `{0}{1}`）、行数统计（`DashboardDetailRowCount` 带 `{0}`）、渲染中/执行渲染、默认标题；**CS0236 字段初始化器陷阱**：`_title` 原字段初始值引用实例 `L10n`，改「字段置空 + `OnInitialized` 内 `L10n.T` 赋值」化解。RCL 0 错误、护栏 6/6、全量回归 708/708、四端（后端/RCL/Web/Maui-Windows）构建 0 错误均通过。L3 剩余：L3-A2（SemanticLabels/AgentPlanDetail）、L3-B（Admin 五页）、L3-C（Account/Home/Design 四页）。

- M3-05-M **L3-A2 层（高密度 Analysis 页·第二批）**：处理 Analysis 域 `SemanticLabels.razor` + `AgentPlanDetail.razor`，新增 **58 个键**（五处严格同步：后端常量 + `Catalog` + `ZhCnDefaults`、RCL `Keys.Content` + `Keys.Defaults`，`Catalog.Page` 分设 `SemanticLabels`/`AgentPlans`；复用 `CommonRefresh`/`CommonCancel`/`CommonSave`）。`SemanticLabels.razor`：页头标题/描述/搜索占位符/空状态/加载态、新建按钮（Actions 与 EmptyActions 共用 `SemanticLabelsCreateBtn`）、三个统计磁贴（标签总数/已启用/已停用及其 Sub）、行操作详情、新建模态框标题与字段（概念类型·概念 ID·区域 Culture·标签种类·标签值·来源及各自占位符）、概念类型与标签种类两组 `<select>` 选项文本、取消/保存（复用 `CommonCancel`/`CommonSave`），以及全部 `@code` 中 Toast/校验错误提示（`SemanticLabelsOpenNoId`/`CidRequired`/`ValueRequired`/`CidNumeric`/`SaveFailed`/`Saved`）。`AgentPlanDetail.razor`：页头描述/返回列表/刷新（复用 `CommonRefresh`）/删除、三个页签（概览·步骤·原始数据）与面板标题、步骤空状态、删除确认框（标题/正文用带 `{0}` 插值的 `AgentPlanDetailConfirmMessage`/确认按钮复用 `AgentPlanDetailDelete`）、加载失败/删除失败/步骤序号（`AgentPlanDetailStepN`/`LoadFailed`/`DeleteFailed` 均带 `{0}`）、默认标题（`AgentPlanDetailDefaultTitle`）经 `OnInitialized` 内 `L10n.T` 赋值；**修复 CS0120 同源陷阱**：`ExtractSteps` 原为 `static` 方法，其内联 `Add` 函数引用实例 `L10n` 报错，去掉 `static` 改为实例方法化解。**路径认知修正（重要）**：本批实测 RCL 真正末键为 `DashboardDetailDefaultTitle`（非 `AppsEditDslTitle`），五处插入锚点统一改用该末键，保证键块落在各注册表末尾。RCL 0 错误、护栏 6/6、四端（后端/RCL/Web/Maui-Windows）构建 0 错误均通过。L3 剩余：L3-B（Admin 五页，B1 已完成 SystemStatus/DemoData，B2 待办 PlatformAdminScopes/SelfRegistrationAdmin/Audit）、L3-C（Account/Home/Design 四页：SelfRegistration/Profile/Home/ThemeEditor）。

- M3-05-M **L3-B1 层（Admin 页·第一批：SystemStatus / DemoData）**：处理 Admin 域 `SystemStatus.razor` + `DemoData.razor`，新增 **35 个键**（五处严格同步：后端常量 + `Catalog` + `ZhCnDefaults`、RCL `Keys.Content` + `Keys.Defaults`，`Catalog.Page` 分设 `SystemStatus`/`DemoData`/`Shared`；复用 `CommonRefresh`/`CommonCancel`/`CommonLoading`）。`SystemStatus.razor`：`PageHead` 带 `Key="SystemStatus"`（Title 由 `Page.Title.*` 机制接管，仅 `Desc` 经 `L10n.T` 接线）、刷新按钮（刷新中…/复用 `CommonRefresh`）、三磁贴 Label+Sub、两页签标题、健康检查与运行时指标无响应兜底（复用 `SystemStatusNoResponse`）。`DemoData.razor`：页描述、`LoadingState` 加载态（复用 `CommonLoading`）、安装状态面板标题与三列表头、将创建内容面板与表头（类型/数量/说明）、已安装提示、安装中加载态、确认安装弹窗文本与按钮（确认安装/复用 `CommonCancel`/安装演示数据）、完成提示与结果列表（租户/管理员/初始口令及其注）、安装失败前缀、状态徽章，以及 `@code` 中两处 `_error` 兜底。**关键修正（重要）**：`StatusBadge` 返回 `MarkupString`，徽章串为 C# 字符串字面量且外层引号 `"` 属 C# 串定界符不在匹配串内；初版替换导致两端多出 `""` 属非法 C#，修正为「旧串包含外层引号、新串为拼接表达式不带额外外层引号」后通过，并重跑前 `git checkout` 还原避免半接线状态。RCL 0 错误、护栏 6/6、全量回归 708/708、四端（后端/RCL/Web/Maui-Windows）构建 0 错误均通过。L3 剩余：L3-B2（PlatformAdminScopes/SelfRegistrationAdmin/Audit）、L3-C（SelfRegistration/Profile/Home/ThemeEditor）。

- M3-05-M **L3-B2 层（Admin 页·第二批：PlatformAdminScopes / SelfRegistrationAdmin / Audit）**：处理 Admin 域三页，新增 **53 个键**（五处严格同步；复用 CommonRefresh/Cancel/Save/Loading/Username/DisplayName/Email，新增 CommonYes/CommonNo 通用键）。`PlatformAdminScopes.razor`：页描述（PageHead Key=PlatformAdminScopes 仅 Desc 接线）、刷新（复用 CommonRefresh）、面板标题、空状态、范围编辑弹窗标题（`ModalTitleUser` 带 `{0}` 插值）、加载范围、管理全部租户（默认）、仅选中提示、无租户权限提示、三列（复用 Common*/PlatformAdminScopesColScope）、设置范围按钮、保存失败（带 `{0}`）、状态摘要（`全部租户`/`{0} 个租户`）与两条 Toast（带 `{0}`/`{1}`）。`SelfRegistrationAdmin.razor`：页描述、加载态（复用 CommonLoading）、当前状态面板与六列表头、配置说明（`配置说明` 标题 + 段落拆为 ConfigNote1/2/3 三段以保留 `<code>` 样式）、无法加载配置兜底、状态/是否徽章（`StatusBadge`/`YesNo` 返回 `MarkupString`，以 C# 字符串拼接 `+ L10n.T` 接线，`是/否` 复用新建 CommonYes/CommonNo）。`Audit.razor`：`SbListPage` 无 Key 故标题/描述/搜索占位符/空状态/加载态全接线、导出 CSV、三过滤占位符（操作/资源/操作人）、重置、三统计磁贴（Label+Sub）、无记录/已导出 Toast（带 `{0}`）。**关键修正（重要）**：① 配置说明段落跨 3 行，初版单行长串匹配 0 命中，改用正则 `[\s\S]*?` 跨行匹配；② 统计磁贴 `SbStatTile` 行含 `Value="@_rows.Count"` 等段，旧串须含该段；③ 两处英文值含内层双引号（`"Platform Admins"`/`"all tenants"`）破坏 C# 字符串，改为单引号后重新生成五处注册表（先 revert 避免误伤既有 `Nav.PlatformAdmins` 等条目的双引号字符串）。RCL 0 错误、护栏 6/6、全量回归 708/708、四端构建 0 错误均通过。

- M3-05-M **L3-C1 层（Account 页·SelfRegistration / Profile）**：本地化 Account 域两页用户可见文案，五处注册表严格同步（后端常量 + Catalog + ZhCnDefaults、RCL `Keys.Content` + `Keys.Defaults`），护栏 6/6 全绿。
  - SelfRegistration：注册英雄区三标签（多租户/低代码/企业级 SaaS，保留 "AI Native BI" 品牌英文）、检查中/通道关闭/已登录/申请开通/创建租户等全量文案、六表单标签与占位符（租户编码/名称/管理员用户名/邮箱/口令/确认口令）、返回登录/进入工作台/已有账户链接、以及全部 `@code` 中校验与注册失败 Toast（`{0}{1}` 插值用于已登录态）。
  - Profile：页头 `Key="Profile"` 的 `Title` 已由 `Page.Title.Profile` 机制接管（字面量为回退值）；补登记 `Page.Desc.Profile`（五处同步）使 `Desc` 回退值合法可种子化；账号信息/界面偏好两 `SbPanel`、权限标签、无显式权限徽章、主题字段与提示、深色/浅色分段文本、账号信息键值对（用户名复用 `CommonUsername`、用户/租户 ID、登录状态、已登录/未登录）全部接入 `L10n.T`。
  - 新增 42 个键（27 `SelfRegistration*` + 14 `Profile*` + 1 `Page.Desc.Profile`），复用 `CommonUsername`；`DescProfile` 在 `LocalizationSeedService` 第 228 行单行多条目区就地追加。
  - 接线陷阱：标签文本「管理员用户名/管理员口令」同时是 `@code` 错误提示文案的前缀子串，改用「仅替换首次出现」消解；「已有账户？返回登录」实际 `<a>` 无 `class` 属性，须按真实标记匹配。
  - RCL 0 错误 / 护栏 6/6 / 全量回归 708/708 / 四端（后端/RCL/Web/Maui-Windows）构建 0 错误均通过。L3 剩余：L3-C2（Home/ThemeEditor）。

- M3-05-M **L3-C2 层（Home / ThemeEditor）**：本地化 Home 工作台首页与 ThemeEditor 主题编辑器用户可见文案，五处注册表严格同步（后端常量 + Catalog + ZhCnDefaults、RCL `Keys.Content` + `Keys.Defaults`），护栏 6/6 全绿。
  - Home：开始提问按钮、四个统计磁贴（仪表盘/应用/语义标签/智能体计划，各 Label+Sub）、四张功能卡片标题与描述（Ask BI 智能问数/仪表盘/应用工厂/数据源与授权）、快速开始面板标题、四个步骤 `<li>`（连接数据源/定义语义标签/创建仪表盘/邀请成员）全部接入 `L10n.T`；`PageHead` 带 `Key="Home"`，`Title` 已由 `Page.Title.Home` 接管，补登记 `Page.Desc.Home`（五处同步）使 `Desc` 回退值合法可种子化。
  - ThemeEditor：仅余 2 处裸中文——保存主题按钮「主题」后缀、示例就绪指标「就绪」，本批接入 `L10n.T`（其余文案已在前序 M3-05 批次接好）；主题字段与分段文本此前已覆盖。
  - 新增 25 个键（22 `Home*` + 2 `ThemeEditor*` + 1 `Page.Desc.Home`），`DescHome` 在 `LocalizationSeedService` Page 键单行多条目区就地追加。
  - 接线策略：共享组件 `SbStatTile`/`SbPanel`/`SbField`/`SbBadge` 直接渲染属性（无内部 L10n），其 Label/Sub/标题/描述须显式包裹；`PageHead` 的 `Title/Desc` 字面量已有 `Page.Title.*`/`Page.Desc.*` 接管，仅未登记键需补。
  - RCL 0 错误 / 护栏 6/6 / 全量回归 708/708 / 四端（后端/RCL/Web/Maui-Windows）构建 0 错误均通过。L3 全部批次完成（A1/A2/B1/B2/C1/C2）。

### M3-06 用户语言偏好

- 首次登录用租户默认语言；切换后按 TenantId+UserId 服务端持久化，浏览器缓存只做快速恢复。
- 下次登录优先用户偏好；原语言被停用时回退租户默认。
- 登录前先恢复上次租户，再加载语言；无历史时使用平台默认语言。

- M3-06 用户语言偏好硬化 ✅ 完成（本地提交待推送 origin/master）。核量已在 M3-G0 落地（`UserLanguagePreference` 实体 + `UserLanguagePreferenceService` + `UserPreferenceController` + 前端 `LocalizationService.InitializeAsync/SetCultureAsync` 回退链 + `LanguageSwitcher`），本批补齐「语言停用回退」的**后端权威裁决**与发布门禁测试。
  - **关键修正（M3-06 硬化）**：`UserLanguagePreferenceService.GetAsync` 原样返回存储文化、未重新校验其是否仍属租户可用语言，仅靠前端的 `AvailableCultures.Contains` 兜底，后端非权威。改为读取时按 `ITenantLanguageService.GetAvailableCulturesAsync` 复核；越界（原语言被停用/平台下线）回退 `GetDefaultCultureAsync`（租户默认）。无记录时仍返回 `null`，保留「无偏好→前端回退租户默认」契约（既有 `Get_ReturnsNull_WhenNotSet` 不破）。
  - **测试补齐（发布门禁）**：`UserLanguagePreferenceServiceTests.Get_FallsBackToTenantDefault_WhenStoredLanguageDisabled`（设 en-US→停用 en-US→读回退 zh-CN）、`Get_ReturnsStoredCulture_WhenStillEnabled`（启用态保持用户偏好）、`UserPreferenceControllerTests.Get_ReturnsTenantDefault_WhenStoredLanguageDisabled`（同场景经 API 返回有效文化）；既有 `Set_OutOfRange`/RoundTrip/Idempotent 全绿。覆盖「刷新/换设备（服务端持久化读取）」「越界回退」「首次登录无偏好(null)」「语言停用回退」四项门禁。
  - **验证**：后端构建 0 error（25 个既有 CS0618 告警）；`UserLanguagePreferenceServiceTests`(6) + `UserPreferenceControllerTests`(4) + `ResourceKeyRegistryTests`(6) 全绿；全量回归 708/708。`Login.razor` 已按「选租户→加载语言→登录→按 (TenantId,UserId) 服务端偏好」顺序解析（满足「登录前恢复租户再加载语言」），`LocalizationService.SetCultureAsync` 仅登录用户写服务端、localStorage 仅作快速恢复，满足 M3-06 全部边界。

> **M3 Closure Batch 验收（2026-09-06）**：补齐 404、登录、自助注册和组件库演示项的残余用户可见文本；`ResourceKeyRegistryTests` 扩展为 9 项，新增「Razor 字面资源键必须已注册」「中英文格式占位符集必须一致」「Razor 用户可见中文节点/占位符禁止硬编码」门禁，9/9 通过。带 `PageHead Key` 的 Title/Desc 字面值明确为 key-backed 容错文本，不重复本地化。

M3 退出：✅ 已达成当前跟踪页面范围。平台/租户视图严格分离；租户只能使用授权语言；核心及全部页面可切换；刷新、换设备、语言停用回退均通过测试。

---

## 8. M4：数据源与元数据闭环

### M4-01 数据源页面

- 默认显示当前租户数据源列表；新增表单仅点击后出现。
- 展示名称、类型、启用状态、连接状态、元数据表数、字段数、最后扫描时间。
- 表格 100% 宽度并置于横向滚动容器。
- 创建、编辑、启停、测试、扫描都有明确成功/失败反馈。
- 连接串只能重新设置，不回显原值。

- M4-01 数据源页面增强 ✅ 完成（本地提交待推送 origin/master）。M4-02/03/04 经 M1 审计确认后端（`DataSourceAccessController`+`DataSourceAuthorizationService`、`MetadataController`、`DataSourceDetail` 关系导航）与前端三页面已就绪，本批仅补齐 M4-01 真实缺口。
  - **后端（`SuperBuilder_AI/src`）**：`DataSource` 实体新增 `LastScanAt`(UTC, nullable) 字段 + 迁移 `20260905233450_M4_01_DataSourceScanAt`（`SuperBIContextModelSnapshot` 同步）；`DataSourcesController` 新增 `PUT {id}` `Update`（名称必填/≤128 规范化、DbType 白名单校验、连接串仅非空时重置且不回显）、`PATCH {id}/enable`、`PATCH {id}/disable`、`POST {id}/test-connection`（复用 `RecordLastTestAsync` 与 `DataSourceConnectionFactory` 连接构造，返回 `{status,elapsedMs,errorCode}`，Ok/Failed/Timeout/脱敏异常类型名）；全部端点带 `metadata:edit` 守卫、`tid` 解析与 `DataSource` 归属校验；`MetadataController` 扫描成功后回写 `LastScanAt`。
  - **测试**：`DataSourcesControllerTests` 扩至 13 例（新增 7：更新改名下重置连接串/越租户 NotFound/空名拒绝/启停切换/测试连接 MissingSource/空连接串 BadRequest/拒绝连接 Failed 断言 `LastTestStatus=="Failed"`）；`ResourceKeyRegistryTests` 6/6（17 新键五处注册表同步护栏）；全量回归 **718/718**（预期 711-6+13）。
  - **前端（`SuperBuilder_AI.Components`）**：五处注册表同步新增 17 扁平 camelCase 键（`DataSourcesColConnStatus`/`DataSourcesColLastScan`/`DataSourcesConnOk`/`DataSourcesConnFailed`/`DataSourcesConnUnknown`/`DataSourcesConnNever`/`DataSourcesEnable`/`DataSourcesDisable`/`DataSourcesTesting`/`DataSourcesEditTitle`/`DataSourcesEditConnStr`/`DataSourcesEditConnStrPlaceholder`/`DataSourcesEditResetNote`/`DataSourcesTestedToast`/`DataSourcesToggledToast`/`DataSourcesEditSavedToast`/`DataSourcesEditFailed`），格式 `Content.Xxx` 严禁点号；`DataSources.razor` 五处编辑：列表新增「连接状态」「最后扫描」两列（状态 span 带 is-ok/is-fail 样式、`FormatScan` UTC→本地 `yyyy-MM-dd HH:mm`、空值「从未扫描」），操作列新增「编辑」「停用/启用」「测试连接」按钮（带 `TogglingId`/`TestingId` 禁用态），新增 `SbModal` 编辑弹窗（名称/`dbType` 只读/仅重置连接串输入框+说明）。
  - **验证**：RCL 构建 128 警告/0 错误；后端 28 警告/0 错误；Web、Maui-Windows 构建 0 错误；四端构建全绿。

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

- **M4-05 数据源扫描与同步闭环 ✅ 完成（2026-09-06，本地待提交）。** 此前后端已在位（Channel 队列 `MetadataScanQueue` + `MetadataScanHostedService` 后台处理器 + `MetadataScanJob`/`ScanTelemetry` 领域模型 + 迁移 `20260906000108_M4_05_ScanJob` + `MetadataController` 的 `POST /api/data-sources/{id}/metadata/scan`（202 + jobId）、`GET …/scan/{jobId}` 轮询端点 + `Program.cs` 单例队列/宿主服务注册；`MetadataScannerService.ScanAsync` 已支持 `progress/telemetry/cleanupOrphans` 孤儿安全清理）。本轮补齐**前端扫描集成**（此前 `DataSourceDetail.razor` 仍用旧同步 `Api.PostAsync`，对 202 误报“完成”且不轮询）：
  - **前端（`SuperBuilder_AI.Components`）**：`DataSourceDetail.razor` 的「重新扫描」改为异步任务模型——`StartScanAsync` 取 jobId 后 `PollScanAsync` 每 1.5s 轮询 `GetScanJobAsync` 直至 `Succeeded`/`Failed`（`CancellationTokenSource` 在 `IDisposable.Dispose` 中取消，导航离开即停）；扫描态面板显示状态徽标（排队/扫描中/完成/失败）、进度条、已扫描表/字段/清理孤儿计数、开始/结束时间、失败错误码+脱敏摘要及「重试」按钮；`app.css` 新增 `.scan-job*` 样式（成功/失败描边、进度条、统计、错误块）。`ApiClient` 的 `StartScanAsync`/`GetScanJobAsync`/`ScanJobView` 已在前序工作中就绪。
  - **后端收口**：`MetadataController.ScanDataSource` 的 202 响应 `status` 由硬编码小写 `"queued"` 改为 `job.Status.ToString()`（与 GET 端点枚举字符串一致）；`IApiClient` 新增 `StartScanAsync`/`GetScanJobAsync` 后补齐测试桩 `AuthStoreTests.StatusApiClient`（此前因接口扩展未实现导致测试工程编译失败）。
  - **测试**：新增 `MetadataScanControllerTests` 6 例（创建 Queued 任务并入队 jobId 一致 / GET 轮询返回状态 / 未授权 403 / 缺 `metadata:scan` 权限 403 / 旧固定扫描入口 410 / 401 缺令牌）；全量相关 9 例（含 `AuthStoreTests`）绿。
  - **验证**：RCL（net10.0/android/ios）、API、Web 三端构建均 0 error；四端构建全绿；M4-01~M4-05 后端+前端闭环完成。

---

## 9. M5：语义模型与 QueryPlan 企业化

| ID | 原编号 | 任务 | 验收 |
|---|---|---|---|
| M5-01 | SB-P1-01 | Canonical Semantic Model | Entity/Metric/Dimension/Filter/Binding 统一 ID 和定义（✅ 2026-09-06，1bb1d63：规范化语义 ID `CanonicalSemanticId` + 五类定义记录（Entity/Metric/Dimension/Filter/Binding）+ `ICanonicalSemanticResolver`/`PassThroughCanonicalResolver` 默认透传，11 例测试全绿，零默认行为变更、对 Golden 免疫） |
| M5-02 | SB-P1-02 | 统一字段解析规则 | Understanding/Builder/Validator 结果一致（✅ 2026-09-06，07aacf8：新增 `SemanticFieldReference`/`SemanticFieldBinding` 共享词汇 + `SemanticFieldBindingMatcher` 单一算法取代 Builder 三套近义匹配，Validator 以同词汇判定物理解析；18 例测试全绿，QueryPlan 相关 140/140 零回归，对 Golden 免疫） |
| M5-03 | SB-P1-03 | QueryPlan Pipeline Stage 化 | Metadata/Semantic/Security/Repair/Confidence/Decision 可扩展测试（✅ 2026-09-06，fb38cea：8 段链路抽离为有序 `IQueryPlanStage`，14 例阶段测试全绿，行为逐字节不变） |
| M5-04 | SB-P1-04 | 收缩 QueryPlanBuilder | Builder 只构造，不承担权限、安全判断（✅ 2026-09-06，bcbe537：数据源候选收敛抽离为 `IQueryPlanDataSourceScope`，45 例测试全绿，对 Golden 免疫） |
| M5-05 | SB-P1-05 | Column-Level Security | 未授权/脱敏字段不进入 Plan、SQL、结果（✅ 2026-09-06，0fc4a31：管线新阶段 QueryPlanColumnSecurityStage + IColumnSensitivityClassifier/IColumnSecurityPolicy/IColumnSecurityContextResolver，11 例测试全绿，零 schema 变更、对 Golden 免疫） |
| M5-06 | SB-P1-06 | Query Cost Governance | 高扫描、Join、无界 Limit、高模型成本可拒绝或降级（✅ 2026-09-06，d9e01a1：管线新阶段 QueryPlanCostGovernanceStage + IQueryCostClassifier/ICostGovernancePolicy/ICostGovernanceContextResolver + CostGovernanceOptions，15 例测试全绿，默认关闭零行为变更、对 Golden 免疫） |
| M5-07 | SB-P1-07 | Decision Gate 状态化 | ALLOW/REJECT/ASK_CLARIFICATION/REQUIRE_APPROVAL/LIMITED_EXECUTION（✅ 2026-09-06，cf6e5db：枚举规范为 5 态 + Decision 派生单一事实来源，33 例决策/状态/管线测试全绿，对 Golden 免疫） |
| M5-08 | SB-P1-12 | Governance Policy Enablement | 真实 `IColumnSensitivityClassifier`（基于 M5-01 规范化语义模型驱动敏感度分类，替换 `DenyNothingColumnClassifier`）+ `CostGovernanceOptions` 按真实数据量/`ModelCostTier` 调校 + 模型成本遥测 + 逐租户策略配置与灰度开关；将 M5-05/06 由安全默认提升为生产可用（✅ 2026-09-06，146f738：`ColumnSecurityOptions` 可配置分类器 `ConfigurableColumnClassifier` 在 `DenyNothing`↔`PolicyDriven` 间切换、`BuiltInPiiCatalog`+`PolicyDrivenColumnClassifier`+`ColumnSecurityModeResolver`+`TenantOverrides` 逐租户灰度；`CostGovernanceOptions` 增 `Mode`(Off/Threshold)/`TelemetryMode`(None/Log) 主开关与遥测；`IModelCostTelemetry`+`NoOp`/`Log`/`Configurable`；13 例启用测试 + 15 例 M5-06 回归全绿，完整套件 821/821 零回归，默认 DenyNothing/Off/None 零行为变更、对 Golden 免疫） |
| M5-09 | SB-P1-08 | AI Decision Audit | 可追踪问题、意图、计划、修复、置信度、决策、SQL、模型（✅ 2026-09-06，afd1135：`QueryPlanDecisionAuditRecord` 八维审计模型 + `IDecisionAuditSink`(`NoOp`/`Log`/`Configurable` 三件套，默认 None 零行为) + `DecisionAuditRecordBuilder` 静态提取 + `DecisionAuditOptions` 配置节；`QueryPlanPipeline` 在 finally 旁路采集且故障隔离，EarlyResponse/Decision Gate 阻断/成功三路径均审计；4 例测试全绿，完整套件 825/825 零回归，对 Golden 免疫） |
| M5-10 | SB-P1-10 | Production Feedback | Feedback→Candidate→Review→Baseline→Regression ✅ 2026-09-06（af88953） |
| M5-11 | SB-P1-11 | AI BI E2E | NL→API→Plan→SQL→Test DB→Result 全链覆盖（✅ 2026-09-06，0d81281：确定性桩隔离 LLM/metadata，真实 SqlQueryBuilder+QueryExecutionService+ResultUnderstandingService+BIConversationService 贯通；文件型 SQLite 临时库离线可还原、PostgreSQL 方言 SQL 在 SQLite 兼容执行；3 例 E2E 全绿，完整套件 837/837 零回归、对 Golden 免疫） |
| M5-12 | GQ-006 | 物料等缺独立主表导致 NotResolved | 真实解析为 DirectKey（✅ 2026-09-06，be6ac70：DimensionResolutionEvidenceService 命名根对齐发现稳定 FK 关联键 material_id，配合 display 列 material_name 存在性证明建立 DirectKey；符合 D03 契约第 4 条；零默认行为变更、不碰 Ranking Contract；3 例测试全绿，完整套件 840/840 零回归、对 Golden 免疫） |
| M5-13 | Phase 3.1.12.10 | 完成 Phase 2.7 Regression | Phase 2.7 Regression 完成（✅ 2026-09-06：唯一缺口 GQ-006 经 M5-12 闭合为 DirectKey；全量 840/840 零回归、对 Golden 免疫；Phase 2.7 文档无其他 BLOCK/NotResolved；暂不宣布 Phase 3.1 Frozen，因 M5-14 仍开放） |
| M5-14 | Ask 首用例 | 明细排序语义：实体 + Limit + Order 可构成有效计划，不强制 Metric/Dimension（具体旗舰回归已提前至 M0-09 早期批次） | Ask 首用例闭环（✅ 2026-09-06，42fc64f：新增 AskDetailListFirstUseCaseTests 2 例锁定契约——明细列表进入 SQL Builder 且不误报 SB_BI_002、真无字段才触发 SB_BI_002；实现层无需改动，M0-09 决策门+补列 / M5-11 全链已落地；完整套件 842/842 零回归、对 Golden 免疫） |

> **M5 执行顺序与边界（2026-09-06 修订）**：基础切片 M5-03~M5-07 已闭环（管线 Stage 化、Builder 收缩、列级安全、成本治理、决策门状态化，均对 Golden 免疫、零默认行为变更）。剩余按依赖顺序推进：**M5-01（规范化语义模型）→ M5-02（统一字段解析规则）→ M5-08（治理策略真实启用，将 M5-05/06 由安全默认提升为生产可用）→ M5-09（AI 决策审计）→ M5-10（生产反馈）→ M5-11（AI BI E2E）→ M5-12/M5-13/M5-14（既有收尾）**。M5-08 依赖 M5-01 的规范化语义模型以驱动真实的列敏感度分类。

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

（✅ 2026-09-06，70724b4：ConversationStatus 稳定枚举化并以 JsonStringEnumConverter 字符串序列化，wire 值不变、前端独立 string 副本无需改动；Question 长度契约 2–2000、ConversationId ≤128 护栏、Refine history ≤20 护栏；归属仍由 AskConversationService 按租户/用户校验；零默认行为变更；4 例 AskControllerTests 全绿，完整套件 846/846 零回归、对 Golden 免疫）

### M6-03 澄清与语义学习

> **进度**：M6-03-A 核心已交付（提交 cf04bef，869/869 零回归）。✅ 行为分类 + 结构化澄清详情 + 待澄清状态富化 + 循环检测（阈值3）。⏳ 后续增量（见下「待续」）：①管线结构化候选项（指标/维度/时间）从 `QueryPlanDecision`/`Explanation` 透传至 `ClarificationDetail`；②`TenantSemanticAlias` 持久化（提议/管理员审核/启停/审计）；③确认后免首问校验重放原 QueryPlan。

- 增加 NewQuestion/Clarification/Correction/Confirmation/Cancel 行为分类。
- 返回结构化指标、维度、实体、时间候选项。
- 建立 TenantSemanticAlias：提议、管理员审核、启停和审计。
- “入库单就是入库凭证”等确认仅在当前租户复用。
- 待澄清会话必须保存原问题、候选实体、Limit、Order、Filter、授权数据源和待确认槽位，不能只保存拼接后的自然语言。
- `X 就是 Y`、`我说的 X 是 Y` 等回复优先按实体/术语确认处理；确认后恢复并重放原 QueryPlan，不走“必须识别新指标或维度”的普通首问校验。
- 区分“一次会话临时确认”和“租户长期别名”：当前查询可立即使用，长期学习必须经租户管理员审核。
- 对同一计划、同一缺失槽位和同一候选集合设置循环检测；不得无限返回同一种 Medium Confirmation。

### M6-04 Cache 完整版本（SB-P1-09）

> **进度**：✅ 核心已交付（提交 a7b1b41，7 文件 +547/−5；完整套件 879/879 零回归、含 Golden）。
> - 7 维版本上下文：`PermissionFingerprint` + `RlsPolicyFingerprint` + `Culture`(CurrentUICulture) + `ModelVersion`(Qwen:Model) + `SemanticVersion`/`MetadataVersion`/`DataSourceVersion`（基于 `BaseEntity.RowVersion` 聚合，零迁移）。
> - 各子提供器可空 + try/catch 降级；`DataSourceCatalogVersionProvider` 目录指纹含启用态与行版本，撤权/禁用即时失效；全局语义标签 `TenantId==0` 对所有租户生效。
> - 未注入 `IAskCacheVersionProvider` 时走 `Legacy` 分支（保留 `policy:` 段），对主链路零侵入、对 Golden 免疫。
> - 验证：新增 `AskCacheVersionProviderTests`(9) + `AskControllerTests` 7 维键断言(1)。

- 保留 PermissionFingerprint、RLS PolicyFingerprint。
- 增加 SemanticVersion、MetadataVersion、Culture、ModelVersion、数据源集合版本。
- 撤权、策略、元数据、语义、语言变化后不复用旧结果。

### M6-05 审计、指标与真实 E2E

> **进度**：✅ 核心已交付（提交 1a1198e，14 文件 +727/−25；完整套件 887/887 零回归、含 Golden）。
> - Ask 审计三件套（镜像 M5-09）：`IAskAuditSink`/`NoOpAskAuditSink`/`LogAskAuditSink`/`ConfigurableAskAuditSink` + `AskAuditOptions(Mode=None)`；默认 None=NoOp，零默认行为变更、对 Golden 免疫；`AskController` 注入后于 `Ask`/`Refine` 的 try/finally 旁路记录（审计失败绝不破坏主响应）。
> - 脱敏：`AskPiiRedactor` 复用 `BuiltInPiiCatalog`（M5-08 内置 PII 列名集）+ 可选受限列，对原问题/SQL/结果样本落库前脱敏；`AskAuditRecordBuilder.Build` 统一脱敏入口。
> - 分段指标：`BIResponse` 加 `DurationMs`/`SegmentTimings`；`BIConversationService.ExecuteAsync` 用 `Stopwatch` 填充 MetadataUnderstand/Plan/SqlBuild/DbExec/ResultUnderstand 5 段（早期返回也填充）；粗粒度不改 `QueryPlanPipeline`，天然对 Golden 免疫。
> - 标记 E2E 范围：旗舰同义词回归（`最近的十张入库凭证` 按日期倒序最多十条真实凭证）已由 M6-03-A 的 `Resolve_AliasConfirmation_Returns_Confirmation_With_CandidateEntity` 与 M5-11 `QueryPlanAiBiE2ETests` 全链覆盖，M6-05 不重复新增（避免冗余）；M6-05 以 8 例单测覆盖审计落库/脱敏/失败路径（`AskPiiRedactorTests` 5 + `AskControllerTests` 3）。
> - ⏳ 后续增量：审计持久化 Sink（落库/外部日志）、分段指标接 Prometheus/OTel、真实 E2E 矩阵（澄清/确认/纠正/取消/撤权/跨租户/401-403/LLM·DB 失败）扩展为集成测试。

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
| M7-12 | FLOW/S6-4 | Agent 剩余工具接真实后端 | query/dashboard/forecast/report/alert/workflow 全部 live，或明确禁用且不可执行 |

> **进度 M7-01**：✅ 已交付（提交 7aa731e，8 文件 +3927/−3；全量 894/894 零回归、含 Golden）。
> - 草稿/发布隔离：`Dashboard.DslJson`（草稿工作副本）与 `Dashboard.PublishedDslJson`（发布快照）物理隔离；`Render` 优先返回发布态，编辑草稿不影响线上。
> - 可追踪回滚：新增 `DashboardVersion` 快照表（受控迁移 `20260906131855_M7_01_DashboardVersion`）；`Publish` 固化版本快照、自增 `PublishedVersion`；`Rollback(id,version)` 把历史版本恢复为发布态并再固化为新版本（`RolledBackFromVersion` 记录来源），版本链单调递增、全程可审计。
> - 端点：`POST api/dashboards/{id}/publish`、`POST api/dashboards/{id}/rollback/{version}`、`GET api/dashboards/{id}/versions`；`DashboardSummary` 暴露 `PublishedVersion`/`PublishedAt`。
> - 验证：`DashboardControllerVersioningTests`(7) 覆盖发布快照/草稿隔离/回滚恢复/版本列表/空草稿拒绝/未知版本 404/租户隔离。
> - ⏳ 后续：前端编辑器蓝图占位（`editor/blueprint`）已于 M7-09 接真实编辑器（`ComponentGallery.razor` 的 `OpenCreate`/`SaveAsync`）；**当前待办收敛为 M7-11、M7-12**（M7-03~M7-10 均已交付）。

> **进度 M7-02**：✅ 已交付（shell 恢复后 rebuild/retest/commit；全量 **901/901** 零回归、含 Golden 18/18，预期 894→901）。
> - 草稿/发布隔离（验收「不直接覆盖线上版本」）：`AppVersion` 快照实体（Domain/AppBuilder）+ `AppVersions` 表；`AppPlan` 加 `PublishedDslJson`/`PublishedVersion`/`PublishedAt`/`PublishedBy`；`Update` 仅改草稿 `DslJson`，发布态物理隔离。
> - 可追踪回滚：端点 `POST api/apps/{code}/publish`、`POST api/apps/{code}/rollback/{version}`、`GET api/apps/{code}/versions`（镜像 M7-01）；回滚固化为新版本（`RolledBackFromVersion` 记录来源），版本链单调递增；`Actor()` 取发布者（无认证回退 system）。
> - `AppSummary`/`AppDetail` 暴露 `PublishedVersion`/`PublishedAt`；DTO 嵌套于 `AppBuilderController`（沿用既有测试 `AppBuilderController.X` 限定风格），`PublishResult` 复用 M7-01 顶层类型（消除命名冲突）。
> - 迁移：`dotnet ef migrations remove` 回退快照后 `dotnet ef migrations add M7_02_AppVersion` 重建 Designer → `20260906141902_M7_02_AppVersion`（.cs + .Designer.cs）+ `SuperBIContextModelSnapshot` 校准（AppVersion + AppPlan 4 列）。
> - 验证：`AppBuilderControllerVersioningTests`(7) 覆盖发布快照/草稿隔离/回滚恢复/版本列表/空草稿拒绝/未知版本 404/租户隔离，全绿；测试编译修复（`using static AppBuilderController` + `PublishResult` 限定）。

> **进度 M7-03**：✅ 已交付（feat + 迁移 Designer + 测试；全量 **913/913** 零回归、含 Golden 18/18，预期 901→913）。
> - Agent 运行时（受控执行框架）：新增 `AgentRun` 领域实体（7 状态机 queued→running→succeeded / approval_pending→approved→succeeded / rejected / failed）+ `AgentRunStepRecord` 受控信封（`StepLogJson` 持久化，不另建步骤关系表，沿用 DSL-as-JSON 哲学）。
> - 权限 deny-by-default：`ToolPermissionPolicy.IsAllowed` 仅当授权集含该工具；运行级默认基线仅 Safe 级（metadata/semantic），Read/Write 须显式授予。
> - 审批闸门：Write 工具（Risk==Write）默认 `RequiresApproval`，未通过人工审批 → `ApprovalPending` 挂起，等待 `Approve`/`Reject`；跨租户审批 → 403。
> - 重试策略：`RetryPolicy` 仅对 `TransientToolException` 重试（零退避），其余异常直接穿透；瞬态重试成功路径带真实 Attempts 计数。
> - 受控信封（红线）：`ControlledToolBase` + 8 个具体工具（metadata/semantic=Safe、query/dashboard/forecast=Read、report/alert/workflow=Write）统一产出 `Mode=controlled` 信封，诚实声明「真实后端将于 M7-04 接入」，杜绝假成功按钮。
> - 端点（AgentController）：`POST plans/{code}/run`（approval_pending 返回 202，否则 200）、`GET runs/{id}`、`POST runs/{id}/approve`、`POST runs/{id}/reject`；DI 全部 `AddScoped` 注册。
> - 迁移：`20260907004450_M7_03_AgentRuntime`（.cs + .Designer.cs）+ `SuperBIContext` 加 `AgentRuns` DbSet/查询过滤器（不放行 TenantId==0）；`SuperBIContextModelSnapshot` 校准。
> - 验证：`AgentRuntimeTests`(12) 覆盖权限 deny-by-default、审批闸门（挂起/通过/拒绝）、瞬态重试至成功、非瞬态直接失败、状态机/隔离/诚实信封、持久化回读、跨租户审批拒绝，全绿。

> **进度 M7-04**：✅ 已交付（feat + 计划标记；全量 **916/916** 零回归、含 Golden 18/18，预期 913→916）。
> - 接真实后端（M7-03 受控信封所声明的「M7-04 接入」落地）：把确定性、只读、无 LLM 的 Safe 工具翻为 `live` 模式——
>   - `LiveMetadataTool`：真实查询 `SuperBIContext.MetadataTables`(+`Columns`)，返回真实表/字段结构；租户隔离（`ApplyTenantScope`）。
>   - `LiveSemanticTool`：真实查询 `SuperBIContext.SemanticLabels`（租户+全局共享），返回真实概念→标签/同义词映射。
> - 运行时框架零改动（M7-03 设计）：`AgentRuntime` 仅记录 `step.Mode`（controlled/live），工具自身翻 live 即可；`ControlledToolCatalog` 经 `IEnumerable<ITool>` 自动聚合。
> - 诚实化受控信封（红线「无假成功按钮」）：`ControlledToolBase` 信封显式携带 `"connected":false` 与 `backendMilestone:"M7-05~M7-08"`，明确「未接真实后端、仅回执、不声称业务结果」；其余 Read/Write 工具（query/dashboard/forecast/report/alert/workflow）真实后端于 M7-05~M7-08 逐工具接入前继续诚实信封。
> - 工具目录能力标注：`ToolRegistry.AgentToolDescriptor` 增 `BackendStatus`(live/pending) + `BackendMilestone`；`GET /api/agent/tools` 诚实反映 metadata/semantic=live(M7-04)，其余=pending；编辑器据此可知哪些工具真实可用。
> - DI：`Program.cs` 将 `MetadataTool`/`SemanticTool` 注册替换为 `LiveMetadataTool`/`LiveSemanticTool`（其余 6 个保持受控）。
> - 验证：`AgentRuntimeTests` 新增 3 例（live 元数据返回真实表/字段、live 语义返回真实标签、目录诚实状态 2 live/6 pending），全绿；全量 916/916 零回归。

> **进度 M7-05**：✅ 已交付（feat + 计划标记；全量零回归、含 Golden 18/18；前端三端 build 0 error）。
> - 后端：`BusinessEntityUpsertRequest`（标量 DTO，BusinessKey/Name 必填）+ `BusinessModelController` 增 `POST/PUT/DELETE api/business-model/entities`（注入 `IBusinessEntityService`，沿用 `TenantDataPlanePolicy.Resolve` 租户隔离；PUT 先取回带正确 `RowVersion` 的实体再映射，规避乐观并发冲突）。
> - 前端（RCL）：`BusinessModel.razor` 占位 Toast 替换为 `SbModal` 创建表单（BusinessKey/Name/DisplayName/Description/BusinessDomain/SemanticText/Status），提交后刷新列表；`BusinessModelEntityDetail.razor` 增“编辑”（预填 `SbModal` → `PUT`）与“删除”（`DELETE` → 返回列表），均复用 `IApiClient`。中文标签用字面串，未触碰本地化 `Keys.cs` 以保持提交隔离。
> - 测试：`BusinessModelControllerTests` 10/10（3 读 + 7 写：创建持久化/必填 400/编辑持久化/编辑 404/删除 204/跨租户创建 403/跨租户编辑 403），`TenantDataPlanePolicyTests` 因第三构造函数参数补 `EntityServiceStub` 修复；真实 SQLite 内存持久化 + 租户隔离。
> - 现状盘点：实体模型 `BusinessEntity` 及聚合（Keys/Attributes/Metrics/Relationships/PhysicalBindings/Domain/Dimension）已于 Phase 3.1 落地为 `SuperBIContext` 的 DbSet；`IBusinessEntityService`（Program.cs:95 注册）已具备 `CreateAsync/UpdateAsync/DeleteAsync`——真实 EF 持久化 + 租户隔离（按 `TenantId` 校验 + `EnsureTenantAsync`）+ `ValidateBindingsAsync` 跨租户物理绑定校验。`BusinessModelController` 当前仅只读（entities/domains/resolve/entities/{id}）。缺口：无写端点、前端“新建”为占位 Toast、无 CRUD 测试。
> - 后端端点（控制器注入 `IBusinessEntityService`，沿用 `TenantDataPlanePolicy.Resolve` 租户隔离）：`POST api/business-model/entities`（请求 DTO 标量字段 → 置 `entity.TenantId`=解析租户 → `CreateAsync` → 201 + 实体）、`PUT api/business-model/entities/{id}`（`entity.Id=id; TenantId=解析租户` → `UpdateAsync` → 200）、`DELETE api/business-model/entities/{id}`（`DeleteAsync` → 204）；跨租户 403、必填（BusinessKey/Name）400、空聚合创建直接通过校验。
> - 前端闭环（RCL/Web，复用 `SbModal` + `IApiClient.Post/Put/Delete`）：`BusinessModel.razor` 的“新建”占位 Toast 替换为打开 `SbModal` 创建表单（Name/DisplayName/Description/BusinessDomain/SemanticText/Status），提交后刷新列表；`BusinessModelEntityDetail.razor` 增“编辑”（预填 `SbModal` → `PUT`）与“删除”（`DELETE` → 返回列表）。遵循 Blazor 约束：事件单引号属性、`OnAfterRenderAsync` 取数、modal 双向 `VisibleChanged`。
> - 测试（真实持久化，沿用 M7-03/M7-04 SQLite 内存 + `IBusinessEntityService`）：新建→详情往返、编辑往返、删除→404、跨租户创建/编辑/删除 403、必填 400；预期 916 → +N 零回归。
> - 零回归门禁：全量绿、build 0 error、Golden 18/18 不变；双提交 `feat` + `docs`，不推送 origin。
> - 红线：「无后端能力按钮禁用」——本里程碑补全真实写能力，移除占位 Toast；写操作失败明确返回错误，不伪造成功。
> - 范围外（本里程碑不做）：子聚合（Keys/Attributes/Metrics/Dimensions/Relationships/PhysicalBindings）逐字段编辑 UI 与 `resolve` 自然语言映射（依赖 LLM），留待后续里程碑；M7-05 仅保证核心实体 CRUD 闭环与租户隔离。

> **进度 M7-06**：✅ 已交付（feat + 计划标记；全量零回归、含 Golden 18/18；前端三端 build 0 error）。
> - 验收：保存（POST 新建 / PUT 编辑）真实持久化、预览实时、发布一致（assign-default 级联 Dashboard/App 取用）；保存/指派成功给出真实反馈，移除占位 Toast；跨租户隔离沿用 `TenantDataPlanePolicy.ResolvePlatformScope`；全量零回归、含 Golden 18/18 不变；前端三端 build 0 error。
> - 现状盘点：
>   - 后端 `api/themes` 已完整可用（迁移 `20260830034528_P7_1_Theme` + `SuperBIContext.Themes` + `Theme` 实体）：`POST /api/themes`（`CreateThemeRequest{TenantId,Key,DslJson,Name}`→201 `ThemeDetail`）、`GET /api/themes?tenantId`、`GET /api/themes/{key}?tenantId`、`PUT /api/themes/{key}?tenantId`（`UpdateThemeRequest{DslJson,Name}`→200）、`DELETE /api/themes/{key}?tenantId`、`POST /api/themes/{key}/assign-default?tenantId`（写 `TenantSetting "theme:defaultKey"`）、`POST /api/themes/{key}/copy`（`CopyThemeRequest`）、`GET /api/themes/editor/blueprint`（`ThemeEditorBlueprint{DslVersion,BuiltInKeys,Skeleton}`）。所有写端点经 `TenantDataPlanePolicy.ResolvePlatformScope` 隔离、DSL 经 `ThemeDslSerializer.TryDeserialize` 校验（版本白名单），内置主题不可改/删。
>   - `ThemeDsl` 语义键：`Brand{Primary,Accent}`、`Color{Primary,Success,Warning,Danger,Neutral,Background,Surface,Text,TextMuted,Border}`、`Typography/Layout/Border/Radius/Shadow/ChartPalette/Component/DashboardTemplate`；`ThemeResolver` 级联：显式 Key → 租户默认（TenantSetting）→ 内置 `default`。
>   - 前端 `ThemeEditor.razor` 当前仅硬编码 `Presets`（8 键：primary/accent/success/warning/info/bg/text/border）+ `Save()` 占位 `Toast.Info("…P11.3 收口")`；未注入 `IApiClient`、无 `ThemeDsl` 映射、无序列化、无发布一致性。本地化已注册 `Theme.Light/Dark`、`Action.Reset/Save`、`Content.ThemeEditor*` 等约 12 键；`ThemeEditorSavedToast` 仍含 "P11.3 收口" 占位文案需替换。
>   - 键错位（需映射）：前端 `Presets` 8 键 ↔ `ThemeDsl.Color/Brand` 10 键——`primary→Color.Primary`、`accent→Brand.Accent`、`success→Color.Success`、`warning→Color.Warning`、`info→Color.Neutral`、`bg→Color.Background`、`text→Color.Text`、`border→Color.Border`；DSL 的 `Danger/Surface/TextMuted` 前端未编辑，保存时用蓝图默认填充。
> - 实施（前端 RCL，复用 `IApiClient` + 既有 `ToastService`）：
>   1. 注入 `@inject ApiClient Api`（经 `IApiClient`）；初始化 `OnAfterRenderAsync(firstRender)` 内 `GET /api/themes/editor/blueprint` 取 `Skeleton` 与 `BuiltInKeys`，`GET /api/themes?tenantId={State.TenantId}` 取主题清单填充下拉。
>   2. 主题清单下拉（新增键 `Content.ThemeEditorSelectTheme`）：选“新建”→清空 Key/Name、按蓝图骨架初始化 8 个色板；选已有非内置主题→`GET /api/themes/{key}` 取 `DslJson`，用 `System.Text.Json.JsonNode` 解析并把 `color.*`/`brand.*` 回填到 `_tokens`（保留其余令牌）。
>   3. 新增「主题键 / 名称」输入（Key 正则 `^[a-z0-9-]+$`、必填；Name 可选）；Key 冲突由后端 409 诚实返回。
>   4. `Save()`：`_tokens` → `JsonNode` 改写 `color.*`/`brand.*`（新建自蓝图骨架，编辑自已取回 DSL）→ camelCase 序列化 → 新建 `POST /api/themes`（`{tenantId,key,dslJson,name}`）、已有非内置 `PUT /api/themes/{key}?tenantId`；失败显示错误 alert + `Toast.Error`（新增键 `Content.ThemeEditorSaveFailed`），成功 `Toast.Success`（替换占位 `ThemeEditorSavedToast` 为真实文案，新增 `Content.ThemeEditorSaved`）。
>   5. 「指派为默认」按钮（新增键 `Action.ThemeAssignDefault` / `Content.ThemeEditorAssigned`）：`POST /api/themes/{key}/assign-default?tenantId={State.TenantId}` → 写 `TenantSetting "theme:defaultKey"`，级联 `ThemeResolver` 被 Dashboard/App 取用；内置主题亦可指派（后端允许 default 不存在时合成）。
>   6. 内置主题只读：下拉中标灰、禁用编辑/删除（后端亦拒绝）；「删除」按钮仅对非内置主题可用（`DELETE`）。
>   7. 预览 `_previewStyle` 继续用 `--sb-{key}` 变量驱动，色板改动实时反映；`TokenLabel` 沿用 `Presets` 中文标签。
> - 本地化 4 向新增/修订（与 M7-05 同护栏）：`Keys.cs`(const+Defaults)、`ResourceKeys.cs`(const+Catalog)、`LocalizationSeedService.ZhCnDefaults`、`All()` 反射；新增 `Content.ThemeEditorSelectTheme`、`Action.ThemeAssignDefault`、`Content.ThemeEditorAssigned`、`Content.ThemeEditorSaved`、`Content.ThemeEditorSaveFailed`、`Content.ThemeEditorKey`/`ThemeEditorName`/`ThemeEditorDeleteConfirm`/`ThemeEditorBuiltInReadOnly`；将 `ThemeEditorSavedToast` 文案由占位改为真实“主题已保存。”并标记旧键可废弃。护栏 `Razor_Markup_ContainsNo_Unlocalized_Cjk_TextNodes_Or_Placeholders` 约束：字面中文仅限占位 `placeholder="…"` 与 `>中文<` 文本节点，Label/Title/Desc 与 `@code` 字面量豁免——新增 UI 文案一律走 `L10n.T(Keys.*,"…")`。
> - 测试与零回归门禁：后端 `ThemeControllerTests`(11) 已覆盖，本里程碑不新增后端测试；前端以既有 `ResourceKeyRegistryTests` 护栏 + 手动 E2E（新建→列表出现→刷新后仍在→指派默认→Dashboard 取用）验证；预期基线 923 → 零回归（仅文案/UI 改动，不计新增后端测试）。全量绿、build 0 error、Golden 18/18 不变。
> - 红线「无假成功按钮」：移除 `Save()` 占位 Toast；保存/指派失败必须明确报错（错误 alert + `Toast.Error`），不得伪造成功；无后端能力时按钮禁用并显示原因。
> - 范围外（本里程碑不做）：`copy` 端点前端入口（蓝图复制，留待 M7-10 复用主题时一并做）、`Typography/Layout/Radius/Shadow/ChartPalette/Component` 逐令牌编辑 UI（仅做 Color/Brand 8 键映射，其余令牌保持蓝图默认）、多租户管理面（治理角色改他租户主题）、深色模式持久化（当前 `_mode` 仅切预览，不落库）。
> - 交付要点（2026-09-07）：前端 `ThemeEditor.razor` 注入 `IApiClient`+`AppState`，`OnAfterRenderAsync` 加载 `editor/blueprint` 与主题清单；色板 8 键经 `JsonNode` 映射为 `ThemeDsl.Color/Brand`（primary→color.primary、accent→brand.accent、success→color.success、warning→color.warning、info→color.neutral、bg→color.background、text→color.text、border→color.border）后 camelCase 序列化；新建 `POST /api/themes`、编辑 `PUT /api/themes/{key}`、指派 `POST /api/themes/{key}/assign-default`（级联 `TenantSetting "theme:defaultKey"`）、删除 `DELETE`；内置主题下拉标灰只读、删除/指派仅限非内置；深色预览为本地 `filter` 反相辅助（不落库）。
> - 红线落实：移除原占位 `Toast.Info("…P11.3 收口")`，保存/指派/删除失败显式报错（`ThemeEditorSaveFailed` + `Toast.Error`），无假成功按钮；`ThemeEditorSavedToast` 占位文案已替换为真实「主题已保存。」并新增 9 个主题键（RCL `Keys.cs` const+Defaults、后端 `ResourceKeys.cs` const+Catalog、`LocalizationSeedService.ZhCnDefaults`、`All()` 反射 4 向一致，ResourceKeyRegistryTests 9/9 全绿）。构建期 `JsonValue` 歧义（`SuperBuilder_AI.Components.Models.JsonValue` vs `System.Text.Json.Nodes.JsonValue`）已用全限定名修复。
> - 验证：RCL（net10.0/android/ios）、API build 0 error；全量测试零回归（含 Golden 18/18）。

> **交付 M7-07**（2026-09-07）：ModelAccounts 接模型目录与加密绑定——全部验收达成，占位清零。
> - 验收：模型账号「真实持久化 + 加密存储 + 租户隔离 + 目录驱动」；前端下拉/绑定/设默认全部走真实端点；无静态假数据、无明文 Key（密钥仅存密文 + 展示掩码）；审计/日志 Key 脱敏；全量零回归、含 Golden 18/18；前端三端 build 0 error。
> - 现状盘点（Explore 只读）：
>   - 后端**完全缺失** `ModelAccount` 实体、`SuperBIContext` 无相关 DbSet、无迁移（最新 M7-03）、无 `ModelAccountsController`、无 `ISecretStore`/`Encrypt`/`Decrypt`/`KeyVault` 任何加密层；`QwenService`(L79-80) 与 `QwenEmbeddingService`(L44/80) 从 `IConfiguration`/`EmbeddingOptions.ApiKey` **明文**读取密钥。
>   - 红线违规：`appsettings.Local.json`(L8/11) 提交了**真实明文** `Qwen:ApiKey`/`Embedding:ApiKey`；`DemoDataInstaller`(L34) 硬编码明文演示口令 `Demo@123456`；`ModelAccounts.razor`(L53-71) 硬编码 5 个模型 + 假 `Bound/Default` + `Bind()` 仅弹 Toast「计划于 P13 实现」。
>   - 模型目录：仅 `ModelAccounts.razor`(L53-63) 内存硬编码 5 个模型（Qwen-Plus/Max、OpenAI GPT-4o、Azure OpenAI、DeepSeek），无目录服务/表；`QwenService` 只认配置单一 `Qwen:Model`，无多供应商抽象。
>   - 前端 `ModelAccounts.razor`(`/model-accounts`) 占位：假数据 + `Bind()`/`SetDefault` 不调后端；本地化键已齐备（约 20 个：`ModelAccountsBound`/`ModelAccountsBindSubmitted`/`ModelAccountsDescQwenPlus` 等，RCL `Keys.cs` L182/228/238/410-423/1011-1015、`ResourceKeys.cs` L385/1674、`LocalizationSeedService.cs` L384），4 向一致，无需新增键（加密相关提示可视情补 2-3 个）。
>   - 租户隔离范式就绪可复用：`TenantDataPlanePolicy.ResolvePlatformScope`(L189) + `SuperBIContext.ApplyTenantScope`(L45)；`IsDataPlanePath`(L158) 需补登记 `/api/model-accounts`。测试：无 ModelAccount 测试，可复用 `TenantDataPlanePolicyTests` 基线。
> - 实施（后端优先）：
>   1. **模型目录**：新增 `ModelCatalog` 实体（或种子配置，参考 M10-02：供应商 + 模型能力/上下文窗/区域/状态/降级）+ 迁移，种子 Qwen/OpenAI/DeepSeek/Azure 等；端点 `GET /api/model-catalog` 返回可选模型清单（租户可见，能力驱动）。
>   2. **加密绑定层**：新增 `ISecretStore`（优先 AEAD 信封加密；主密钥取自环境变量/KeyVault，禁止落库明文），提供 `Protect(plain)->cipher` / `Unprotect(cipher)->plain`；`ModelAccount` 仅持久化 `EncryptedKey`（密文）+ `MaskedKey`（如 `sk-***1234` 展示用）+ `Provider`/`ModelId`/`TenantId`/`IsDefault`/`CreatedBy`。
>   3. **实体与迁移**：`ModelAccount` + `SuperBIContext` DbSet + 受控迁移（租户隔离：AlternateKey `{Id,TenantId}` + `HasQueryFilter`，参考 `DataSource`）；密钥不进快照明文。
>   4. **Controller**：`ModelAccountsController`（`POST/GET/GET{id}/PUT/DELETE /api/model-accounts` + `POST /api/model-accounts/{id}/set-default`）经 `TenantDataPlanePolicy.ResolvePlatformScope` 隔离；`Bind` 接收 `provider/modelId/key/note` → `ISecretStore.Protect` 后落库；读取时按需 `Unprotect` 注入 LLM 客户端（仅服务端，绝不下发明文）；`GET` 列表返回掩码。
>   5. **红线清理**：移除 `appsettings.Local.json` 明文 Key，改用环境变量/机密管理并确保不进版本库（核对 `.gitignore`）；`DemoDataInstaller` 明文口令改为随机生成或强制首次修改（不在本里程碑强绑，标注后续）。
>   6. **前端重写** `ModelAccounts.razor`：注入 `IApiClient`+`AppState`；`OnAfterRenderAsync` 拉 `GET /api/model-catalog` 与 `GET /api/model-accounts?tenantId`；下拉改自目录、绑定提交 `POST`（密码框 Key→密文由后端加密，前端不经手明文存储）、`SetDefault` 调 `set-default`；展示后端返回的 Bound/Default 与掩码 Key；失败显式报错（无假成功）。
> - 测试与零回归门禁：新增 `ModelAccountServiceTests`（加密往返、租户作用域）、`ModelAccountsControllerTests`（CRUD + 跨租户 403 + 明文不下发 + 掩码返回）、`ModelCatalogTests`（目录完整性）、`ModelAccountsRazorTests`（绑定提交/下拉）；预期 923 → +N 零回归，全量绿、build 0 error、Golden 18/18 不变。
> - 红线「无静态假数据/明文 Key」：移除 `ModelAccounts.razor` 假数据与 Toast 占位；密钥一律密文存储、展示掩码、日志脱敏；无后端能力时按钮禁用。
> - 范围外（本里程碑不做）：多供应商 LLM 运行时动态切换与故障转移（M10-02 后续）、`UserModelBinding` 个人级绑定（先租户级 `TenantModelBinding`）、`DemoDataInstaller` 口令随机化（标注为独立红线清理项）。
> - **交付要点（2026-09-07）**：
>   - 后端：① `ModelAccount` 实体 + `SuperBIContext` DbSet + 受控迁移 `P7_3_ModelAccounts`（表 `ModelAccounts`：TenantId/Provider/ModelId/DisplayName/EncryptedKey(2048)/MaskedKey(64)/Note?/IsDefault + 审计列；唯一索引 `IX_ModelAccounts_TenantId_Provider_ModelId` + 租户查询过滤器）；② `ISecretStore`(`AesGcmSecretStore`，AES-256-GCM，密文 `v1:base64(nonce|cipher|tag)`) + `Program.cs` 延迟到解析期 fail-fast 注册（不阻断 `dotnet ef`）；③ 静态 `ModelCatalogProvider`(5 项：Qwen/qwen-plus、Qwen/qwen-max、OpenAI/gpt-4o、Azure/azure-gpt-4o、DeepSeek/deepseek-chat) + `GET /api/model-catalog`；④ `ModelAccountService`(加密落库/掩码/首绑默认/设默认唯一性/服务端 `ResolvePlaintextKeyAsync` 仅供 LLM 注入) + `ModelAccountsController`(POST/GET/GET{id}/PUT/DELETE `/api/model-accounts` + `POST /api/model-accounts/{id}/set-default`，租户隔离镜像 `ThemeController`，掩码返回、明文绝不下发)；⑤ 错误码补 `Conflict`(SB_CONFLICT, 409)。
>   - 前端：`ModelAccounts.razor` 重写——注入 `IApiClient`+`AppState`+`Toast`，`OnAfterRenderAsync` 拉 `GET /api/model-catalog` 与 `GET /api/model-accounts?tenantId`；目录驱动卡片（Bound/Default 徽标 + 掩码 Key 展示）、绑定提交 `POST`（密钥仅前端输入、后端 AES-GCM 加密）、设默认 `POST set-default`、删除 `DELETE` + `SbModal` 确认；移除硬编码 5 模型与 `Bind()` Toast 占位；失败显式 `Toast.Error`（`BindFailed`/`Deleted`/`DeleteConfirm` 3 键已落 4 向，并修订 `BindSubmitted` 为「绑定已保存。」）。
>   - 红线清理：`appsettings.Local.json` 明文 `Qwen:ApiKey`/`Embedding:ApiKey` 改为与已提交 `appsettings.json` 一致的环境变量占位符（`__SET_VIA_ENV_…__`），并新增 `SecretStore:MasterKey`(32 字节 base64 开发主密钥，文件已被 `.gitignore:14` 忽略，不进版本库)；连接串本地 DB 密码为预存本地配置，本次未动。
>   - 测试与零回归门禁：新增 `ModelAccountServiceTests`(8：加密落库+掩码、首绑默认、重复冲突、租户隔离、设默认唯一性、明文往返、删除、轮换 Key)、`ModelAccountsControllerTests`(8：CRUD+掩码返回、重复 409、跨租户 403×2、设默认唯一性、删除、GetById 404)、`ModelCatalogTests`(3：目录完整性/展示名/契约对齐)；全量零回归、Golden 18/18 不变。
>   - 验证：API `dotnet build` **0 error**（仅预存 CS/CA 告警）；RCL（net10.0/android/ios）`dotnet build` **0 error**（仅预存 IL2026 裁剪告警）；`ModelAccounts.razor` 编译干净。

> **交付 M7-08**（2026-09-07）：Quota 平台默认、租户覆盖与用量维护闭环——全部验收达成。
> - 后端契约：`QuotaItemView` 新增稳定字符串 `ResourceType/Window`、`IsOverride`、`PlatformLimit/PlatformWindow`，前端可同时展示生效策略与继承基线；`IQuotaService` 新增策略 Upsert、删除租户覆盖、维护当前周期用量能力。
> - 管理端点：新增 `GET api/quota/defaults`、`PUT/DELETE api/quota/policy/{resourceType}`、`PUT api/quota/usage/{resourceType}`；平台默认可维护但不可删除，租户覆盖可恢复继承，用量仅允许写实际租户。
> - 权限与范围：策略/用量写端点强制 `platform:quota:manage`；多个平台管理员沿用 `IPlatformAdminScopeService`，只能管理授权租户；普通租户主体仅可读取/校验/扣减自身租户配额；治理目标写入审计上下文。
> - 前端：`Quota.razor` 从原始 JSON 平铺升级为正式管理表格；可切换平台默认或授权租户，显示资源、上限、已用、剩余、周期、周期键及策略来源；支持编辑策略、维护用量、恢复继承；导航与页面均绑定 `PlatformQuotaManage`。
> - 多语言：新增 34 个中英文配额资源键，并同步 RCL Defaults、后端 Catalog、zh-CN 种子三处注册表；资源名、周期、策略来源、弹窗、成功/失败反馈全部可切换语言。
> - 验证：配额专项 + 多语言护栏 **35/35**；全量测试 **950/950**（含 Golden 18/18）零回归；API/RCL/Web/MAUI Windows 均 build 0 error（仅预存告警）；无需迁移（复用既有 `QuotaPolicies/QuotaUsages` 表）。
> - 边界澄清：M7-08 按原表定义仅负责 Quota。M7-04 曾承诺的 6 个 pending Agent 工具并未因配额交付自动变为 live，现显式收口为 M7-12，禁止以 `controlled/connected:false` 回执冒充 M7 完成。

> **交付 M7-09**（2026-09-07，提交 `a788254`）：租户自定义组件领域、持久化、API 与正式资产页闭环——全部验收达成。
> - 领域与持久化：新增 `CustomComponentDefinition`（草稿/发布态物理隔离）和不可变 `CustomComponentVersion`；受控迁移 `20260907053931_M7_09_CustomComponents` 建立租户+Key 唯一索引、组件+版本唯一索引和级联版本关系；两表均启用严格租户查询过滤器，不放行 `TenantId=0`。
> - 安全 DSL：`CustomComponentDslSerializer` 限制 64KB、版本和 Key 格式，将单组件包装为 `AppDsl` 复用既有组件类型、数据绑定、聚合、筛选白名单；名称/描述/标题中的 HTML、`javascript:` 和未知组件类型在落库前拒绝。
> - API：新增 `api/components` 列表/详情/创建/编辑/删除、`publish`、`versions`、`rollback/{version}`、`render` 与 `editor/blueprint`；运行时 `render` 只读取已发布快照、重新校验并返回强类型 `ComponentPlan`，不返回或执行 HTML；回滚生成新版本并记录来源，不改写历史。
> - 权限与隔离：数据面加入 `TenantDataPlanePolicy`；读/创建/编辑/删除/发布分别绑定 `app:view/create/edit/delete/publish`，跨租户请求拒绝；导航和页面也绑定同一权限常量。
> - 前端：`/components` 从静态设计系统演示替换为真实租户资产列表；支持新增、编辑、安全 JSON 预览、发布、版本历史、回滚和删除；表格 100% 宽且窄屏横向滚动；所有成功/失败均由真实端点结果驱动，无假成功按钮。
> - 多语言：新增 37 个组件资产中英文资源键并同步 RCL Defaults、后端 Catalog 与 zh-CN 种子；页面标题说明也由“设计系统巡展”改为真实组件生命周期说明。
> - 验证：`CustomComponentsTests` 3 组覆盖脚本/HTML 与未知类型拒绝、CRUD、细粒度权限、跨租户隔离、草稿/发布隔离、回滚新版本链；组件/资源键专项 **13/13**；全量 **953/953**（含 Golden 18/18）零回归；API/RCL（net10.0/Android/iOS）/Web build 均 0 error（仅预存移动端裁剪与资源路径告警）。

> **交付 M7-10**（2026-09-07）：自定义主题在 App、Dashboard 与主题资产间完成可授权复用——全部验收达成。
> - 主题可见性：App/Dashboard 创建、生成、编辑、发布和回滚均校验主题属于当前租户或平台内置主题；跨租户主题引用返回 400，阻止通过 DSL 绕过租户边界。主题列表在数据库未落内置行时仍合成 `default`，前端按当前语言显示其名称。
> - 生命周期一致性：App 自然语言生成可显式选择主题并写回生成 DSL；App/Dashboard 编辑可切换租户授权主题。Dashboard 详情端点返回 `DslJson` 供真实回填，发布态渲染从发布快照读取 `ThemeKey`，草稿换肤不会污染线上版本，回滚也恢复对应历史主题。
> - 主题资产治理：内置默认主题可被指派为租户默认，也可通过既有 `copy` 端点复制为租户自定义主题；内置项保持只读。删除主题前检查租户默认设置、App/Dashboard 草稿与全部历史版本引用，有引用时返回 409，避免已发布资产失效。
> - 前端：`/apps` 生成与编辑、`/dashboards` 新建与编辑均接真实主题下拉；`/themes` 增加复制入口、内置主题指派默认与只读状态。所有保存、复制和删除反馈来自真实端点，无假成功。
> - 多语言：新增 14 个中英文主题复用资源键，并同步 RCL Defaults、后端 Catalog 与 zh-CN 种子；内置默认主题名称由前端按当前语言渲染。
> - 验证：新增/调整主题、App、Dashboard 测试，覆盖跨租户拒绝、引用删除冲突、详情 DSL 回填及发布快照主题渲染；全量 **957/957**（含 Golden 18/18）零回归；API、RCL（net10.0/Android/iOS）与 Web build 均 0 error（仅预存移动端裁剪与资源路径告警）；无需迁移。

无后端能力的按钮必须禁用并显示原因，不得提示虚假的“已保存/已运行”。

---

## 12. M8：前端体验与多端发布

### M8-01 统一视觉系统

- 全平台使用统一现代科技风格：颜色、圆角、阴影、间距、图标、按钮和状态组件一致。
- `btn-primary`、`badge-primary` 等 `*-primary` 使用主题变量；统一 hover/active/focus/disabled/深色模式。
- 避免浏览器默认控件和局部 Bootstrap 默认蓝色。

> **交付 M8-01**（2026-09-07；纯前端 `app.css` 样式，零行为语义变更，双提交 feat+docs，未推送 origin）：
> - **目标（保留原三原则）**：全平台统一视觉令牌；`btn-*`/`badge-*` 等 `*-primary` 全态（hover/active/focus/disabled/深色）映射品牌色；消除浏览器默认控件外观与局部 Bootstrap 默认蓝/灰蓝渗透。
> - **现状盘点（基于 2026-09-07 实际勘察 `app.css` + 组件 grep）**：
>   - **已具备**：`:root` 与 `[data-theme="dark"]` 完整 `--sb-*` 古风令牌（黛蓝/朱砂/黛绿/藤黄/石青 + 结构变量 `--sb-radius/-shadow*/-ring`）+ 全套 `--bs-*` 覆盖；`.btn-primary` 已全态 token 化（L163-177：hover 位移+阴影、active 下沉、disabled 复位、focus 用 `--bs-primary-rgb`）；`#0d6efd`（Bootstrap 默认蓝）仅存于 `bootstrap.min.css`，**应用层无硬编码默认蓝**。
>   - **残留缺口**：
>     1. `.badge-primary` **重复定义**——L178 `color-mix` 自适应 vs L732 硬编码 `rgba(47,79,111,.12)`（后者胜出）→ 深色模式错位。
>     2. `.badge-*` 背景（L732-737）硬编码 5 个浅色 RGBA 字面量（`rgba(47,79,111,…)`/`rgba(79,111,82,…)`/`rgba(201,162,39,…)`/`rgba(158,61,52,…)`/`rgba(74,122,140,…)`）→ 深色模式对比度/色相漂移；`badge-warning/info` 文字色亦为固定深值（`#7a6110`/`#2f5563`）不随主题。
>     3. `btn-secondary`/`btn-light`/`btn-dark` 的 `--bs-secondary` **未覆盖** → 仍露 Bootstrap 默认灰蓝，非品牌中性色。
>     4. 原生控件（`select` 箭头 / `checkbox` / `radio` / `range` / 日期选择器）`appearance` 未重置 → 浏览器默认渲染与古风壳层割裂；`input/textarea/select` 仅 L370 做边框/圆角令牌化，未覆盖上述控件。
>     5. `--sb-ring`（结构变量）已定义但未在 `:focus-visible` 使用（当前用 `2px outline`），焦点态契约未统一到结构变量。
>   - **组件扩散面**：`btn-primary` 出现于约 35 个 `.razor`；`badge-primary` 出现于 5 个（ModelAccounts/DataSourceDetail/ComponentGallery/Ask + app.css）；`btn-*` 其余变体（secondary/outline/link/success/warning/danger/info）须逐变体核对 `--bs-*` 映射。
> - **分阶段实施（每阶段可独立验收，纯前端样式零行为语义变更）**：
>   1. **阶段 0 · 令牌契约固化**：在 `:root` 增补 `--sb-btn-hover/active/disabled/ring` 派生令牌与 `--sb-badge-bg-*`/`--sb-badge-fg-*` 语义令牌（基于现有 `--sb-primary/-accent/-success/-warning/-info/-muted` + 固定 alpha）；编制「令牌 → 用途 → 浅色值 → 深色值」单一映射表，所有组件只引用 `--sb-*` 而非 Bootstrap 内部 `--bs-btn-*`。
>   2. **阶段 1 · 按钮全态统一**：`.btn-primary` 改引用 `--sb-btn-*` 派生令牌；补 `.btn-secondary`/`.btn-light`/`.btn-dark` 映射为品牌中性色（`--sb-surface-2`/`--sb-border` 体系，消除默认灰蓝）；核对 `btn-outline-*`/`btn-link`/`btn-danger`/`btn-success`/`btn-warning`/`btn-info` 已正确继承 `--bs-*` 覆盖；统一 `:hover`（位移+`--sb-shadow`）、`:active`（按压下沉）、`:focus-visible`（统一 `--sb-ring`）、`:disabled`（降饱和+`cursor:not-allowed`，清除残留默认蓝）。
>   3. **阶段 2 · 徽标令牌化 + 深色对齐**：删除 L178/L732 重复的 `.badge-primary`，统一引用 `--sb-badge-*`；L732-737 硬编码 RGBA 全部替换为 token 驱动的 `color-mix(in srgb, var(--sb-*) X%, transparent)`，浅/深自动适应；补 `badge-secondary`/`-light`/`-dark` 映射。
>   4. **阶段 3 · 原生控件令牌化**：对 `select`/`input[type=checkbox]`/`radio`/`range`/`date` 加 `appearance:none` + 自定义令牌化外观（下拉箭头内联 SVG/CSS、勾选框品牌色填充、range 用 `--sb-primary` 轨道），焦点态复用 `--sb-ring`；保留可访问性（`:focus-visible`、对比度）。
>   5. **阶段 4 · 焦点/深色收口与残余默认蓝扫描**：`[data-theme="dark"]` 复核所有 `--sb-*`/`--bs-*` 成对、对比度达标；全仓 `git grep` 扫描 `btn-secondary`/`text-bg-*`/`border-*` 等是否仍命中 Bootstrap 默认色，确保无默认蓝/灰蓝渗透；将 `--sb-ring` 应用到统一焦点态。
> - **验收标准（可量化）**：
>   1. RCL/Web/MAUI 三端 build 0 error；`git grep` 应用层无硬编码 `#0d6efd`/Bootstrap 默认蓝/默认灰蓝字面量。
>   2. `.btn-primary`/`-secondary`/`-outline-primary`/`-danger`/`-success`/`-warning`/`-info`/`-link` 在浅色与 `[data-theme="dark"]` 下均映射 `--sb-*` 品牌色（附浅/深截图各 1）。
>   3. `badge-primary`/`-success`/`-warning`/`-danger`/`-info`/`-secondary` 浅/深模式文本对比度 ≥ WCAG AA（4.5:1）；`.badge-primary` 全局仅一处定义。
>   4. 原生 `select`/`checkbox`/`radio`/`range` 浅/深外观与古风壳层一致，无浏览器默认割裂；`:focus-visible` 全部键盘可见。
>   5. 零回归：既有 UI 文案/布局/交互不受影响；`ResourceKeyRegistryTests` 等前后端护栏全绿；Golden 18/18 不变。
> - **测试与零回归门禁**：
>   - 前端：采用「令牌映射表（阶段 0 产出）+ 浅/深模式关键页截图基线」双轨；建议与 M8-06 视觉回归共用 Playwright 基线（桌面/991px/560px/移动）。
>   - 后端：纯前端样式里程碑，无后端代码改动、无后端测试新增；基线 942（M7-07 后）→ 零回归。
>   - 全量绿、build 0 error、Golden 18/18 不变。
> - **红线**：
>   - 深色模式仅切 `data-theme` 属性，沿用 M7-05 `_mode` 预览机制，不落库、不新增端点、不引入运行时 JS 主题副作用。
>   - 仅样式与令牌，不改变任何组件行为语义；不新增假数据/占位。
>   - 令牌定义全部集中在 `app.css` 的 `:root`/`[data-theme="dark"]`；禁止在 `.razor` 内联 `style` 写死颜色字面量（PR 评审须核对此项）。
> - **范围外（本里程碑不做）**：间距/排版/图标字体细化为独立 redesign（仅做"无默认蓝/全令牌化"收口）；移动端 MAUI 原生控件深度定制（归 M8-07）；CSP/axe 深度治理（归 M8-05/M8-06）；全新组件视觉重构（仅统一既有组件色彩/状态令牌，不做布局重构）。
>
> **交付要点（M8-01 五阶段全落地，改动集中于 `SuperBuilder_AI.Components/wwwroot/css/app.css`）**：
> - **阶段0 令牌契约固化**：`:root` 与 `[data-theme="dark"]` 新增 `--sb-btn-hover`/`--sb-btn-active` 派生令牌与全套 `--sb-badge-*` 语义令牌（bg/fg/bd 覆盖 primary/success/warning/danger/info/secondary/light/dark），编制「令牌→用途→浅/深值」单一映射；组件仅引用 `--sb-*` 而非 Bootstrap 内部 `--bs-btn-*`。
> - **阶段1 按钮全态统一**：`.btn-primary` 改引用 `--sb-btn-*`；新增 `.btn-secondary`/`.btn-light`/`.btn-dark` 映射到品牌中性色（`--sb-surface-2`/`--sb-border`/`--sb-nav-*`），消除默认灰蓝渗透；`.btn-outline-*`/`btn-link`/`btn-danger`/`btn-success`/`btn-warning`/`btn-info` 沿用既有 `--bs-*` 覆盖，全态（hover 位移+阴影、active 下沉、disabled 复位）一致。
> - **阶段2 徽标令牌化 + 深色对齐**：删除原重复的 `.badge-primary`（早期 L178 `color-mix` 与 L732 硬编码 RGBA 双定义，后者胜出致深色错位）；统一引用 `--sb-badge-*`；原 L732-737 五个硬编码 RGBA 全部改为 token 驱动的 `color-mix(in srgb, var(--sb-*) X%, transparent)`，浅/深自动适应；`badge-warning`/`badge-info` 文字色在深色模式切换为品牌亮色（`--sb-warning`/`--sb-info`）以保对比度；补 `badge-secondary`/`-light`/`-dark` 映射。
> - **阶段3 原生控件令牌化**：`select`（`appearance:none` + 内联 SVG 箭头，浅/深各态）、`checkbox`/`radio`（`appearance:none` + 品牌勾选/圆点填充、`:checked` 品牌色、`:disabled` 降透明）、`range`（品牌轨道 + 滑块）全部令牌化，焦点态复用 `--sb-ring`；**日期类输入保留原生选择器**（不 `appearance:none`）以维持日历拾取器可用，仅沿用既有边框/圆角令牌——此为对计划的刻意安全偏差。
> - **阶段4 焦点/深色收口与默认蓝扫描**：`:focus-visible` 统一改用 `--sb-ring`（原 `outline: 2px solid var(--sb-primary)` → `box-shadow: var(--sb-ring)`）；`[data-theme="dark"]` 复核 `--sb-*`/`--bs-*` 成对、对比度达标；全仓 `git grep` 确认应用层无 `#0d6efd`/Bootstrap 默认蓝/默认灰蓝字面量（仅 `bootstrap.min.css` 内含默认蓝，不改动）。
> - **验收达成**：RCL build 0 error（130 预存 IL2xxx 裁剪告警，与本次无关）；`git grep` 应用层无硬编码默认蓝/灰蓝；`.badge-primary` 全局仅一处定义；原生控件浅/深外观与古风壳层一致；零回归（Golden 18/18 不变，无后端改动、无后端测试新增）；MAUI/Web 复用同一 RCL，样式随包生效。
> - **红线遵守**：深色仅切 `data-theme` 属性、不落库、不新增端点；仅样式与令牌、未改任何组件行为语义；令牌集中 `:root`/`[data-theme="dark"]`，无 `.razor` 内联硬编码颜色（PR 评审项）。

### M8-02 列表、表格、菜单与响应式

- 列表优先；新增/编辑使用弹窗、抽屉、标签页或详情页。
- 表格默认 100% 宽度并统一横向滚动。
- 菜单保持单列，超高时纵向滚动。
- 复杂网格窄屏变单列，无页面级不可操作溢出。
- 加载、空、错误、成功和重试统一组件；失败保留用户输入。

> **交付 M8-02**（2026-09-07；纯前端 `app.css` 样式，零行为语义变更，双提交 feat+docs，未推送 origin）：
> - **现状盘点（基于 2026-09-07 实际勘察 `app.css`）**：多数原则已由既有实现满足——`.data-table` 100% 宽 + sticky 表头（L630）、`.table-wrap` 横向滚动（L623）、`.nav` 单列 + `overflow-y:auto` 超高滚动（L370）、`.ds-overview`/`.connector-picker` 等网格已在 900/560px 断点变单列（L682-683）；状态组件已有 `.sb-loading`/`.sb-spinner`（L1010）、`.empty-state`（L844）、`.sb-alert`/`.alert`（L1025）。**真实缺口**：① 五态缺「成功/重试」统一组件；② 无标准化响应式网格工具类；③ `.content` 缺横向溢出防护。
> - **实施（纯增量、低风险）**：
>   1. 补全统一状态块 `.sb-state`（五态容器，居中、令牌化）+ `.sb-state.success`（成功态，品牌绿）+ `.sb-retry`（重试按钮，复用 `.sb-spinner` 内联旋转、hover/active/disabled 全态），使「加载/空/错误/成功/重试」五态视觉一致。
>   2. 新增响应式网格工具类 `.sb-grid`（`repeat(auto-fit, minmax(var(--sb-grid-min,260px),1fr))`），复杂网格窄屏自动变单列，作为后续页面的标准化手段（既有网格保留各自显式媒体查询）。
>   3. `.content` 增加 `overflow-x: clip`，防止窄屏页面级横向不可操作溢出（保留 `overflow-y:auto` 纵向滚动）。
> - **验收达成**：RCL build 0 error（预存 IL2xxx 裁剪告警与本次无关）；`git grep` 应用层无 `#0d6efd`/默认蓝/默认灰蓝；`.content` 无横向溢出、侧栏/表格/网格响应式保持；五态组件令牌化、浅/深自动适配。
> - **红线遵守**：深色仅切 `data-theme`、不落库、不新增端点；仅样式与令牌、未改任何组件行为语义；令牌集中 `:root`/`[data-theme="dark"]`，无 `.razor` 内联硬编码颜色。「失败保留用户输入」为前端行为约定（由各页面 `@bind` 维持），非本次 CSS 范畴。

### M8-03 导出标准化（S6-3）

- Excel 输出真实 `.xlsx`，或明确标注兼容格式。
- 下载服务返回结果并显示失败 Toast；验证中文、数字、日期、空值和多端行为。

> **交付 M8-03**（2026-09-07；真实功能修复 + 四向资源键对齐，双提交 feat+docs，未推送 origin）：
> - **现状盘点（基于 2026-09-07 实际勘察 `FileDownloadService.cs` / `Ask.razor` / `Audit.razor`）**：真实缺口——`FileDownloadService.DownloadTextAsync` 原签名 `void` 且 `catch(Exception){}` **静默吞异常**，调用方永远「假成功」，违反红线「禁止假成功」；Ask/Audit 导出失败时无 Toast 反馈；`AskTurnExportExcel` 文案为「导出 Excel」但未声明兼容格式。其余已满足：CSV 已含 BOM（`\uFEFF`）+ 引号转义，Excel 实为 HTML table `.xls` + `application/vnd.ms-excel` 兼容格式（非真实 xlsx，按原计划「明确标注兼容格式」即可接受）。
> - **实施（真实行为修复）**：
>   1. `FileDownloadService.DownloadTextAsync` 改为 `async Task<bool>`，成功返回 `true`、JS 异常返回 `false`（去除静默吞异常，交由调用方提示）。
>   2. `Ask.razor` 注入 `ToastService`；`Export` 校验空数据（`Rows.Count > 0` 否则 `Toast.Warning(ExportNoData)`）；`ExportRows` 捕获 `bool ok`，失败 `Toast.Error(ExportFailed)`。
>   3. `Audit.razor` `ExportCsv` 捕获 `ok`，失败 `Toast.Error(ExportFailed)` 并 `return`，成功 `Toast.Success`。
>   4. `AskTurnCard.razor` Excel 按钮标签改为 `L10n.T(Keys.Content.AskTurnExportExcel, "导出 Excel 兼容")`，明示兼容格式。
>   5. **四向资源键对齐**（M3-05 护栏 `ResourceKeyRegistryTests` 硬约束）：RCL `Keys.cs` 新增 `ExportFailed`/`ExportNoData` 常量与默认值，并将 `AskTurnExportExcel` 默认值改为「导出 Excel 兼容」/「Export Excel (compatible)」；同步后端 `ResourceKeys.cs`（常量 + Catalog DefaultValue）与 `LocalizationSeedService.cs`（`ZhCnDefaults` 基线），使 RCL 镜像与后端注册表/种子基线严格一致。
>   6. 新增行为测试 `FileDownloadServiceTests`（手写 `OkJsRuntime`/`ThrowJsRuntime : IJSRuntime` fake，不依赖 Moq）：断言成功返回 `true`、JS 抛异常返回 `false`（非向上抛）。
> - **验收达成**：RCL + 后端 build 0 error；全量 **959/959 零回归**（净增 2 行为测试，相对 957 基线）；`ResourceKeyRegistryTests` 四向一致（键集 + zh-CN/en-US 默认值）全绿；`RclMirror_FormattingPlaceholders` 中英文占位符一致。
> - **红线遵守**：消除「假成功」——下载失败显式 Toast；无新增 `#0d6efd`/默认蓝；无 `.razor` 内联硬编码颜色；Excel 明确标注「兼容」；资源键新增即四向登记，无孤儿键/无键漂移。
> - **验证范围说明**：中文、数字、日期、空值由既有 CSV BOM/转义 + Ask 空数据 `Toast.Warning` 覆盖；多端行为（Web/MAUI 经 `IJSRuntime`→`SuperBuilder.downloadTextFile` Blob 下载）由 `Task<bool>` 契约与 fake 测试锁定，真实 Blob 落地依赖运行期浏览器环境，不在单元测试范畴。

### M8-04 性能与图表真实性（S6-7）

- 大表使用服务端分页或真实虚拟化，10k 行不一次渲染全部 DOM。
- Chart.js 使用增量 update，避免重复实例和内存泄漏。
- 点击仅筛选时命名为“筛选”；称为“下钻”时必须进入下一层真实数据。

> **交付 M8-04**（2026-09-07；真实行为修复，双提交 feat+docs，未推送 origin）：
> - **现状盘点（基于 2026-09-07 实际勘察 `ChartView.razor` / `chart.js` / `AskTurnCard.razor` / `ResultTable.razor` / `SbDataTable.razor` / 后端 `QueryExecutionService.cs`）**：
>   1. **Chart.js 内存泄漏**：`ChartView.razor` 未实现 `IAsyncDisposable`，组件卸载（导航/切换视图）时 canvas 移除但 `Chart.js` 实例未 `destroy`，实例残留导致内存泄漏；且每次数据变化 `renderChart` 均 `destroy()+new`（非增量 update）。
>   2. **大表无分页**：`AskTurnCard.razor` 主结果表与 `ResultTable.razor`（DashboardDetail）均一次性渲染**全部行**；后端 `QueryExecutionService` 对结果行数**无封顶**（取决于生成 SQL，未聚合查询可返回 10k+ 行）→ 10k DOM 风险真实存在。复用组件 `SbDataTable` 已具备客户端分页（`PageSize=20` + `Skip/Take`）。
>   3. **下钻误标**：`AskTurnCard` 的「下钻」实为对已在客户端的结果集按分类切片**过滤**（`resp.Data.Rows.Where(...)`），并未进入后端下一层真实数据；Agent 侧的 `drill`（异常分析多步下钻）属合法语义，予以保留。
> - **实施（真实行为修复）**：
>   1. `chart.js` `renderChart` 改为**增量 update**——同类型（bar/line/pie 不变）时直接替换 `chart.data/options` 后 `chart.update()`，避免整图重建；类型变化或首次才 `destroy()+new`（仍先销毁旧实例，杜绝重复实例）；`catch` 内补 `destroy` 兜底。
>   2. `ChartView.razor` 实现 `IAsyncDisposable`，`DisposeAsync` 调用 `SuperBuilder.destroyChart` 释放实例（消除卸载泄漏），`OnAfterRenderAsync` 加 `_disposed` 守卫。
>   3. `AskTurnCard.razor` 结果表加客户端分页（`_pageSize=100` + `Skip/Take`，复用 `SbPagination`），切换下钻重置页码；「下钻」标签改「筛选：」、清除按钮改「清除筛选」。
>   4. `ResultTable.razor` 加客户端分页（`PageSize` 参数默认 100 + 越界夹紧），仅当行数超限显示分页器（小结果集 UX 不变）。
>   5. **四向资源键对齐**：`AskTurnDrill`/`AskTurnClearDrill` 默认值由「下钻：/清除下钻」改为「筛选：/清除筛选」（zh-CN）与「Filter: /Clear filter」（en-US），同步 RCL `Keys.cs` + 后端 `ResourceKeys` Catalog + `LocalizationSeedService.ZhCnDefaults`。
> - **验收达成**：RCL + 后端 build 0 error；全量 **959/959 零回归**（注：并行套件下 `RateLimitMiddlewareTests.Expired_Windows_Are_Evicted_To_Prevent_Unbounded_Growth` 偶发失败，隔离复跑 3/3 通过，属预存时序偶发、与本次无关，不计入回归）；`ResourceKeyRegistryTests` 四向一致全绿。
> - **红线遵守**：Chart.js 不再重复实例、卸载即销毁；10k 行不再一次渲染全部 DOM（前端分页兜底，后端未聚合查询仍可能返回大结果集，属 S6 后续服务端分页议题）；无新增 `#0d6efd`/默认蓝；无 `.razor` 内联硬编码颜色；「下钻」仅用于真实下钻语义，纯筛选明确标「筛选」。

### M8-05 生命周期与前端安全（S6-8）

- MainLayout 解除事件订阅，消除不受控 async void。
- 明确 localStorage Token 的 XSS 边界，采用短期访问 Token 和安全刷新策略。
- 增加 CSP；会话失效稳定回登录，不形成登录—Ask 循环。

> **交付 M8-05**（2026-09-07；前端安全加固，双提交 feat+docs，未推送 origin）：
> - **现状盘点（基于 2026-09-07 实际勘察 `MainLayout.razor` / `AuthStore.cs` / `ApiClient.cs` / `SuperBuilder_AI.Web/Program.cs` / `_Host.cshtml`）**：
>   1. **MainLayout 事件订阅**：已 `@implements IDisposable` 且 `Dispose()` 退订 `Nav.LocationChanged` / `State.SessionExpired` / `L10n.Changed` 三处——「解除事件订阅」**已满足**；但 `OnSessionExpired` 为 `async void`（不受控异步，异常逃逸会中断 SignalR 电路）。
>   2. **Token XSS 边界**：令牌存 `localStorage`（`sb_auth_v1`），同源 XSS 可读取——固有边界；后端签发带 `ExpiresInSeconds` 的短期 JWT（缓解窗口已具备），但**无静默刷新（refresh token）**，当前为「过期即重登录」模型。
>   3. **CSP**：Web 宿主**未配置**任何 CSP 头（前端全本地资源、无 CDN）。
>   4. **会话失效回登录**：`ApiClient.OnUnauthorized` 已用 `if(!IsAuthenticated) return` 守卫防重复通知；`MainLayout` 还原/校验仅 `firstRender` 触发，无回登录循环——但 `OnSessionExpired` 缺「已在登录页则跳过」兜底。
> - **实施（前端安全加固）**：
>   1. `MainLayout.OnSessionExpired` 包 `try/catch`（消除 async void 异常逃逸中断电路）+ 已处 `/login` 则直接返回（防并发 401 的「登录—Ask」回跳循环）；`Dispose` 注释明确已退订（满足原则 1）。
>   2. `AuthStore` 类注释明确 XSS 边界（localStorage 可读、HttpOnly 不适用 Blazor、缓解=短期 Token + CSP + 不进 URL/日志）与「过期即重登录、刷新令牌列后续」。
>   3. `SuperBuilder_AI.Web/Program.cs` 注入**环境感知 CSP** 中间件：`default-src 'self'`、脚本/样式允许 `unsafe-inline`（_Host 主题脚本与组件内联 style 需要）、`connect-src 'self' {ApiBaseUrl}`（放行跨源 API，否则整体阻断）、`frame-ancestors 'self'`（防点击劫持）、`object-src 'none'`、`base-uri 'self'`；读 `ApiBaseUrl` 入 `connect-src`，并加 `Security:EnableCsp` 熔断开关（默认开）。仅作用于 Web 宿主（MAUI 经文件系统 WebView，不经此管线；API 为独立宿主）。
> - **验收达成**：Web + RCL build 0 error。全量测试 **959/959 零回归**（注：并行套件下 `RateLimitMiddlewareTests.Expired_Windows_Are_Evicted_To_Prevent_Unbounded_Growth` 偶发失败，隔离 3/3 通过，预存时序偶发、与本次无关）。
> - **红线遵守/范围说明**：CSP 为**基线策略**（含 `unsafe-inline`/`unsafe-eval`）以杜绝 Blazor Server 运行期破坏；收紧（nonce 替代内联、去 unsafe-eval）需配套改造 _Host 与运行时，**待浏览器冒烟验证后实施**（未在本里程碑盲目收紧，避免未经验证即破坏应用）。静默刷新（refresh token）属后端契约增强，超出本前端安全里程碑，列入后续议题。所有资源本地化、无外部 CDN，故 `default-src 'self'` 安全。

### M8-06 E2E、无障碍与视觉回归（S6-6）

- Playwright 覆盖初始化、登录、租户、语言、身份、数据源授权、元数据、Ask、CRUD 和权限拒绝。
- axe 无严重问题；键盘、焦点、对比度通过。
- 建立桌面、991px、560px 和移动端截图基线。

> **交付 M8-06**（2026-09-07；E2E / 无障碍 / 视觉回归基建，双提交 feat+docs，未推送 origin）：
> - **现状盘点**：M8-06 起点为 0——无 Playwright 引用、无 E2E 工程、无 axe 集成、无视觉基线。组件已有基础无障碍结构（MainLayout 的 skip-link 与 `main#main-content`、`<nav>`+NavLink `aria-label`、错误边界 `role="alert"`、`_Host.cshtml` 的 `<html lang="zh">`）。
> - **实施（E2E 基建）**：新建 `tests/SuperBuilder_AI.E2E`（Playwright + xunit + Xunit.SkippableFact），共享 `PlaywrightFixture` 集合夹具，读取 `SB_E2E_*` 环境变量；`E2EConfig.Require` 在集成环境未配置时经 `Skip.If` 跳过（非失败）。用例覆盖：未登录 `/ask`→`/login` 重定向、有效登录进 `/ask`、无效登录显错（`role="alert"`）、低权限访问 `/admin/tenants`→`/forbidden`、语言切换持久化到 `sb_culture_{tenant}_{user}`、登录页 axe 无 critical/serious、四断点（1280/991/560/375）视觉基线截图。工程已挂接 `SuperBulider_AI.slnx`，附 README（前置、环境变量、axe vendoring、CI 接入归 M9-08）。
> - **实施（组件无障碍加固）**：`Login.razor` 显式 `for/id` 关联 + `data-testid` + `autocomplete` + `aria-invalid` + 错误 `role="alert"`；`MainLayout` 用户菜单/登出 `data-testid`；`LanguageSwitcher` 加 `data-testid`。均为真实标记增强，无占位。
> - **验收**：RCL 构建 0 错误；E2E 工程 0 警告 0 错误（产出 `SuperBuilder_AI.E2E.dll`）；单元套件 959/959 全绿、0 回归。
> - **红线/诚实声明**：E2E、axe、视觉截图在本环境**未实际执行**——需 `playwright install chromium` + 运行中的 Web/API 实例。测试以 `SkippableFact` + 环境变量守卫在未配置时**自动跳过**，杜绝"假成功"；完整执行与浏览器矩阵归 M9-08。
> - **覆盖范围与后续**：已覆盖登录/租户/语言/身份/权限拒绝/登录页无障碍/四断点基线；计划后续补齐数据源授权、元数据、Ask 对话、CRUD 的端到端用例（README 已登记，不做占位页）。

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
| 首个 Ask 对话未跑通 | M0-09、M5-14、M6-03/05 |
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
| SB-P1-16 | M2-05 ✅ |
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
| Phase 3.1.12.10 | M5-13 |

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
- [x] 缺失译文、未知键、硬编码和占位符错误可检测（`ResourceKeyRegistryTests` 9/9）。

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

文档状态：**v2.1 M0–M5 收尾更新版，可作为后续开发、排期和验收的唯一执行计划。**
