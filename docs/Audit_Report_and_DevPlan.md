> **治理声明**：本文档是 `Master_Development_Plan.md` 的**输入 / 审计基线**，**不是**独立执行计划。所有里程碑状态、优先级与验收以 `Master_Development_Plan.md`（唯一事实来源）及其 `milestones/` 拆分文档为准；本文与 MDP 冲突时以 MDP 为准。映射见 MDP §17。
>
> **文档角色**：全量源码审计报告（技术发现映射到 M1/M3 等，见 MDP §17.5）

# SuperBuilder AI 当前代码审计报告与后续开发计划

> 首次形成：2026-09-03  
> 最近核验：2026-09-03  
> 审计方式：当前工作区静态代码核验、前后端路由交叉检查、初始化与权限链路检查、完整自动化测试  
> 当前测试基线：**431/431 通过，0 失败**  
> 说明：本文描述当前工作区（包含尚未提交的在制改动），状态会随代码继续变化。

---

## 0. 执行摘要

系统的身份认证、租户隔离、平台管理员初始化、租户创建、数据源管理、数据源授权、Ask 多数据源范围和多语言分层模型已经具备基础闭环，但距离生产发布仍有明确缺口。

当前最重要的问题是：

| 严重度 | 编号 | 问题 | 当前影响 |
|---|---|---|---|
| 高 | SEC-1 | 配置文件存在数据库及模型服务明文凭据 | 凭据泄露，需要迁移密钥并执行轮换 |
| 高 | C-1 | 4 个前端页面仍存在前后端路由不匹配 | Agent、业务实体详情和语义标签详情可能 404 |
| 高 | SEC-2 | PermissionGuard/NavMenu 在权限集合为空时放行 | 权限尚未加载或加载失败时可能错误显示受限入口 |
| 高 | INIT-1 | 启动过程没有受控执行 EF Migration | 全新数据库不能可靠启动 |
| 高 | L10N-1 | 全站绝大多数可见文本仍为硬编码中文 | 切换语言不能覆盖所有页面内容 |
| 高 | L10N-2 | AuthController 仍只保留 zh-CN/en-US | 平台维护的其他语言登录后会被裁剪 |
| 中 | ASK-1 | Ask 普通提交缺少完整 try/finally、超时和取消治理 | 网络异常后页面可能保持 Busy 状态 |
| 中 | ASK-2 | Ask 待澄清会话使用单机内存 | 服务重启丢失，多实例无法共享 |
| 中 | IAM-1 | 尚不支持通过管理页面添加第二个平台管理员 | 不满足多平台管理员产品要求 |
| 中 | FLOW-1 | Agent、BusinessModel、ThemeEditor 等仍有占位操作 | 页面存在但操作未形成落盘闭环 |

主观就绪度仅用于排期参考：

- 后端治理骨架：约 85%。
- 租户、数据源与授权闭环：约 80%。
- 页面文件覆盖：约 85%，但仍有契约和占位问题。
- 初始化与部署韧性：约 60%。
- 多语言运行框架：约 60%；全站文本接入率仍低。
- 自动化单元/组件测试：当前 431 项全部通过；缺少真正端到端测试。

---

## 1. 审计范围与统计口径

### 1.1 当前范围

- 前端页面：`SuperBuilder_AI.Components/Components/Pages` 下约 30 个路由页面。
- 前端整体：Pages、Layout、Shared 等组件共 68 个 Razor 文件包含中文文本。
- 后端：`src/Api/Controllers` 下 17 个 Controller，以及相关 Application、Domain、Infrastructure。
- 数据层：SuperBIContext、EF Migration、初始化种子和租户查询过滤。
- 产品基线：`Platform_Product_Development_Requirements.md`。

### 1.2 当前工作区

最近核验时工作区约有 49 项变更，其中约 14 个新文件。该数字只是审计时点快照，不作为长期验收指标。

### 1.3 测试状态

最近一次完整测试：

```text
失败 0，通过 431，跳过 0，总计 431
```

因此，旧版报告中“在制改动未经过全量测试”的描述已经失效。

---

## 2. 已完成或已形成基础闭环的能力

### 2.1 平台管理员初始化

- 登录页能够检测是否存在平台管理员。
- 不存在时显示一次性初始化表单。
- 初始化创建仅允许本机访问。
- 首位平台管理员创建后初始化入口关闭。
- 配置型自动初始化在配置有效时仍可使用。

当前不足：

- 初始化表单尚未完整覆盖邮箱等平台管理员资料。
- 尚无正式的第二个平台管理员创建入口。
- Bootstrap 执行没有完整的启动异常保护。

### 2.2 租户管理

- 平台管理员能够创建租户和首位租户管理员。
- 租户、管理员、初始密码和语言设置位于同一事务。
- 租户页面支持启用、停用、设置及编辑。
- 已创建租户可修改名称、支持语言和默认语言。
- 租户语言只能从平台已启用语言中选择。

### 2.3 多语言分层

已建立：

1. `UiLanguages`：平台语言目录。
2. `UiTextResources(TenantId=0)`：平台文本基线。
3. `UiTextResources(TenantId>0)`：租户专属覆盖。
4. 租户支持语言和默认语言。
5. 用户语言偏好按租户和用户缓存。
6. 租户覆盖文本可恢复为平台继承值。

范围规则已经调整为：

- 平台管理员看到全部平台语言，只维护平台文本基线。
- 租户管理员只看到本租户获授权的语言。
- 租户管理员同时看到平台默认值和租户最终值。
- 登录前公共读取接口与登录后管理接口已经分离。

### 2.4 登录租户与语言选择

- 登录页先选择平台管理或具体租户。
- 选择租户后加载该租户支持语言和默认语言。
- 上次选择的登录租户保存至浏览器缓存。
- 再次进入登录页时优先恢复仍然有效的上次租户。

### 2.5 数据源与授权

- 数据源管理页默认展示列表，新增表单按需展开。
- 新建数据源自动授予创建者访问权。
- 数据源详情提供用户和角色授权入口。
- 租户管理员可以授予及撤销数据源访问权限。
- 普通用户的数据源范围由用户授权和角色授权合并计算。
- 拥有元数据管理权限的租户管理员默认可访问本租户全部启用数据源。

### 2.6 Ask 数据源范围

- Ask 页面不再要求用户手动选择单个数据源。
- 页面显示当前账号全部可访问数据源。
- 请求使用 `DataSourceId=0`，后端在全部授权数据源中自动规划。
- 每次查询仍重新校验当前授权范围。
- 多数据源查询绕过不安全的旧单源缓存路径。

### 2.7 Ask 待澄清会话

已增加：

- `ConversationId`。
- `ConversationStatus`。
- `RewrittenQuestion`。
- Medium/RequiresConfirmation 结果的待澄清状态。
- 下一条普通输入自动作为上一问题的补充说明。
- 会话绑定 TenantId 和 UserId，禁止跨租户或跨用户复用。
- 查询完成或清空对话后清除待澄清状态。
- 当前过期时间为 30 分钟。

示例：

```text
原问题：给我最近的十张入库单
用户补充：入库单就是入库凭证
后端行为：以原问题和补充说明重新生成完整查询，而不是单独分析第二句话
```

当前限制：状态保存在单机进程内存中，尚未使用 Redis 或数据库。

### 2.8 元数据关系展示

- 数据源详情显示 MetadataTable、MetadataColumn 和 MetadataSemantic。
- 布尔值使用开关样式。
- MetadataColumn、MetadataSemantic 和 Vector/Qdrant Point 具有独立详情路由。
- 详情查询校验当前租户和元数据查看权限。

---

## 3. 已确认的功能缺陷

### 3.1 C-1：4 个前后端契约不匹配

| 前端调用 | 后端现状 | 结果 |
|---|---|---|
| `GET api/agent?tenantId=...` | 后端为 `GET api/agent/plans` | Agent 列表 404 |
| `GET api/agent/tools-catalog` | 后端为 `GET api/agent/tools` | Agent 工具目录 404 |
| `GET api/business-model/entities/{id}` | 无 Get-by-id | 业务实体详情 404 |
| `GET api/semantic-labels/{id}` | 无 Get-by-id | 语义标签详情 404 |

建议：统一使用语义清晰的 REST 路由，并增加契约测试，避免仅靠人工同步 URL。

### 3.2 FLOW-1：存根和占位操作

- ModelAccounts：模型数据硬编码，绑定操作未落盘。
- Admin/Themes：仍以空状态为主。
- Agent：部分创建、运行行为没有完整运行时支持。
- BusinessModel：创建入口仍有占位性质。
- DataSources TestConn：连接测试尚未形成正式 Connector 闭环。
- ThemeEditor：保存与后端主题接口尚未完整接通。

原则：如果后端能力尚未实现，应明确禁用并标注依赖，不能使用成功 Toast 模拟完成。

---

## 4. 初始化与部署审计

### 4.1 INIT-1：缺少受控 Migration

当前启动代码会执行身份和配额种子，但没有执行 `Database.Migrate()` 或等价的受控升级工具。

准确的失败场景是：

- 全新数据库没有表结构时，Identity 种子异常会被捕获。
- 随后的 PlatformAdminBootstrapper 没有 try/catch。
- 如果表、平台租户或平台角色不存在，内部 `SingleAsync` 会导致启动失败。
- 单纯“没有平台管理员”不会抛异常，而是正常进入首次初始化流程。

建议：

- 生产环境使用独立迁移任务或明确授权的启动迁移。
- Migration 成功后再执行 Identity、Quota、Localization 和 Bootstrap 种子。
- 所有初始化步骤记录结构化状态，不允许静默跳过关键失败。

### 4.2 初始化时序

UiLanguages 当前由 LocalizationController 懒初始化。租户创建依赖该目录，但如果多语言端点从未被访问，语言目录可能为空，服务端语言白名单校验会退化。

建议将语言目录与文本基线纳入正式启动种子，消除控制器 GET 请求触发写入的行为。

### 4.3 Migration 命名

- `AddUiLocalizationGovernance` 是空迁移。
- 真正建表的是 `SyncUiLocalizationModel`。

功能可运行，但迁移命名和内容不匹配，影响维护与回滚审计。

### 4.4 演示数据

目前没有完整的可选演示租户、演示数据源、元数据、业务模型和仪表盘种子。

建议提供显式的 Demo 安装命令或管理操作，不要在生产启动时默认写入演示业务数据。

---

## 5. 身份、权限与安全审计

### 5.1 SEC-1：明文凭据

`appsettings.json` 中存在数据库密码和模型服务 API Key 配置。该问题优先级最高。

必须：

1. 将凭据迁移至环境变量、Secret Manager 或正式密钥服务。
2. 轮换已经进入代码仓库历史的真实凭据。
3. 日志和错误响应执行脱敏。
4. 不仅删除当前文件内容，还要评估 Git 历史泄露。

### 5.2 SEC-2：权限守卫 fail-open

NavMenu 和 PermissionGuard 当前在权限集合为空时放行。这会混淆以下状态：

- 权限尚未加载。
- 权限加载失败。
- 用户确实没有任何权限。

建议 AppState 增加：

- `PermissionsLoading`
- `PermissionsLoaded`
- `PermissionsLoadFailed`

只有明确加载成功且拥有权限时才显示受限内容。最终授权仍以后端为准。

### 5.3 IAM-1：多平台管理员

当前 Bootstrap 只负责首位管理员，IdentityService 会拒绝正常授予 `platform-admin` 角色。因此尚不支持第二个平台管理员。

建议增加独立的平台治理管理员管理接口和页面，并为管理员设置可管理租户范围。

### 5.4 匿名登录选项

`login-options` 会公开已启用租户的 ID、编码、名称和语言设置。这是当前“登录前选择租户”功能的实现基础，但存在租户枚举风险。

需要产品与安全选择：

- 公共 SaaS：改为输入租户代码后精确解析，不公开租户列表。
- 私有部署：可以保留下拉列表，但应支持配置关闭。

### 5.5 限流

如果限流键允许直接信任客户端任意提供的 API Token 头，则攻击者可以不断更换值规避单机限流。

建议优先使用已认证 TenantId/UserId，其次使用受信客户端标识，匿名请求使用 IP，并在多实例部署时迁移至分布式限流存储。

---

## 6. 多语言审计

### 6.1 当前三套资源

1. 后端 `PlatformStrings` 静态资源。
2. 数据库 `UiLanguages + UiTextResources`。
3. 前端 LocalizationService 内置兜底字典。

三者键空间尚未统一，长期会造成相同文本有多个事实来源。

### 6.2 L10N-1：页面接入不足

当前整个 Components 范围约 68 个 Razor 文件包含中文文本，仅约 5 个文件使用 `L10n.T`。

已接入的公共区域包括：

- 登录页部分内容。
- MainLayout 部分内容。
- NavMenu。
- AppBreadcrumb。
- PageHead 标题和描述约定。

仍需迁移：

- 页面按钮、表单标签、占位符、空状态、错误文本。
- 弹窗和确认文本。
- 业务页面中的提示及动态消息。
- 共享组件中的默认文案。

### 6.3 L10N-2：登录后语言裁剪

AuthController 当前仍执行类似：

```csharp
available.Where(x => x is "zh-CN" or "en-US")
```

这会导致平台虽然维护了 zh-TW、ja-JP 或 ko-KR，登录后仍然只保留中英文。必须改为查询 UiLanguages 中已启用语言，并与租户授权语言取交集。

### 6.4 文本保存即时生效

多语言管理页保存文本后会刷新列表，但不会通知当前 LocalizationService 重新加载。因此修改后的文本通常要切换语言或重新登录后才会在其他区域生效。

建议增加资源版本或 Changed 事件，保存成功后刷新当前文化资源。

### 6.5 缺失翻译治理

当前新增文本键主要作用于当前语言，缺少：

- 各语言缺失数量。
- 未翻译过滤器。
- 基线键覆盖率。
- 发布前完整性检查。

建议将“资源键目录”和“各语言文本值”分开建模，资源键创建一次，各语言分别填写。

### 6.6 租户文本隔离

UiTextResource 没有全局查询过滤，当前依赖 Controller 手动限定 TenantId。现有接口已经进行范围过滤，但缺少数据层纵深防御。

---

## 7. Ask 审计

### 7.1 已完成

- 全授权数据源自动规划。
- 数据源权限指纹参与缓存。
- 行级安全策略指纹参与缓存。
- Medium Confidence 待澄清会话。
- 会话租户和用户隔离。
- 查询完成后清理待澄清状态。

### 7.2 ASK-1：前端异常恢复

普通 `DoAsk` 仍然需要完整的：

```csharp
try
{
    // request
}
catch (...)
{
    // user-facing error
}
finally
{
    Busy = false;
}
```

还应增加 CancellationToken、请求超时、防重复点击和页面销毁取消。

### 7.3 ASK-2：会话存储

AskConversationService 当前为 Singleton + ConcurrentDictionary：

- 单实例有效。
- 应用重启丢失。
- 多实例请求漂移时上下文失效。
- 无集中运维和审计能力。

生产建议迁移到 Redis；如需长期审计，再将最终会话摘要异步写入数据库。

### 7.4 澄清重写质量

当前实现通过结构化提示将原问题和补充说明交回 QueryUnderstanding。后续还应增加：

- 新问题、确认、术语映射、条件补充、条件修改、取消的行为分类。
- 明确的候选实体列表。
- 租户语义别名审核和学习流程。
- 澄清次数上限和循环检测。

---

## 8. 架构与性能判断修正

### 8.1 DataSources Manage 不是已证实的 N+1

统计字段位于 EF 投影相关子查询中，通常会被生成到一条 SQL。可能存在性能退化，但不能仅凭源码定性为 N+1。

正确验证方式：记录生成 SQL、实际执行计划、逻辑读取和数据规模压测。

### 8.2 DataSource 创建不传 TenantId 是正确设计

DataSources 前端创建请求不传 TenantId 并非缺陷。TenantId 应由认证令牌解析，避免信任客户端提交的租户范围。

### 8.3 租户过滤仍有 opt-in 风险

SuperBIContext 的 ApplyTenantScope 需要控制器或应用服务显式调用。新增端点如果遗忘调用，可能扩大查询范围。

建议逐步采用统一租户上下文、Controller Filter 或仓储边界，并保留服务端所有权条件作为第二道防线。

---

## 9. 字段级审计

本节以“字段定义 → 数据库约束 → API 输入输出 → 页面展示/编辑 → 租户隔离”为一条完整链路。风险等级不是按字段数量判断，而是按字段被伪造、遗漏或形成脏数据后的业务影响判断。

### 9.1 通用字段与时间规范

| 实体/字段 | 当前定义 | 已有约束 | 字段级风险 | 整改要求 |
|---|---|---|---|---|
| `BaseEntity.Id` | `long` | EF 主键 | 规则清晰 | API 不接受客户端回写；详情和外键跳转统一使用该值 |
| `BaseEntity.CreatedTime` | `DateTime`，默认 `DateTime.Now` | 无统一数据库默认值 | 与多个实体使用 `DateTime.UtcNow` 不一致，跨时区排序和审计可能偏移 | 全库统一 UTC；明确 `datetime2` 精度；响应层按用户时区显示 |
| 多数实体缺少 `UpdatedTime` | 未定义 | 无 | 无法可靠判断数据最后修改时间 | 对可编辑主数据增加 `UpdatedTime`，由服务端维护 |
| 多数实体缺少并发字段 | 未定义 | 无 | 两个管理员同时编辑时后保存者静默覆盖前者 | 对租户、语言、文本、角色、数据源、元数据语义增加 `rowversion` 或等价 ETag |
| 多数实体缺少软删除字段 | 未定义 | 依赖物理删除/状态字段 | 删除策略在模块间不一致 | 主数据统一 `Status/DeletedTime/DeletedBy` 语义；关联表可物理删除 |

验收：持久化时间均为 UTC；API 返回 ISO 8601；并发冲突返回 `409`，而不是静默覆盖。

### 9.2 租户与租户设置字段

| 字段 | 当前定义/来源 | 当前问题 | 目标约束与行为 |
|---|---|---|---|
| `Tenant.TenantCode` | `string?`；数据库只有唯一索引 | 控制器会校验非空，但实体与数据库未声明必填和长度；旁路写入可能产生空值 | `NOT NULL`，建议 `varchar(64)`；保存前 trim；采用稳定的小写编码规则；创建后默认不可修改 |
| `Tenant.TenantName` | `string?` | 创建时允许空字符串；无最大长度 | `NOT NULL`，建议最大 128；创建和编辑均校验非空 |
| `Tenant.Enabled` | `bool`，默认 true | 状态只有布尔值，无法表达暂停原因和生命周期 | 短期保留；增加停用原因、停用时间和操作者审计；停用后禁止登录与刷新令牌 |
| `TenantSetting.TenantId` | `long` | 有外键和级联删除 | 保留；所有写接口必须从管理目标或令牌解析，不接受普通租户客户端任意指定 |
| `TenantSetting.Key` | 最大 128，同租户唯一 | `UpsertTenantSettingRequest.Key` 可传任意键 | 建立允许键目录或按权限限制命名空间，禁止覆盖安全敏感配置 |
| `TenantSetting.Value` | 可空、数据库长文本 | 语言列表以 JSON 字符串存储，数据库无法校验成员 | 短期写入时做 JSON Schema/类型校验；中期将租户语言拆为关系表 |
| `TenantSetting.DataType` | 最大 32，自由文本 | 可写入任意类型名，消费端解释不一致 | 枚举限定 `string/int/bool/json`，并验证 `Value` 与类型匹配 |
| `localization:availableCultures` | JSON 数组 | 无外键保证语言来自 `UiLanguage`；语言停用后可能留下悬挂授权 | 建议建立 `TenantUiLanguage(TenantId, UiLanguageId, Enabled, SortOrder)` |
| `localization:defaultCulture` | 字符串 | 只能靠业务代码保证属于可用语言 | 关系化后以复合约束保证默认语言属于租户授权集合；每租户只允许一个默认值 |

### 9.3 用户、角色与权限字段

| 字段/组合 | 当前约束 | 风险或歧义 | 整改要求 |
|---|---|---|---|
| `User.TenantId` | 有索引，无显式 Tenant 外键 | 可能产生不存在租户的用户 | 增加 Tenant 外键，删除租户时 Restrict；租户注销走归档流程 |
| `User.Username` | 全局唯一，最大 128 | 登录页面已经先选租户，但两个租户不能使用相同用户名；与租户命名空间模型不完全一致 | 产品确认后建议改为唯一 `(TenantId, NormalizedUsername)`；平台管理员仍属于平台租户 |
| `User.Username` 规范化 | 仅字符串 | 大小写、前后空格、Unicode 等价字符可能导致登录歧义 | 增加 `NormalizedUsername`；统一 trim、大小写和 Unicode 规范化 |
| `User.DisplayName` | 必填，最大 128 | 创建 DTO 可空，由服务层兜底，规则需固化 | 明确默认取用户名；页面显示与审计 Actor 分开 |
| `User.Email` | 最大 256，可为空语义但 CLR 为非空字符串 | 无格式、规范化、唯一性或验证状态 | 增加 `NormalizedEmail`、`EmailConfirmed`；是否租户内唯一由产品决定 |
| `User.PasswordHash` | 最大 256，可空 | 首次管理员有口令，普通 `CreateUserRequest` 没有密码字段，账号如何激活不清晰 | 明确邀请/设密/重置流程；任何可登录用户不得长期保留空哈希 |
| `User.SecurityStamp` | 最大 64，可空 | 空值时令牌吊销语义不稳定 | 创建用户时必生成；改密、停用、角色敏感变更时轮换 |
| `User.Status` | 枚举 | API 仅输出字符串，状态转换规则未形成统一契约 | 限定状态机；停用即时拒绝新登录并使现有令牌失效 |
| `Role(TenantId, Code)` | 唯一 | `TenantId=0` 表示全局角色，但缺少 Tenant 外键是设计特例 | 保持哨兵设计时必须集中封装，禁止租户创建 `platform-admin` 等保留码 |
| `Permission(TenantId, Code)` | 唯一 | 同上 | 权限码由平台目录维护，不允许普通页面任意创建系统权限 |
| `UserRole(TenantId, UserId, RoleId)` | 组合唯一 | 当前模型配置未显式展示 User、Role 外键及“租户一致性”数据库约束 | 增加外键；服务层校验用户租户、角色租户为本租户或全局，并加入跨租户负向测试 |
| `RolePermission(TenantId, RoleId, PermissionId)` | 组合唯一 | 同样依赖应用层保证租户范围 | 增加外键和租户一致性校验；全局角色变更仅允许平台治理身份 |
| `CreateUserRequest.TenantId` | 客户端提交 | 曾出现 `tenantId > 0` 错误，且存在篡改风险 | 租户管理员接口忽略请求中的 TenantId，统一取 JWT `tid`；平台代管接口使用独立路由和显式管理目标 |
| `CreateRoleRequest.TenantId` | 客户端提交 | 与上项相同 | 按调用面解析租户，不信任普通请求体 TenantId |

### 9.4 多语言字段

| 字段/组合 | 当前约束 | 风险 | 整改要求 |
|---|---|---|---|
| `UiLanguage.Culture` | 必填、最大 16、全局唯一 | 格式仅靠业务逻辑 | 写入时按 BCP 47 归一化并拒绝非法值；数据库保存规范化结果 |
| `UiLanguage.DisplayName` | 必填、最大 64 | 含义是平台显示名，但和 NativeName 的使用边界需统一 | 管理页面同时展示“平台名称/本地名称/代码/启用状态” |
| `UiLanguage.NativeName` | 必填、最大 64 | 同上 | 语言切换器优先显示 NativeName |
| `UiLanguage.Enabled` | bool | 停用后对已授权租户如何处理不明确 | 禁止直接停用仍被租户使用的语言，或提供影响清单与迁移默认语言动作 |
| `UiLanguage.SortOrder` | int | 无范围及重复治理 | 允许重复但增加稳定次级排序 `Culture`；页面支持拖动或数字排序 |
| `UiTextResource.TenantId` | `0` 表示平台，正数表示租户 | 无 Tenant 外键是哨兵设计；容易写错作用域 | 保存端从身份与目标租户推导；平台页面强制 `0`，租户页面强制 JWT `tid` |
| `UiTextResource.Culture` | 最大 16 | 无 UiLanguage 外键，可能出现无效语言 | 增加逻辑/关系约束；租户文本还必须验证该语言已授权给租户 |
| `UiTextResource.ResourceKey` | 最大 160 | 自由字符串，重命名会形成废弃键 | 建立资源键目录、模块、默认文本和弃用状态；CI 扫描代码引用 |
| `UiTextResource.Value` | 必填、最大 2048 | 无占位符一致性校验，可能破坏 `{0}` 等格式化文本 | 保存时比较平台基线中的占位符集合；不一致则拒绝或告警 |
| `UiTextResource.Description` | 最大 256 | 无模块/页面归属字段 | 增加 `Module/Page/Context` 或在资源键目录中维护，便于筛选和翻译 |
| 唯一键 `(TenantId,Culture,ResourceKey)` | 已有 | 能保证单值，但不能保证 Culture 获授权 | 保留并补授权校验 |
| 租户继承 | 缺少租户行时回退平台 `TenantId=0` | 需要区分“未覆盖”和“刻意设为空” | Value 不允许空白；删除租户覆盖即恢复继承；页面明确显示来源与有效值 |

### 9.5 数据源与授权字段

| 字段/组合 | 当前定义 | 风险 | 整改要求 |
|---|---|---|---|
| `DataSource.TenantId` | `long?` | 租户数据源可无租户；查询过滤还需 `HasValue` | 改为 `long NOT NULL` 并加 Tenant 外键；若需要平台模板，另建连接器模板而非空租户数据源 |
| `DataSource.Name` | `string?` | 可空、无长度、无租户内唯一约束 | 必填，最大 128；唯一 `(TenantId, NormalizedName)` |
| `DataSource.DbType` | `string?` | 自由字符串，大小写和别名不统一 | 使用连接器类型枚举/目录；API 返回稳定 code 和本地化 displayName |
| `DataSource.ConnectionString` | `string?` 明文持久化 | 数据库备份、日志或管理接口泄漏即暴露业务库凭据 | 使用密钥库或信封加密；响应 DTO 永不返回原值；编辑页只显示掩码和“已配置” |
| `DataSource.Enabled` | `bool?` | null 的含义不明确 | 改为非空 bool；补 `LastTestStatus/LastTestTime/LastErrorCode`，错误消息脱敏 |
| `CreateDataSourceRequest.ConnectionString` | 客户端明文提交 | 必要但敏感；可能被日志记录 | 禁止请求体日志；限制长度；连接测试超时；保存前加密；错误不得回显连接串 |
| `DataSourceAccessGrant.TenantId` | long | 未与 DataSource 形成复合 FK | 授权服务已校验，数据库仍建议通过复合候选键或触发/服务约束保证同租户 |
| `SubjectType + SubjectId` | 多态主体 | 无 User/Role 外键，删除主体后可能留下孤儿授权 | 删除用户/角色时同步撤销；定期完整性检查；列表返回主体名称和失效标记 |
| 授权唯一键 | `(TenantId,DataSourceId,SubjectType,SubjectId)` | 合理 | 保留；重复授权必须幂等 |

### 9.6 元数据、语义与向量字段

| 字段/关系 | 当前状态 | 字段级风险 | 整改要求 |
|---|---|---|---|
| `MetadataTable.TenantId` | 非空 | 与 `DataSource.TenantId` 的一致性无数据库复合约束 | 扫描写入时强制继承 DataSource 租户；增加跨租户一致性测试/巡检 |
| `MetadataTable.DataSourceId` | 非空 FK | 正确 | 删除数据源的行为需明确并测试，避免元数据残留或误级联 |
| `MetadataTable.TableName` | `string?`；与 DataSourceId 唯一 | 可空且未配置长度；SQL Server 唯一索引对 null 的行为会掩盖脏数据 | 必填；按目标数据库最大标识符长度设限；另存 Schema/Catalog，唯一键应含 Schema |
| `MetadataColumn.MetadataTableId` | `long?` | 可形成无所属表的孤儿字段，与产品关系模型不符 | 改为非空 FK；历史空值先清洗 |
| `MetadataColumn.ColumnName` | `string?`；表内唯一 | 可空、无长度 | 必填并设长度；保留原始名称，另设规范化搜索名称 |
| `Length` | `long?` | 无精度、标度字段，无法完整描述 decimal 等类型 | 增加 `Precision/Scale/Ordinal/NativeType` |
| `IsNullable/IsPrimaryKey` | `bool?` | 元数据扫描后不应出现未知三态，页面也难区分 null | 若确需“未知”，页面显示未知；否则落库归一为非空 bool |
| `BusinessKey` | 可空 | 名称易与业务实体键概念混淆 | 明确它是稳定字段标识还是业务主键；必要时更名 `StableKey` |
| `MetadataSemantic.MetadataColumnId` | `long?`，唯一，一对一级联 | 可生成无字段语义记录；“一对一”只对非空值有效 | 改为非空；详情页用此 FK 跳转 MetadataColumn |
| `BusinessMeaning/Keywords/Synonyms/ExampleQuestions` | 多个自由字符串 | 多值内容可能用分隔符存储，难去重、排序和多语言化 | 结构化为子表或 JSON 数组并定义 schema；展示层用标签/列表而非原始字符串 |
| `Confidence` | `decimal(5,4)?` | 数据库范围仍可写负数或大于 1 | 增加 `0 <= Confidence <= 1` 检查约束；页面显示百分比和来源 |
| `Source` | 自由字符串 | 来源值漂移 | 枚举 `AI/Manual/Imported/Learned`，人工编辑时保留原来源及审计 |
| `SearchText` | 可空长文本 | 可能包含业务敏感字段/样本内容 | 明确生成规则、脱敏规则和重建版本；页面默认折叠 |
| `VectorId` | 可空字符串 | Qdrant 点位删除或重建后可能悬挂 | 增加索引状态、最后同步时间和错误码；详情页提供只读关联与重建操作 |
| `EmbeddingModel` | 可空 | 模型升级后旧向量与新查询向量可能不兼容 | 与 `VectorDimension` 组成版本元数据；检索前校验模型与维度 |
| `VectorDimension` | `int?` | 无正数约束，且可能与模型不匹配 | 增加正数检查；由向量服务写入，页面只读 |
| `MetadataLearningRecord.TenantId` | `long?` | 学习记录可能无租户，存在跨租户训练污染风险 | 改为非空；从查询上下文写入，不接受客户端指定 |
| `MetadataLearningRecord.MetadataColumnId` | `long?` | 无显式导航/FK，可能指向不存在或其他租户字段 | 增加 FK和租户一致性验证；无法匹配字段的反馈另建 unresolved 状态 |
| `Correct` | `bool?` | true/false/null 语义不明 | 定义 `Pending/Correct/Incorrect` 枚举；页面用状态控件而非裸文本 |

元数据页面的字段级展示至少应包含：字段名、数据类型、长度/精度、可空、主键、业务含义、置信度、来源、向量状态，以及可点击的“数据源 → 表 → 字段 → 语义 → 向量”关系链。所有 FK 链接必须同时显示对象名称，不能只显示数字 Id。

### 9.7 业务实体与物理绑定字段

| 字段/关系 | 当前约束 | 风险 | 整改要求 |
|---|---|---|---|
| `BusinessEntity(TenantId,BusinessKey)` | 唯一 | 合理，但 BusinessKey 缺少统一规范化 | trim、大小写规则固定，创建后默认不可变 |
| `BusinessDomain` 字符串与 `BusinessDomainId` | 两套域字段并存 | 可能出现名称与 FK 指向不一致 | 以 `BusinessDomainId` 为权威；字符串仅作为迁移兼容字段并逐步移除 |
| Key/Attribute/Metric 的 `Name` | 实体内唯一 | DisplayName、Description 等长度约束不完整 | 补齐最大长度；显示文本走 SemanticLabel 或明确本字段为默认语言基线 |
| `Aggregation` | 最大 50，自由字符串 | 可出现无效聚合函数 | 枚举/白名单 `SUM/COUNT/AVG/MIN/MAX/DISTINCTCOUNT/NONE` |
| `RelationshipType/Cardinality` | 自由字符串 | 关系方向和基数可能出现不可识别值 | 枚举化并校验 source != target 等业务规则 |
| `PhysicalBinding` 四个 Owner FK | 检查约束保证恰好一个非空 | 这是正确约束 | 保留，并增加自动化测试覆盖四类 owner |
| `DataSourceId/MetadataTableId/MetadataColumnId` | 均有 FK | 数据库未保证 Table 属于 DataSource、Column 属于 Table，也未直接携带 TenantId | 保存前逐层验证同一租户和父子关系；可考虑冗余 TenantId 加复合约束以强化隔离 |
| `Priority` | int | 无范围及同 owner 冲突约束 | 限定非负；定义相同优先级的稳定排序或建立唯一规则 |
| `PhysicalRole/BindingType` | 最大 50，自由字符串 | 值域漂移 | 建立枚举目录并在 UI 使用下拉选择 |

### 9.8 行级安全字段

| 字段/组合 | 当前状态 | 风险 | 整改要求 |
|---|---|---|---|
| `TenantId/DataSourceId/MetadataTableId/MetadataColumnId` | 分别存在字段与 FK | 未保证四者属于同一租户和同一元数据链 | 创建/编辑时做完整链路校验；拒绝跨租户、跨源、跨表字段组合 |
| `SubjectType/SubjectId` | 多态主体，SubjectId 可空 | 空值与全体用户之间的含义需固定；无主体 FK | 明确定义 Everyone 主体类型，不以 null 猜测；删除主体时处理策略 |
| `SubjectKey/SubjectValue` | 自由字符串 | 与 SubjectId 两套主体表达可能冲突 | 规定优先级或拆分策略类型；不允许互相矛盾的组合 |
| `Effect` | Allow/Deny 枚举 | 冲突策略需明确 | 明确 Deny 优先、最小权限或其他合并算法并形成测试 |
| `Operator` | 最大 16 | 仅字符串白名单才能防 SQL 注入 | 严格枚举映射为 SQL AST，绝不能直接拼接请求值 |
| `Value` | 最大 2048 | 可能含多值和敏感信息 | 参数化查询；按数据类型解析；审计日志脱敏 |
| `Version/UpdatedTime` | 已有 | 但并发控制未见 ETag 契约 | 更新时带 Version；不匹配返回 409 |

### 9.9 Ask 请求与响应字段

| 字段 | 当前行为 | 风险 | 整改要求 |
|---|---|---|---|
| `AskRequest.Question` | 只校验非空 | 无长度限制，可能导致模型成本、日志和缓存键膨胀 | 建议限制 2–2000 字符；超限返回结构化 `400` |
| `ConversationId` | 可空字符串 | 无格式和长度限制；内存字典键可被滥用 | 服务端生成高熵 ID；限制长度/字符集；校验归属和过期时间 |
| `DataSourceId` | `long`，`0` 表示自动使用全部授权源 | 哨兵值能工作但契约不直观 | 改为 `long?` 或明确 OpenAPI 描述；普通页面不要求用户选择，管理员授权决定范围 |
| `AskRefineRequest.History` | 客户端可提交任意列表 | 无轮数、单轮长度和总长度限制；客户端历史不可作为可信会话 | 限制轮数和总字符；主要历史从服务端会话读取，客户端只提交本轮 instruction |
| `AskRefineTurn.Role` | 自由字符串，代码仅接受 user | 非法值静默忽略 | 使用枚举并返回校验错误，或从公开 DTO 移除客户端 role |
| `ConversationStatus` | 字符串 | 状态值可能漂移 | 定义枚举契约，如 `AwaitingClarification/Completed/Failed/Expired` |
| `RewrittenQuestion` | 仅澄清后返回 | 有助于解释，但可能暴露不应展示的内部扩写 | 保持面向用户的安全文本；与内部 prompt/SQL 分离 |
| 缓存字段 | 租户、问题、数据源权限指纹、RLS 指纹 | 基础隔离已具备 | 增加语言、模型/语义版本、元数据版本；撤权和元数据更新后失效 |

### 9.10 字段级整改优先级

| 优先级 | 编号 | 必须先处理的字段级事项 |
|---|---|---|
| P0 | FIELD-SEC-1 | `ConnectionString` 和配置文件密钥迁出明文存储并轮换 |
| P0 | FIELD-IAM-1 | 所有租户内管理接口不再信任请求体 `TenantId` |
| P0 | FIELD-META-1 | 修复 MetadataTable/Column/Semantic、PhysicalBinding 和 RLS 的租户及父子一致性校验 |
| P0 | FIELD-AUTH-1 | `SecurityStamp` 必填化，停用/改密/敏感授权后可立即吊销令牌 |
| P1 | FIELD-TENANT-1 | TenantCode/TenantName/DataSource 核心字段非空、长度和唯一约束落库 |
| P1 | FIELD-L10N-1 | 租户语言授权关系化，Culture 和默认语言建立完整性约束 |
| P1 | FIELD-IAM-2 | 决定 Username 全局唯一还是租户内唯一，并迁移到规范化索引 |
| P1 | FIELD-ASK-1 | Question、ConversationId、History 增加长度/数量/归属校验 |
| P2 | FIELD-AUDIT-1 | 统一 UTC、更新时间、操作者和并发版本字段 |
| P2 | FIELD-SEM-1 | 关键词、同义词、示例问题结构化，语义与向量版本可追踪 |

每个字段级改动必须同步完成五项内容：实体定义、EF 映射与 Migration、API DTO 校验、页面展示/编辑、自动化测试。只改其中一层不视为完成。

## 10. 更新后的开发计划

### 批次 A：立即安全和契约修复

1. SEC-1：移除并轮换明文凭据。
2. C-1：修复 4 个前后端 404 契约。
3. SEC-2：修复权限 fail-open，区分加载中、失败和空权限。
4. ASK-1：为 DoAsk 增加 try/finally、取消、超时和防重复。
5. 清理现有编译警告。

退出条件：431+ 测试零失败，新增路由契约测试和权限状态测试。

### 批次 B：初始化和平台治理

1. 建立受控 Migration/Seed/Schema Version 流程。
2. 将 Localization 种子从 Controller 懒写入迁移到启动种子。
3. 修正空迁移和真实建表迁移的维护说明。
4. 增加第二个平台管理员创建与管理页面。
5. 增加平台管理员—租户管理范围授权。
6. 补齐初始化管理员资料字段。
7. 决定生产环境 Bootstrap 路由策略。

### 批次 C：多语言产品化

1. 去除 AuthController 中英文硬编码。
2. 建立统一资源键目录。
3. 合并 PlatformStrings、UiTextResources 和前端兜底键空间。
4. 补齐 Nav、Page、Common、Validation、Error 等基础资源。
5. 分批迁移全部 Razor 文件中的用户可见文本。
6. 增加缺失翻译、覆盖率和发布检查页面。
7. 保存文本后即时刷新客户端资源。
8. 将用户语言偏好持久化到服务端，浏览器缓存作为快速恢复层。

### 批次 D：Ask 企业化

1. ASK-2：会话状态迁移到 Redis。
2. 增加对话行为分类器。
3. 增加结构化澄清候选项。
4. 建立 TenantSemanticAlias 审核和学习闭环。
5. 增加会话取消、过期、次数限制和审计。
6. 补充真实 HTTP 端到端测试。

### 批次 E：清除功能占位

1. Agent 创建、工具和运行时闭环。
2. BusinessModel 创建和编辑闭环。
3. ThemeEditor 接入现有主题 API。
4. ModelAccounts 使用正式模型目录和加密绑定。
5. DataSource Test Connection 和多数据库 Connector。
6. Quota 策略管理端点和页面。

### 批次 F：发布验收

1. Playwright 主流程测试。
2. axe 无障碍检查。
3. 桌面、窄屏和移动端截图基线。
4. Web、RCL、MAUI Windows 和 Android 构建。
5. iOS Release 在 macOS 构建环境验证。
6. 数据库从空库升级及旧版本升级演练。
7. 密钥、日志、审计和备份恢复演练。

---

## 11. 统一验收门禁

每一批次至少满足：

- Golden 基线保持 18/18。
- 全量测试零失败，动态下限不得低于当前 431。
- 不删除或弱化现有安全测试。
- 新增 API 同时具备权限、租户隔离和错误契约测试。
- 新增页面具备加载、空、错误和成功状态。
- 新增用户可见文本必须进入统一语言资源。
- 新增关联链接必须存在真实详情接口和详情页面。
- 触及 Migration 时必须验证空库创建、升级和回滚策略。
- 涉及缓存或会话时必须验证撤权、跨租户、过期和多实例行为。

---

## 12. 尚需产品或架构裁决

1. 登录页是否允许公开列举全部启用租户，还是改为输入租户代码精确解析。
2. 生产环境是否保留 Loopback Bootstrap，或只允许部署工具初始化。
3. 多个平台管理员是否管理全部租户，还是建立明确的租户范围。
4. 演示数据采用一键安装还是独立 Demo 环境。
5. Ask 会话使用 Redis，还是数据库加 Redis 的组合。
6. 多语言迁移是否按核心页面优先分批发布。
7. P12 多数据库连接器和 P13 多模型 BYO 是否进入当前发布周期。

---

## 13. 文档维护规则

- 本文以当前代码和最近测试结果为准，不以旧规划文档的完成标记为准。
- 每个问题修复后应更新状态、测试数量和验收证据。
- 工作区变更数量只作为时点信息，不作为质量判断。
- 主观百分比只能用于排期，不能代替功能验收。
- 如果代码和本文冲突，应重新核验源码并更新本文。

---

文档状态：**当前代码核验版，可作为后续整改和验收基线。**
