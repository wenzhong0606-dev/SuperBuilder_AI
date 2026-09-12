> **HISTORICAL SNAPSHOT / 历史快照**：本文是归档资料，只描述记录当时的计划、状态或审计判断。文中的“当前、唯一、已完成、未完成、风险、测试基线、HEAD”等均不得解释为现在的项目状态。当前事实请按 `docs/README.md` 的治理顺序核验。\n\n> **治理声明**：本文档是 `Master_Development_Plan.md` 的**输入 / 审计基线**，**不是**独立执行计划。所有里程碑状态、优先级与验收以 `Master_Development_Plan.md`（唯一事实来源）及其 `milestones/` 拆分文档为准；本文与 MDP 冲突时以 MDP 为准。映射见 MDP §17。
>
> **文档角色**：可执行清单（Stage/P 阶段映射到 M0–M11，见 MDP §17.5）

# SuperBuilder — 至完成开发清单（可执行 CheckList）

> 配套主文档：`docs/DevelopmentPlan.md`（单一事实来源）。本文件为纯执行清单，按 §0 主阶段脊柱编号。  
> 规则：**每个打 ✅ 的阶段都必须通过 `Golden 18/18` 回归闸门**（端点 `GET /evaluation/golden-runtime/run`）。

---

## ✅ Stage 0 — 基础与 AI Native BI 运行时（已完成）

- [x] Phase 0 基础架构（EF/Qdrant/Qwen/多数据库）
- [x] Phase 1 AI BI 查询核心链
- [x] Phase 2 QueryPlan 可靠性（2.1/2.2/2.2.5/2.4/2.5）
- [x] Phase 2.7 D14–D21（D14–D20 FROZEN、D21 Exit PASS、Phase 2.7 READY TO CLOSE）
- [x] Golden 18/18 达标（11+5+1+1，overall/pos/neg 均 100%，failedGates=[]）
- [x] Phase 3.1 Golden Runtime + CSV Fixture + 回归恢复（18/18）

---

## 🟡 Stage 1 — 架构治理（A-track，并行）

- [x] **A1** 目标结构文档/骨架/冗余清理（删 40 文件、归档 13 计划）
- [x] **A2** 单项目内 `src/` 四层物理迁移（234 .cs，命名空间保留，build 0 error，Golden 18/18）
- [x] **A3** 抽取独立项目 Domain/Application/Infrastructure/Api —— **按用户决策跳过（2026-09-11）**；M9-02/03 已用「单项目内 `src/` 四层物理迁移 + 架构不变量测试」等价替代（目录=分层、命名空间=关注点），不再拆 `.csproj`
  - [ ] 新建 4 个 `.csproj` + slnx 引用
  - [ ] 迁移包引用与 `Migrations`（随 Infrastructure.Persistence）
  - [ ] 每拆一个项目：`dotnet build` 绿 + Golden 18/18
- [x] **A4** 上帝类拆分 + 依赖倒置（**P6 前必须完成**）
  - [x] `QueryPlanBuilder`(4308行/8 partial) → `BusinessTermExtractor`+`TableSelector`+`FieldResolver`+`JoinBuilder`+瘦编排器（`QueryPlanBuilder.*.cs` partials）
  - [x] `BIConversationService` → `QueryPlanPipeline` 编排器
  - [x] `QueryPlanValidator`/`SemanticApplicabilityEvaluator` 抽纯领域服务
  - [x] 补 `IQueryPlanBuilder` 等 `I*` 端口 + DI 注册
  - [x] build 0 error；逐 partial 重建与语义对齐
  - [x] **GQ-010 Filter 数量漂移回归修复**：`ApplyFilterResolutions` 由"按索引取 min 条重写、其余忽略"改为"按 SemanticText 语义对齐 + 丢弃无绑定幻影 Filter"，消除 Runtime 多产出 Filter 时残留未重写语义名（入库日期）触发 `FilterFieldNotFound`
  - [x] **GQ-008 幽灵维度加固（与上条对称）**：`ApplyDimensionResolutions` 在 `bindings.Count==0` 时不再直接 return，改为 `RemoveAll(IsUnboundDimension)` 清除 LLM 空维度；数量漂移不再硬抛 `InvalidOperationException`，改为 SemanticText 对齐 + 丢弃幻影 + 用未消费 Resolution 绑定补全
  - [x] **Metric 漂移同构加固（收口第三处）**：`ApplyMetricResolutions` 原 `plan.Metrics.Count != bindings.Count` 硬抛 `InvalidOperationException`（曾可触发 GQ-002/GQ-005 的 500 回归）改为与 Filter/Dimension 一致——SemanticText 对齐 + 丢弃无绑定幻影指标 + 用未消费 Resolution 绑定补全（保留 GQ-002 EntityCount→COUNT 强制覆盖）；1:1 时等价于原索引重写，行为不变。`ApplyOrderResolutions` 因 `BuildResolvedPlanSkeleton` 的 orders 直接由 `resolution.Orders` 生成（结构恒等）无需改
  - [x] **Golden 闸门 LLM 超时加固**：`QwenService` 注入的 `HttpClient` 从未设 `Timeout`（落默认 100s），高延迟时段 `qwen3.7-plus` 单次推理超时被取消 → GQ-005 判 ERROR（属基础设施抖动而非逻辑失败，却污染 18/18 结论）。新增可配置 `Qwen:TimeoutSeconds`（默认 180s，与 Embedding 侧 120s 对齐）覆盖默认超时
- [~] **A5** 限界上下文解耦（**A3 前置**）—— **部分完成（M9-11，2026-09-09）**：循环依赖已消除；SharedKernel 抽取与重复 DTO/枚举合并未做
  - [x] 消除 `Metadata↔Organization` 循环依赖 —— **M9-11 已交付**：移除 `Tenant.DataSources` 集合导航（环的唯一边），应用层零 `tenant.DataSources` 读取，改由 Fluent `HasOne(...).WithMany(x => x.DataSources)` 保持关系语义；build 0 error + Golden 18/18
  - [ ] 抽 `SharedKernel`
  - [ ] 合并 `GoldenBaseline`↔`GoldenBaselinePersistenceRecord`
  - [ ] 合并 `QueryPlanSemanticResolution`↔`SemanticApplicabilityResult/*Resolution`
  - [ ] 统一三套搜索结果 DTO、统一 `DimensionResolutionType` 枚举

---

## ⬜ Stage 2 — 产品演进（P-track，至完成）

### P3 — Business Semantic Model（✅ 已完成：批次 1-2 模型落地 + 管线接线 + Golden 18/18）

- [x] `src/Domain/BusinessEntity/BusinessDomain.cs`（新增 AR：业务域）
- [x] `BusinessEntityDimension.cs`（子实体）+ `BusinessSemanticResolutionResult.cs`（VO）
- [x] `src/Application/Ports/BI/Entity/IBusinessEntityRepository.cs`（端口）+ `Infrastructure/Persistence/BusinessEntityRepository.cs`（实现）
- [x] `BusinessEntityRegistryService.cs`（注册/发现/校验）
- [x] `BusinessSemanticMappingService.cs`（Metadata→BusinessEntity，确定性 token 重叠打分，不调 LLM）
- [x] `Infrastructure/Persistence/Configurations/Phase31EntityModelConfiguration.cs`（含 `BusinessDomainConfiguration` + `BusinessEntityDimensionConfiguration`）
- [x] Migration `20260828144504_P3BusinessEntityModelWithDomains`（8 张表，已 apply；消除 `TenantId1` 影子 FK）
- [x] `Api/Controllers/BusinessModelController.cs` + DI 注册（`Program.cs:45-46`）
- [x] **管线接线（P3 收口）**：`QueryIntentNormalizer.NormalizeWithBusinessEntitiesAsync` 在同步归一化后异步调用 `IBusinessSemanticMappingService` 识别候选业务实体并写入 `QueryIntent.BusinessEntityHints`（软信号，异常/无匹配静默跳过，绝不阻断主链路）；`IQueryUnderstandingService` 新增 `UnderstandAsync(question, tenantId)` 重载，`BIConversationService` 实时路径接入；Golden 沿用无 tenant 的 `UnderstandAsync(question)` 重载 → 走新代码零行为变化
- [x] **编排层透传**：`QueryPlan.BusinessEntityContext` 承载命中业务实体；`QueryPlanBuilder` 两路径（1 参/2 参 `BuildAsync`）将 `intent.BusinessEntityHints` 透传到计划（不覆盖 Metadata 解析结果，零膨胀）
- [x] `BusinessEntityMetric.cs`（即 Metric VO；`BusinessEntityMetricDefinition.cs` 命名项由该文件承担，无需新增）
- [x] 诊断控制器 `Api/Diagnostics/*BusinessEntity*` 路由收敛（已统一于 `evaluation/business-entity*` 前缀，内部链接自洽）
- [x] **验收**：`dotnet build` 0 error 0 warning；**Golden 18/18 PASS**（`expectedOutcomePassed 18/18`、`failedGates:[]`、`decision:PASS`）；无新增 Migration（未新增 EF 实体）

### P4 — Multi-Tenant Platform Core

**P4.1 平台上下文抽象（✅ 完成 · 零 schema 变更 · 未触及 gated 查询链路）**

- [x] `src/Domain/Organization/TenantContext.cs`（租户运行时上下文 record，含 `System` 全局态）
- [x] `src/Domain/Organization/PlatformContext.cs`（聚合 Tenant/User/Workspace/Locale/Theme，先落地 Tenant）
- [x] `src/Application/Ports/Platform/IPlatformContextAccessor.cs`（scoped 访问器接口）
- [x] `src/Application/Platform/PlatformContextAccessor.cs`（默认实现）
- [x] `BIConversationService` 注入可选 `IPlatformContextAccessor`；`ExecuteAsync` 内由 `tenantId` 建立 `PlatformContext` 写入访问器（为 P4.3 全局过滤奠基；下游 `tenantId` 透传不变）
- [x] `TenantManagementController`（`api/tenant-management`：GET 列表/按Id、POST 创建、PATCH 启用/停用；注入 `SuperBIContext`）— 已验证返回 Tenant1(WMS)/Tenant3(CSV_FIXTURE) → 200
- [x] `Program.cs` 注册 `IPlatformContextAccessor`（scoped）
- [x] **验收**：`dotnet build` 0 error 0 warning；服务启动监听 5032 无 DI 错误；未触及 gated 路径 → 不强制重跑 Golden  
  **P4.2 Tenant 实体扩展 + Migration（✅ 完成 · 零行为变更 · build 绿 + Golden 18/18）**
- [x] `src/Domain/Organization/TenantSetting.cs`（通用租户 KV 配置，统一承载 Setting/Locale/Theme/Workspace，Key 命名约定按域前缀）
- [x] `SuperBIContext` 新增 `DbSet<TenantSetting>` + 配置（级联删除、`(TenantId,Key)` 唯一索引、列注释）；Migration `20260829090341_P4_2_TenantSettings` 已生成并应用
- [x] `TenantManagementController` 扩展 `GET/POST /api/tenant-management/{id}/settings`（运行时验证：写读往返 200）
- [x] **验收**：`dotnet build` 0 error（10 个预存 nullable 警告均不在本阶段文件）；**Golden 18/18 PASS（decision=PASS, 18/18, failedGates=[]）**  
  **P4.3 SuperBIContext 全局租户过滤 + 跨租户单测（✅ 完成 · 零回归 · build 绿 + Golden 18/18）**
- [x] `SuperBIContext` 新增 `_tenantFilterEnabled`/`_scopedTenantId` 私有字段 + 显式 `ApplyTenantScope(long tenantId)` 方法（默认关闭=no-op）；`OnModelCreating` 为 4 个直接持有 TenantId 的根实体加 `HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId)`
- [x] **关键设计**：不读取 `IPlatformContextAccessor`（避免 DbContext 构造期/请求期取值错位）；由 `BIConversationService.ExecuteAsync` 在已知 tenantId 后显式调用 `ApplyTenantScope` 开启。**Golden 无租户路径从不调用 → 过滤恒为 no-op**（实测 18/18 未破）
- [x] `DataSource.TenantId` 为 `long?`：过滤器用 `e.TenantId.HasValue && e.TenantId.Value == _scopedTenantId` 守卫，避免 `long? == long` 产生被提升的 `bool?` 与 `||` 组合触发 EF "Nullable object must have a value" 回归（此前两次失败根因）
- [x] 新增测试项目 `tests/SuperBuilder_AI.Tests/`（xunit + EF Core SQLite 内存库，离线可还原）`SuperBIContextTenantFilterTests`：覆盖 4 个根实体，验证 (1) 无作用域=全可见 no-op (2) 开启租户=仅本租户可见 (3) 跨租户不可见 (4) System/tenantId<=0=no-op；**4/4 通过**
- [x] **验收**：`dotnet build` 0 error 0 warning；**Golden 18/18 PASS（decision=PASS, passedCases=18/18, failedGates:None, overallPassRate=1.0, positivePassRate=1.0；3 个 ERROR 为预期负例 Qwen 403 抖动，expectedOutcome 满足）**  
  **P4.4 核心 Runtime 入口 PlatformContext 化（✅ 完成 · 零回归 · build 绿 + Golden 18/18 PASS）**
- [x] `IQueryUnderstandingService.UnderstandAsync` 带租户重载签名由 `(question, long tenantId)` 迁移为 `(question, PlatformContext platformContext)`
- [x] `QueryUnderstandingService` 实现同步迁移；`BIConversationService.ExecuteAsync` 在已知 `platformContext`（由 tenantId 收敛）后显式传入 `UnderstandAsync(question, platformContext)`，内部不再透传裸 `tenantId`
- [x] `QueryIntentNormalizer.NormalizeWithBusinessEntitiesAsync` / `EnrichBusinessEntityHintsAsync` 由 `long tenantId` 迁移为 `PlatformContext`；内部以 `platformContext?.Tenant?.TenantId ?? 0` 提取租户并调用 `IBusinessSemanticMappingService.ResolveAsync`（行为等价：System/TenantId=0 时解析为空、hint 静默跳过）
- [x] **范围澄清**：`QueryPlanPipeline.RunAsync(question, intent)` 当前签名本就不携带 `tenantId`（其对租户隔离的依赖已通过 P4.3 的 `SuperBIContext` 全局过滤在查询层强制执行），故 P4.4 无需改动该入口；"核心 Runtime 入口 PlatformContext 化"实际落地于意图理解入口链路
- [x] Golden 无租户重载 `UnderstandAsync(question)` 保持不变 → 历史行为零变化
- [x] **P4 总验收（✅ 完成 · Golden 18/18 PASS）**：build 绿（0 error，10 个预存 nullable 警告均不在本阶段文件）+ 跨租户单测 4/4 通过；Golden 18/18 在切换可用模型 `qwen-plus` 后复跑通过（decision=PASS, passedCases=18/18, failedGates=None, overallPassRate=1.0, positivePassRate=1.0, 0 ERROR）。此前两次失败（qwen3.7-plus 配额耗尽 403 / qwen3.5-ocr 模型不适用）均属外部模型问题、非本阶段代码回归。

### P5 — Multi-Language Runtime（✅ 已完成 · 全绿 · Golden 18/18 ×4 轮）

**P5.1 LocaleContext 领域模型 + 接入 PlatformContext（✅ 完成 · 零 schema 变更 · Golden 18/18 PASS）**

- [x] `src/Domain/Organization/LocaleContext.cs`（语言区域运行时上下文 record：Culture/Language/Region/DisplayName/TimeZoneId/TextDirection/IsDefault；IETF BCP 47 归一化 `zh_CN`→`zh-CN`；无效输入回退 `Default`）
- [x] **零回归硬约束**：平台默认语言恒为 `zh-CN`（与既有中文业务语义、Golden 基线一致）；未显式指定语言的路径（含 Golden 运行时）恒取 `Default` → 行为与 P5 之前完全一致
- [x] `PlatformContext.Locale` 由占位 `string? Locale` 升级为 `LocaleContext Locale`；新增 `FromTenant(tenantId, tenantCode, culture)` 工厂
- [x] `ILocalizationService` / `LocalizationService`（纯确定性：无 DB、无 LLM）：语言区域解析 + 回退链（`zh-TW`→`["zh-TW","zh","zh-CN"]`，终点恒为默认语言）+ 支持语言清单
- [x] `LocalizationController`（`api/localization`：GET `locales` / `resolve?culture=` / `fallback-chain?culture=`）；`Program.cs` 注册 singleton
- [x] 单测 `LocalizationServiceTests`：**22 项全通过**（归一化、无效输入回退、回退链不变量、默认语言零回归）
- [x] **验收**：`dotnet build` 0 error（10 个预存 nullable 警告均不在本阶段文件）；单测 **26/26**（P4.3 4 + P5.1 22）；3 个端点运行时 200；**Golden 18/18 PASS（decision=PASS, passedCases=18/18, failedGates=None, overallPassRate=1.0, positivePassRate=1.0, 0 ERROR）**  
  **P5.2 业务语义多语言标签持久化 + Migration（✅ 完成 · Golden 18/18 PASS）**
- [x] `src/Domain/Localization/SemanticLabel.cs`（同一语义概念的多语言表述：`销售额`/`Sales Amount`/`売上高` → 同一 Concept 的多条标签）+ `SemanticConceptTypes` / `SemanticLabelKinds` 常量
- [x] **弱多态关联**（ConceptType + ConceptId）而非为每个宿主建表：一套机制同时服务字段级语义与 P3 业务实体。`TenantId` 用非可空 `long`（0=全局共享），规避 P4.3 的 `long?` 提升布尔陷阱
- [x] 唯一索引含 **`SortOrder`**：同义词/示例问句天然多值，仅按 LabelKind 唯一会使第二条同义词撞键被覆盖（单测暴露后修正）
- [x] `ISemanticLabelService` / `SemanticLabelService`：回退链解析（单次 SQL 取链上全部候选 → 内存按链序定序）、同义词去重排序、upsert 文化归一化；解析失败软降级返回 null，绝不阻断主链路
- [x] `SemanticLabelController`（`api/semantic-labels`：GET 列表 / `resolve` / `synonyms`；POST upsert）
- [x] Migration `20260829154339_P5_2_SemanticLabels` 已生成并应用（仅新增 `SemanticLabels` 表，未触碰任何既有表）
- [x] 全局租户过滤放行 `TenantId == 0`：否则共享译文在租户作用域内会集体消失
- [x] 单测 `SemanticLabelServiceTests` **12 项全通过**（回退到语言段/默认语言、无标签返回 null、同义词去重、upsert 归一化与幂等、跨租户不可见、全局标签可见）
- [x] **验收**：build 0 error（10 个预存 nullable 警告均不在本阶段文件）；单测 **36/36**；端点读写往返 200（`zh_TW`→`zh-TW` 归一化，`ja-JP`/`ko-KR` 正确回退到默认语言）；**Golden 18/18 PASS（decision=PASS, 18/18, failedGates=None, overallPassRate=1.0, positivePassRate=1.0, 0 ERROR）**  
  **P5.3 本地化资源 + 语义标签接入检索（✅ 完成 · 门控隔离 · Golden 18/18 PASS）**
- [x] `PlatformStrings`（zh-CN/zh-TW/en-US/ja-JP/ko-KR 五语言文案资源）；`LocalizationService.GetString` 由"返回键名占位"升级为按回退链真实解析，未登记键仍返回键名本身（宁可暴露原始键，不展示空文案）
- [x] `ISemanticLabelRecallService` / `SemanticLabelRecallService`：按已登记标签做**确定性**文本匹配（无 LLM、无向量库），解决跨语言 Embedding"能召回但排序偏后"的偏序问题
- [x] **接入检索层（触及 Golden 路径文件）**：`IMetadataSemanticSearchService.SearchAsync` 新增**可选** `locale` 参数（既有调用方签名不变）；`MetadataSemanticSearchService` 仅在非默认语言时对命中标签的候选做排序提升
- [x] **三重门控保证零回归**：`locale == null` / `IsDefault` / `Culture` 为空 → 标签匹配整体短路，行为与 P5 之前逐字节一致；Golden 与既有中文链路从不进入该分支
- [x] **只提升已召回候选，绝不注入合成候选**（提升量 `0.15 × 匹配强度`，封顶 1.0），避免凭空产生下游无法解释的结果；标签召回异常一律静默降级、不阻断主检索
- [x] `GET /api/semantic-labels/recall`（含 `gated` 字段便于验证门控是否生效）
- [x] 单测 **19 项全通过**：门控不变量（默认/Invariant/null 均空）、大小写不敏感、长标签优先且强度归一化、同概念取最强、短标签过滤、回退到默认语言标签、仅作用于 MetadataSemantic
- [x] **验收**：build 0 error（10 个预存 nullable 警告均不在本阶段文件）；单测 **55/55**；端点验证（默认语言 `gated:true` 零命中；`ja-JP` 经回退链命中 `入库日期`；`en-US` 命中 `Inbound Date`）；**Golden 18/18 PASS（decision=PASS, 18/18, failedGates=None, overallPassRate=1.0, positivePassRate=1.0, 0 ERROR）**  
  **P5.4 AI 意图语言无关化 + 多语言验收（✅ 完成 · 门控隔离 · Golden 18/18 PASS）**
- [x] `QueryIntentLocaleDirective`（纯函数、可离线断言）：非默认语言时在 QueryIntent 提示词中追加"一律以**简体中文**输出语义名称"的指令，使 `Intent → Semantic Concept` 与提问语言无关
- [x] **门控（零回归核心）**：默认语言 / Invariant / null → 返回空字符串 → **提示词与 P5 之前逐字节一致**；Golden 走无 locale 的 `UnderstandAsync(question)` 重载，恒不进入该分支
- [x] `QueryUnderstandingService.BuildRawIntentAsync` 新增可选 `locale` 参数；`UnderstandAsync(question, PlatformContext)` 透传 `platformContext.Locale`
- [x] **关于"多语言问句 Golden 复跑"的落地方式（重要约束）**：契约文件 `Evaluation/Golden/query-plan-golden-v1.json` 为**不可删改**的护栏，故多语言验证未改写该契约，而是新增**确定性离线一致性测试**——同一语义概念登记 zh-CN/en-US/ja-JP/ko-KR 四种标签后，四种语言的问句均解析到同一 `semanticId`，证明语义层语言无关（无需 LLM，可离线复现）
- [x] 单测 **10 项全通过**（门控不变量、四语言指令内容、归一化目标为简体中文、禁止保留源语言词汇、未登记语言回退、跨语言语义一致性、默认语言走门控路径不受标签影响）
- [x] **P5 总验收 ✅**：P5.1~P5.4 全绿；build 0 error（10 个预存 nullable 警告均不在本阶段文件）；单测 **65/65**；**Golden 18/18 PASS ×4 轮（P5.1/P5.2/P5.3/P5.4）**

### P6 — Low-code BI Engine（前置：A4 完成）

**P6.1 Dashboard DSL 领域模型 + 持久化（✅ 完成 · 零 schema 破坏 · Golden 18/18 PASS）**

- [x] `src/Domain/Dashboard/DashboardDsl.cs`（`DashboardDsl` 根聚合 + `DashboardWidget`/`DashboardPage` 子实体：纯 POCO、无 EF 依赖；`TenantId` 仅作用域列）
- [x] `src/Domain/Dashboard/WidgetDsl.cs` / `WidgetQueryDsl.cs`（`WidgetType` 枚举 + 查询/过滤/排序 VO；覆盖 Chart/Table/KPI/Filter/Text/AIInsight/Query/Page 等类型占位）
- [x] `src/Domain/Dashboard/Dashboard.cs`（`Dashboard` 持久化实体：Id/TenantId/DslJson/Name/Version/IsGlobal；**无外键**——全局共享模板 `TenantId=0` 在 `Tenant` 表无行会触发 FK 约束失败，故仅保留 TenantId 作用域列）
- [x] `SuperBIContext` 注册 `DbSet<Dashboard>` + `HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId==_scopedTenantId || e.TenantId==0)`（全局共享模板放行）
- [x] Migration `20260830012408_P6_1_Dashboard` 已生成并应用（仅新增 `Dashboards` 表；首次因 FK 约束失败回滚后去 FK 重做）
- [x] 单测 `DashboardTenantIsolationTests` **5/5 通过**（无作用域全可见、本租户隔离、全局模板可见、跨租户不可见、System no-op）
- [x] **验收**：build 0 error；单测 **98/98**（P5 65 + P6.1 5 + P6.2 28）；**Golden 18/18 PASS（decision=PASS, 18/18, failedGates:None, overallPassRate=1.0, positivePassRate=1.0, 0 ERROR）**  
  **P6.2 DashboardDSL 序列化与校验（✅ 完成 · 不存裸 HTML 红线 · Golden 18/18 PASS）**
- [x] `src/Application/Ports/BI/IDashboardDslSerializer.cs`（端口：`SerializeAsync`/`DeserializeAsync`/`Validate`）
- [x] `src/Application/BiQuery/Dashboard/DashboardDslSerializer.cs`：JSON 序列化 + 结构校验 + **「不存裸 HTML」红线拦截**（检测到 `<script>`/内联事件/on* 属性/iframe 等危险片段即抛 `DashboardDslValidationException`，绝不写入 DB）
- [x] 单测 `DashboardDslSerializerTests` **28 项全通过**（序列化往返、各 Widget 类型、租户作用域列保留、HTML 红线多情形、空/缺字段降级）
- [x] **验收**：build 0 error；单测 98/98；**Golden 18/18 PASS（decision=PASS, 18/18, failedGates:None）**  
  **P6.3 LowcodeRenderer 渲染引擎（✅ 完成 · 对照 QueryPlanPipeline 接入 · Golden 18/18 PASS）**
- [x] `src/Domain/Dashboard/Rendering/DashboardRenderModels.cs`（纯结构化渲染模型 `DashboardRenderModel`/`PageRenderModel`/`WidgetRenderModel`/`WidgetDataRenderModel`/`FilterRenderModel`/`AiInsightRenderSpec`；**绝不承载 HTML**，与 P6.2 红线一致）
- [x] `src/Application/Ports/BI/IDashboardRenderer.cs` + `IWidgetDataResolver.cs`/`WidgetDataResult`（端口 + 取数结果）
- [x] `src/Application/BiQuery/Dashboard/DashboardLowcodeRenderer.cs`：DSL→渲染模型；全局筛选器下推（合并进 `EffectiveFilters`）；数据组件委托 `IWidgetDataResolver`；文本组件二次净化（去 HTML 防 XSS）；AI 洞察仅占位
- [x] `src/Application/BiQuery/Dashboard/QueryPlanWidgetDataResolver.cs`：**对照 QueryPlanPipeline 接入**——`WidgetQueryDsl`→`QueryIntent`（自然语言问句或显式指标合成）→`IQueryPlanPipeline`（Decision Gate）→`ISqlQueryBuilder`→`IQueryExecutionService`，与 `BIConversationService` 的 Step 2~7 完全一致，不引入新语义漂移
- [x] `Program.cs` 注册 `IDashboardRenderer`/`IWidgetDataResolver`
- [x] 单测 **9 项全通过**（渲染器 6：全类型/数据行/筛选下推/文本净化/无取数组件/AI 占位；解析器 3：NoQuery/Blocked/Proceed）；合计 **单测 107/107**
- [x] **验收**：build 0 error；**Golden 18/18 PASS（C:/tmp/golden_p63.json，decision=PASS, 18/18, failedGates:None）**
- [x] **P6.4 DashboardController（生产）+ 编辑器前端占位（✅ 完成 · Golden 18/18 PASS）**
  - [x] `src/Api/Controllers/DashboardController.cs`：CRUD（`POST /api/dashboards` 创建·`GET` 列表/单资源·`PUT`·`DELETE`，租户作用域隔离）+ `GET /api/dashboards/{id}/render`（加载 DSL → P6.3 `IDashboardRenderer` → 纯结构化 `DashboardRenderModel`，取数按仪表盘所属租户作用域隔离）+ `GET /api/dashboards/editor/blueprint`（编辑器占位：DSL 骨架 + 全部可用枚举清单，仅结构化 JSON）
  - [x] `Program.cs` 补注册 `IDashboardDslSerializer`（P6.1/P6.2 序列化器此前未注入 DI）；`IDashboardRenderer`/`IWidgetDataResolver` 已注册（P6.3）
  - [x] 测试项目 `SuperBuilder_AI.Tests.csproj` 增加 `FrameworkReference Microsoft.AspNetCore.App` 以支持控制器单测；`DashboardControllerTests` **10 项全通过**（创建/校验失败/租户作用域/获取/更新/删除/渲染编排/编辑器蓝图）
  - [x] **验收**：build 0 error；**单测 117/117**（107 + P6.4 10）；**Golden 18/18 PASS（C:/tmp/golden_p64.json，首跑 GQ-011 因 Qwen LLM 非确定性抖动 ERROR → 复跑 PASS，decision=PASS, 18/18, failedGates:None, overallPassRate=1.0, 0 ERROR）。已确认 `IDashboardDslSerializer` 仅被 P6.4 控制器与注册使用，绝不进入 BI 查询链路，故该抖动非本阶段代码回归**
- [x] **P6 总验收 ✅**：build 0 error；单测 **117/117**；**Golden 18/18 PASS（×2 轮：首跑 GQ-011 抖动 BLOCK，复跑 18/18 PASS）**；DSL 渲染冒烟（`/api/dashboards` 返回 200 `[]`、`/editor/blueprint` 返回完整结构化蓝图）通过

### P7 — Multi-Theme / Style Engine

- [x] **P7.1 Theme 领域聚合 + 持久化 + 内置默认主题 ✅**：`ThemeDsl`（结构化设计令牌 Brand/Color/Typography/Layout/Border/Radius/Shadow/ChartPalette/Component/DashboardTemplate，绝不承载 CSS/HTML）+ `Theme` 实体（TenantId/Key/Name/IsBuiltIn/DslVersion/DslJson，租户查询过滤 TenantId=0 放行）+ 内置默认浅色主题 `BuiltInThemes.DefaultDsl()`；`SuperBIContext` 增加 `DbSet<Theme>` 与配置；迁移 `20260830034528_P7_1_Theme` 已生成并应用到 `SuperBuilder_Platform`；单测 `ThemeDslTests` 3 项通过。**build 0 error；单测 120/120；Golden 18/18 PASS**。
- [x] **P7.2 ThemeContext 接入 PlatformContext + 级联解析服务 ✅**：`ThemeContext` 值对象（Key/Source/Dsl，绝不承载 CSS/HTML）+ `ThemeSource` 枚举；`PlatformContext.Theme` 由 `string?` 升级为 `ThemeContext`（默认 `ThemeContext.Default` 内置浅色）；`IThemeResolver`/`ThemeResolver` 实现级联 仪表盘显式键 → 租户默认(TenantSetting `theme:defaultKey`) → 内置默认，跨租户不泄漏（仅允许当前租户 ∪ 内置 TenantId=0）；`Program.cs` 注册 `IThemeResolver`。单测 `ThemeResolverTests` 6 项（级联优先级 + 跨租户隔离）通过。**build 0 error；单测 126/126；Golden 18/18 PASS（C:/tmp/golden_p72.json）**。
- [x] **P7.3 渲染引擎接入主题 + 主题切换渲染测试 ✅**：`DashboardLowcodeRenderer` 消费 `context.Theme`（P7.2 级联结果）在 `DashboardRenderModel.Theme` 输出结构化 `ThemeRenderModel`（语义键→hex 的 `ColorMap`）；新增 `ThemeRenderMapper` 纯函数把组件 `StyleDsl`（Palette/Background/ShowBorder/Padding）语义键映射到具体色值/档位，合并主题 `Component` 默认值；`WidgetRenderModel.StyleSpec` 承载逐组件可落地风格。`DashboardController.Render` 按仪表盘所属租户 + `ThemeKey` 经 `IThemeResolver` 级联解析并注入 `PlatformContext.Theme`（解析兜底内置默认，渲染永不失败）。单测 `ThemeRenderTests` 6 项（ColorMap 解析 / 组件语义键映射 / 无 Style 回退主题默认 / 同 DSL 两主题 StyleSpec 不同）通过。**build 0 error；单测 131/131；Golden 18/18 PASS（C:/tmp/golden_p73.json，decision=PASS, 18/18, failedGates:None, 0 ERROR）**。
- [x] **P7.4 Theme 管理端点（CRUD + 指派）+ P7 总验收 ✅**：`ThemeController`（租户作用域 CRUD + 内置主题不可改/删守卫 + 重复键 409 + 指派租户默认 `TenantSetting["theme:defaultKey"]` + 从内置/已有主题复制 + 编辑器蓝图）+ `ThemeDslSerializer`（序列化/反序列化/版本校验）。单测 `ThemeControllerTests` 12 项 + `ThemeDslSerializerTests` 4 项（含指派后级联解析命中、从内置复制继承默认主色、内置守卫、蓝图）。**build 0 error；单测 147/147；Golden 18/18 PASS（C:/tmp/golden_p74.json，限流窗口恢复后复跑 decision=PASS, 18/18, failedGates:None, 0 ERROR）**。
- [x] **P7 总验收 ✅**：build 绿 + **Golden 18/18 PASS** + 主题切换渲染测试（147/147 单测 + Golden 18/18 双绿）

### P8 — AI App Builder

- [x] **P8.1 App 领域模型（AppPlan/PagePlan/ComponentPlan）✅**：`AppPlan` 实体（TenantId/Code/Name/Description/Status/DslVersion/DslJson/ThemeKey，租户查询过滤 TenantId=0 放行）+ `AppDsl`（根 DSL：Version/Code/Name/Description/ThemeKey/Pages）+ `PagePlan`（Id/Name/Order/Layout/Components）+ `ComponentPlan`（Type/Id/Title/Order/Position/Binding 强类型取数 + Properties 类型专属参数 + Style 语义键）+ 常量类（AppComponentTypes/AppAggregateTypes/AppFilterOperators/AppLayoutKinds/AppStatuses/AppDslVersions）。`AppDslSerializer`（IAppDslSerializer）做序列化/反序列化/校验（HTML 红线 + 版本/唯一性/枚举/绑定校验）。`SuperBIContext` 加 `DbSet<AppPlan>` + 配置 + 迁移 `20260830053330_P8_1_AppPlan`（已应用到 SuperBuilder_Platform）。单测 `AppDslSerializerTests` 10 项（往返/空JSON/畸形/版本/重复页/不支持组件/HTML/空页/不支持聚合/默认实体）。**build 0 error；单测 157/157；Golden 18/18 PASS（C:/tmp/golden_p81.json）**。
- [x] **P8.2 AppBuilderAgent 编排 ✅**：`IAppBuilderAgent` 端口（src/Application/Ports/AppBuilder）+ `AppBuilderAgent` 实现（src/Application/AppBuilder）。两条路径：**默认路径** `BuildFromDslAsync`（结构化 AppDsl → AppPlan，纯确定性、不调用 LLM、零回归）+ **非默认路径** `GenerateFromDescriptionAsync`（自然语言描述 → 调 `IQwenService` 生成 DSL JSON → `IAppDslSerializer` 先校验后信任 → AppPlan，仅显式传入描述时启用 LLM）。`AppBuildResult` 统一承载（Success/Plan/DslJson/Errors/UsedAi）；Code 解析优先级 explicit → dsl.Code → 名称 slug 兜底。Program.cs 注册 `IAppBuilderAgent → AppBuilderAgent`。单测 `AppBuilderAgentTests` 9 项（默认成功/空页失败/HTML红线/Code优先级/slug兜底/LLM有效/空描述/畸形JSON/不支持组件）。**build 0 error；单测 166/166（含 P8.2 新增 9）；Golden 18/18 PASS（expectedOutcomePassed 18/18，failedGates=None）**。
- [x] **P8.3 AppBuilderController 端点 ✅**：`AppBuilderController`（src/Api/Controllers）租户作用域 CRUD（`POST/GET/PUT/DELETE /api/apps`）+ 生成端点（`POST /api/apps/generate`，非默认路径调 `GenerateFromDescriptionAsync` 启用 LLM）+ 编辑器蓝图（`GET /api/apps/editor/blueprint`，DSL 骨架+枚举清单）。编排委托 `IAppBuilderAgent`（默认路径 `BuildFromDslAsync` 确定性、非默认 `GenerateFromDescriptionAsync` 先校验后信任）。全局模板（TenantId=0）可见不可改/删；Code 同租户+全局唯一；跨租户隔离。单测 `AppBuilderControllerTests` 13 项（含 FakeQwen 覆盖生成路径、跨租户隔离、全局守卫、畸形 LLM 返回 502）。**build 0 error；单测 179/179（含 P8.3 新增 13）；Golden 18/18 PASS（expectedOutcomePassed 18/18，failedGates=None）**。
- [x] **P8.4 P8 总验收 + 端到端应用生成冒烟 ✅**：真实运行 5032 服务端到端验证整链——`GET /api/apps/editor/blueprint`(200) → `POST /api/apps` 结构化 DSL 默认路径(201) → `GET /api/apps/{code}` 读取回填一致 → 列表包含 → 跨租户(tenant=2)隔离返回 404 → `DELETE` 清理(204)；`POST /api/apps/generate` 自然语言非默认路径因 Qwen 限流返 502（best-effort 非阻断，LLM 路径已由 P8.2/P8.3 FakeQwen 单测覆盖）。**build 0 error；单测 179/179；Golden 18/18 PASS（expectedOutcomePassed 18/18, failedGates=None）**。P8 AI App Builder 全绿闭合。

### P9 — AI Agent / Copilot

- [x] **P9.1 ToolRegistry + Agent 领域模型 + 序列化器 + 持久化 ✅**：`AgentTools`/`AnalysisDimensions`/`AnalysisDirections` 常量 + `AgentDsl`/`AgentToolSelection`/`AnalysisStep`/`AgentPlan`/`AgentResult` 模型；`ToolRegistry`（工具目录 + 确定性意图解析 + 异常分析链路构建）；`AgentDslSerializer`（序列化/校验/HTML 红线）；`SuperBIContext` 加 `DbSet<AgentPlan>` + 配置 + 查询过滤（TenantId=0 放行）；迁移 `20260830073012_P9_1_AgentPlan`；`IAgentDslSerializer` 注册。单测 `ToolRegistryTests` 11 项 + `AgentDslSerializerTests` 13 项。**build 0 error；单测 203/203；Golden 18/18 PASS**。
- [x] **P9.2 AgentPlanner 编排 ✅**：`IAgentPlanner` 端口（src/Application/Ports/Agent）+ `AgentPlanner` 实现（src/Application/Agent）。**默认路径** `PlanFromIntentAsync`（意图 → `ToolRegistry.ResolveFromIntent` 确定性工具选择 + 据异常信号附加 `BuildAnomalyChain` 异常分析链路，纯确定性、不调 LLM、零回归）+ **非默认路径** `GenerateFromDescriptionAsync`（自然语言描述 → `IQwenService` 生成 DSL JSON → `IAgentDslSerializer` 先校验后信任，仅显式描述启用 LLM）。`AgentResult` 统一承载（Success/Plan/DslJson/Errors/UsedAi）；Code 解析优先级 explicit → dsl.Code → 意图 slug 兜底。Program.cs 注册 `IAgentPlanner → AgentPlanner`。单测 `AgentPlannerTests` 11 项（含 FakeQwen 覆盖 LLM 路径、确定性同意图同工具）。**build 0 error；单测 214/214（含 P9.2 新增 11）；Golden 18/18 PASS（expectedOutcomePassed 18/18，failedGates:None）**。
- [x] **P9.3 AgentController 端点 + 异常检测/原因分析链路 ✅**：`AgentController`（`src/Api/Controllers`，`SuperBuilder_AI.Controllers`）。端点：租户作用域 CRUD（`POST /api/agent/plan` 默认确定性路径 / `GET /api/agent/plans` / `GET /api/agent/plans/{code}` / `PUT /api/agent/plans/{code}` / `DELETE`）+ 生成（`POST /api/agent/plan/generate` 非默认 LLM 路径，失败 502）+ 工具目录（`GET /api/agent/tools`）+ 异常原因分析链路（`GET /api/agent/anomaly-chain` 确定性 6 步链，销售额→同比→环比→区域→客户→产品→渠道）+ 编辑器蓝图（`GET /api/agent/plans/blueprint`）。零回归门控：默认路径仅调 `IAgentPlanner.PlanFromIntentAsync`，不碰 LLM；全局模板（TenantId=0）可见不可改/删；Code 同租户+全局唯一。单测 `AgentControllerTests` 17 项（含 FakeQwen 覆盖 LLM 路径/畸形 502/跨租户隔离/工具目录/异常链/蓝图）。**build 0 error；单测 231/231（含 P9.3 新增 17）；Golden 18/18 PASS（expectedOutcomePassed 18/18，failedGates:None）**。
- [x] **P9.4 P9 总验收 ✅**：build 0 error + **Golden 18/18 PASS** + 工具选择测试（`ToolRegistryTests` 11 + `AgentPlannerTests` 11 + `AgentControllerTests` 17 共 39 项）+ 端到端 Agent 计划生成冒烟（`p9_e2e_smoke` 23/23：租户 CRUD/跨租户隔离 404/异常分析链路 6 步/蓝图/LLM 生成路径 201/清理）。**build 0 error；单测 231/231；Golden 18/18 PASS（expectedOutcomePassed 18/18，failedGates:None）**。P9 全阶段闭合。
- [x] **验收 ✅**：build 0 error + **Golden 18/18 PASS** + 工具选择测试（P9 全部子阶段）

### P10 — Enterprise / SaaS

- [x] **P10.1 Identity 基础设施 ✅**：User/Role/Permission/UserRole/RolePermission 领域模型 + SuperBIContext DbSet/配置/查询过滤(TenantId=0 全局放行) + 迁移 `20260830093125_P10_1_Identity` + `IIdentityService`/`IdentityService`（幂等种子 + 确定性 RBAC 权限解析 + 角色指派/撤销，不调 LLM）+ `IdentityCatalog` 全局角色权限目录 + Program.cs 注册 + 启动期幂等种子(try/catch 不阻断)。单测 `IdentityServiceTests` 9 项。**build 0 error；单测 240/240；Golden 18/18 PASS**。
- [x] **P10.2 Identity API 端点 ✅**：`IdentityController`（`api/identity`）租户作用域 用户/角色/权限 CRUD + 角色指派/撤销 + 权限解析 + 全局目录守卫。单测 `IdentityControllerTests` 16 项（镜像 P9.3 范式）。**build 0 error；单测 256/256；Golden 18/18 PASS**
- [x] **P10.3 AuditLog 审计日志 ✅**：`AuditLog`（`src/Domain/Audit`）+ SuperBIContext DbSet/配置/查询过滤(TenantId=0 全局放行) + 迁移 `20260830104059_P10_3_AuditLog` + `IAuditLogService`/`AuditLogService`（结构化记录 谁/什么/何时/结果 + 租户作用域查询，确定性、不调 LLM）+ `AuditController`（`api/audit` 租户作用域查询 + 手动记录）+ `AuditMiddleware`（自动请求级审计，非阻塞、异常静默）。单测 `AuditLogServiceTests` 8 项 + `AuditControllerTests` 4 项。**build 0 error；单测 268/268；Golden 18/18 PASS**。
- [x] **P10.4 Billing/Quota 账单与配额 ✅**：`QuotaPolicy`/`QuotaUsage`（`src/Domain/Quota`）+ SuperBIContext DbSet/配置/查询过滤(QuotaPolicy 放行 TenantId=0 平台默认 / QuotaUsage 仅本租户) + 迁移 `20260830111544_P10_4_Quota` + `IQuotaService`/`QuotaService`（幂等种子平台默认配额 + 租户覆盖优先回退 + 按周期键 Total/Monthly/Daily 滚动归零 + Check/Consume enforcement，确定性、不调 LLM）+ `QuotaController`（`api/quota` 概览/单资源/校验/扣减，TenantId<=0 拒绝 400）。单测 `QuotaServiceTests` 9 项 + `QuotaControllerTests` 8 项。**build 0 error；单测 285/285；Golden 18/18 PASS**。
- [x] **P10.5 Observability 中间件 + 架构依赖校验（反射等价 NetArchTest）+ P10 总验收 ✅**：`ObservabilityMiddleware`（关联ID透传 + 请求/响应日志 + 耗时，非阻塞静默）+ 依赖方向校验（Domain/Ports/Services/Controllers 不反依赖外层；NuGet 镜像缺 NetArchTest 故改反射实现）+ 安全/审计基线测试（RBAC deny-by-default + 租户隔离 + 审计记录/查询往返 + 配额平台默认 enforcement）
- [x] **验收 ✅**：build 0 error + **Golden 18/18** + 安全/审计基线测试（单测 294/294）

---

### P11 — 前端 + 平台扩展（方案 `docs/P11_Frontend_MAUI_Blazor_Plan.md`）

**P11.0 后端前置（✅ 已完成 · 全为新增文件 + Program.cs 编辑，未碰 Golden 依赖 · build 0 error · 单测 308/308 · Golden 18/18 PASS）**

- [x] `src/Application/Auth/TokenService.cs`：`ITokenService`（`Issue`/`Validate`）+ `TokenPrincipal`；HMAC-SHA256 手动 JWT 风格无状态令牌，**零 NuGet 依赖**
- [x] `src/Api/Middleware/AuthMiddleware.cs`：Bearer/X-Api-Token 解析 → 校验 → 设 `HttpContext.User` + `Items["TenantId"]`；匿名白名单（`/evaluation`、`/health`、`/api/auth/login`、`/`、静态资源，**保证 Golden 不受影响**）；其余 `/api/*` 无令牌 → 401
- [x] `src/Api/Middleware/RateLimitMiddleware.cs`：`/api/*` 按 IP/令牌固定窗口限流（120/分），异常静默
- [x] `src/Api/Controllers/AuthController.cs`（`api/auth`）：`POST /login {username,tenantId}` → 查活跃用户 → `GetPermissionsAsync` → 签发令牌；`GET /me`（本阶段登录不校验口令，口令/外部 IdP 在 P13 BYO 补齐）
- [x] `src/Api/Controllers/AskController.cs`（`api/ask`）：`POST {question,dataSourceId?}` → 需有效令牌 + `DashboardView` 权限（deny-by-default 与 P10 一致）→ `IBIConversationService.AskAsync(question, tenantId)`；租户隔离由令牌 `tid` 声明驱动
- [x] `src/Api/Program.cs`：注册 `ITokenService` 单例（密钥 `Auth:SigningKey`，缺失用 dev 默认）+ CORS 策略 `P11Cors`；中间件顺序 `Routing → Cors → RateLimit → Auth → Authorization → Observability → Audit`；`MapGet("/health")`
- [x] 测试（确定性、无 LLM/DB）：`TokenServiceTests`(5) + `AuthMiddlewareTests`(5) + `AskControllerTests`(4)
- [x] **验收**：build 0 error（仅 12 个预存 nullable 警告）；单测 308/308（原 294 + 14）；**Golden 18/18 PASS（decision=PASS, expectedOutcomePassed 18, expectedOutcomeFailed 0, failedGates=None）**

**P11.1~P11.5 前端（MAUI Blazor Hybrid + Blazor Web 共享 RCL）**

- [x] **P11.1 脚手架 ✅**（三个新项目 build 0 error；现有 API src 零改动；Golden 18/18 不受影响）：
  - [x] RCL `SuperBuilder_AI.Components`（net10.0 Razor Class Library，零 NuGet 依赖除 Components.Web/Http）：`Routes`/`MainLayout`/`NavMenu` + 全页面占位（`Login`/`Ask` 为真实调用 `api/ask`·`api/auth`，其余占位）+ `ApiClient`(`IApiClient`)+`AppState`+`ThemeService`+`LocalizationService` 运行时骨架 + `wwwroot/css/app.css` + `wwwroot/js/chart.js`
  - [x] Web Head `SuperBuilder_AI.Web`（**Blazor Server 经典模型**：`_Host.cshtml` HTML 壳 + `MapBlazorHub` + `MapFallbackToPage`；Router 经 `AdditionalAssemblies` 扫描 RCL 页面；`dotnet run` 验证首页/CSS/JS 均 HTTP 200）
  - [x] MAUI Head `SuperBuilder_AI.Maui`（MAUI Blazor Hybrid，Windows 目标 `net10.0-windows10.0.19041.0` 编译 **0 error**；`MainPage.BlazorWebView` 以 RCL `Routes` 为 RootComponent、`wwwroot/index.html` 为 HostPage；`WindowsPackageType=None` 免打包）
  - [x] 图表库选型：**Chart.js**（轻量、纯 JS、Web 与 MAUI WebView 双端通用；RCL `ChartView.razor` 经 JS 互操作封装，P11.2 细化）
  - [x] 解决方案 `SuperBulider_AI.slnx` 已纳入三个新项目（注：文件名拼写沿用历史，未改名以免破坏现有引用）
- [x] **P11.2 旗舰页 ✅**（三个 Head 零后端改动；现有 API src 零改动；Golden 18/18 不受影响；三项目 build 0 error；Web Head 运行时 `/`·`/ask`·`/login` 均 200、RCL 静态资源 200）：
  - [x] `Models/BIResponse.cs`：对齐后端契约（BIResponse/QueryResult/QueryAnswer/VisualizationSuggestion）+ `JsonValue` 辅助（JsonElement→字符串/数值）；`ApiClient` 增加类型化 `AskAsync`(返回 AskOutcome) 与 `PublishAppAsync`(POST `api/apps`)
  - [x] Chart.js **本地化**：`wwwroot/js/chart.umd.min.js`（v4.4.1，205KB，Web/MAUI 离线可用）；重写 `chart.js` 的 `renderChart(canvas, spec)` 支持 type/data/datasets/options 且管理实例生命周期（重渲染前 destroy）；Web `_Host.cshtml` 与 MAUI `index.html` 显式引入 `chart.umd.min.js`+`chart.js`（RCL JS 不会自动加载）
  - [x] `ChartView.razor` 重写：参数化图表规格（Type/Labels/Datasets/Title/ShowLegend/Palette），参数变化即即时重渲染
  - [x] `Ask.razor` 重写（旗舰）：多轮对话列表 + 端到端渲染（AI 解读 / KPI 卡(Summary) / Chart.js 图表(Visualizations) / 数据表 / SQL 折叠）+ **视图层多轮调整**（每图工具栏：柱状/折线/饼图·图例开关·换配色；+ 自然语言指令框「改成柱状图/隐藏图例/配色换绿」→ 纯前端 DSL 变更、零后端调用、即时重渲染）+ **发布为应用**（序列化为 App DSL v1.0 POST `api/apps`，复用 P8 `BuildFromDslAsync` 确定性路径）
  - [x] `app.css` 补充：KPI 卡 / 数据表 / 对话气泡 / 答案块 / 视图工具栏 / 迷你按钮 等
  - [x] **本轮增强（2026-08-31）统一错误治理 + 古风样式**：后端新增 `ApiErrors`（ErrorCodes/SuperBuilderException/ApiError）+ `UnifiedExceptionMiddleware`（捕获未处理异常→结构化 `ApiError{code,message,traceId,details}` 友好 JSON + 关联ID 结构化日志，按异常类型/消息映射错误码，**零业务抛点改动、不影响 Golden**）+ `AuthMiddleware` 401 也统一为 `ApiError`；Ask 控制器早期返回统一 `ApiError`。前端 `ApiClient.ParseApiError` 解析 + `Ask.razor` 展示「错误码+追踪ID」古风告警。
  - [x] **本轮增强 Bootstrap + 中国古风色系**：本地化 Bootstrap 5.3.3（`wwwroot/lib/bootstrap`，CSS 232KB/JS 80KB，离线可用）并在 `_Host.cshtml`/`index.html` 引入；`app.css` 重写为古风配色（宣纸底/墨字/黛蓝主/朱砂强调/藤黄/石青/黛绿），覆盖 Bootstrap `--bs-*` 变量，含 dark 古风变体 + 响应式（窄屏侧栏转顶栏）；Login/Ask/NavMenu 套用 `btn`/`form-control`/`alert` 等 Bootstrap 组件。
  - [x] **UI 现代化重构（2026-08-31，本轮）布局/PC·移动兼容/现代化**：重做应用壳层 `MainLayout`（顶栏=汉堡+品牌+主题切换+用户芯片 + 全高 flex 主体）；`NavMenu` 加 Feather 风格 SVG 图标精灵 + 分组；**≤991px 侧栏变抽屉 + 遮罩**（路由变更自动收起）；`Ask.razor` 提问栏 `sticky` 置顶 + 气泡头像 + KPI 网格/图表卡/表格层次阴影；`Login.razor` hero 分栏（品牌侧+表单侧，移动端堆叠）；明/暗主题切换经 `ThemeService` 持久化 localStorage，`_Host`/`index` 内联脚本防首屏闪烁。**注意 Blazor Server 预渲染阶段无 JS 运行时，主题读取须放 `OnAfterRenderAsync` 否则 `/` 500**。
- [x] **P11.3 其余页面 + 组件库页 + 主题编辑器 ✅（2026-08-31，页面部分）**：新建共享 `PageHead`（图标+标题+描述+右侧操作区）与 `TablePresenter`（任意 JSON 数组 → 友好表头/单元格/状态徽章；`InferColumns`/`Cell`/`Friendly`/`StatusBadge`/`IsStatusColumn`/`StringifySafe`）；`IApiClient`+`ApiClient` 新增松类型 `GetJsonAsync`（不抛异常，HTTP 非 2xx 与网络/解析错误一律经 err 返回，便于页面优雅降级）。
  - [x] **数据类页面**（接后端只读 GET，统一加载/错误/空态 + 响应式）：`Dashboards`(api/dashboards) · `Apps`(api/apps，卡片网格) · `BusinessModel`(api/business-model/entities+domains，统计卡 + 业务域列表 + 实体表) · `SemanticLabels`(api/semantic-labels，搜索 + 表格) · `Agent`(api/agent + api/agent/tools-catalog，智能体列表 + 工具目录)
  - [x] **平台类页面**：`DataSources`（连接器网格 + 测试连接表单）· `ModelAccounts`（模型卡片 + BYO Key 绑定表单）· `Components`（组件库展示页，自包含）· `Themes`（主题编辑器：调色板实时预览 + 明/暗预设切换）
  - [x] **管理后台**：`Admin/Tenants` · `Admin/Identity`(api/identity) · `Admin/Audit`(api/audit) · `Admin/Quota`(api/quota) · `Admin/Localization`(api/localization) · `Admin/Themes`
  - [x] 设计系统扩展：`app.css` 新增 `page-head`/`stat-grid`/`stat-tile`/`panel`/`toolbar`/`badge`/`empty-state`/`field-grid`/`seg`/`row-list`/`swatch-grid` 等工具类；`NavMenu` 图标精灵补 `sb-ico-search`，现共 22 个 symbol
  - [x] 三项目 build 0 error；Web 冒烟 **18 个页面全 200** + 静态资源（app.css/Bootstrap/chart.js）200；引用的 18 个图标全部有定义
  - [x] **Razor 踩坑记录**：含 C# 字符串字面量的事件处理器必须用单引号作属性定界符（`@onclick='() => Toast("x")'`）；渲染名为 `code` 的变量必须写 `@(code)`，否则 `@code</span>` 被当作 `@code` 指令；void 方法直接绑定需包成 lambda（`@onclick='() => Toast("x")'` 而非 `@onclick='Toast("x")'`）
- [x] **ask/refine（多轮语义调整）✅（2026-08-31）**：后端新增 `POST api/ask/refine`（`AskController.Refine` + `AskRefineRequest`/`AskRefineTurn`）；`ComposeRefinedQuestion` 将「原始问题 + 历史(user 轮次) + 指令」合成为独立中文问题再复用既有 BI 链路。**门控隔离**：仅显式调用该端点时启用，默认 `api/ask` 路径逐字节不变，不触碰 Golden 依赖文件。`Ask.razor` 新增「语义细化」输入框，结果作为子轮次（`IsRefine` 左侧高亮）追加，共享 `ApplyOutcome` 错误处理。三项目 build 0 error；`api/ask/refine` 已注册可达（缺令牌返回 400，与 `api/ask` 一致的非回归鉴权行为）。
- [x] **P11.4 MAUI 验证 ✅（2026-08-31，配置就绪 + 双端现状）**：
  - **Windows 端**：`SuperBuilder_AI.Maui -f net10.0-windows10.0.19041.0` 构建 **0 error**（复验通过）；`MauiProgram.cs` 用 `AddMauiBlazorWebView()` + 注册 `IApiClient/AppState/ThemeService` 等同 Web 的 Scoped 服务；`wwwroot/index.html` 引用与 Web 完全相同的 RCL 资源（Bootstrap/app.css/chart.js/blazor.webview.js）并含 no-FOUC 主题脚本。
  - **共享 RCL 正确性**：MAUI WebView 渲染的组件与 Web Head 完全同源；Web 已渲染 18 页面全 200 → 组件层面对 MAUI 同样成立（0 error 已证）。
  - **Android/iOS 端（已打通原生构建，2026-08-31 本轮）**：RCL 改多目标 `net10.0;net10.0-android;net10.0-ios`，MAUI 头改 `net10.0-android;net10.0-ios;net10.0-windows10.0.19041.0`，补齐 `Platforms/Android`（MainActivity/MainApplication/AndroidManifest）与 `Platforms/iOS`（AppDelegate/Program）、`Resources/AppIcon`、`Resources/Splash`。
    - **Android ✅ 真原生产物**：`dotnet build -f net10.0-android` **0 error**，产出 `SuperBuilder_AI.Maui.apk` + `-Signed.apk`（约 16MB）。
    - **iOS ✅ 编译链打通**：`dotnet build -f net10.0-ios` **0 error**，完成托管编译 + AOT + 原生静态库链接（`libextension-dotnet.a`/`libmono-*`）+ 多语言资源收集 + codesign 清单；**最终 `.app` 打包/签名需配对 Mac 宿主**（Windows 上 `_CreateAppBundle` 为空操作），属平台限制非代码缺陷。
  - **⚠️ 关键坑：RCL 不可引用 `Microsoft.AspNetCore.Components.WebView.Maui`**。该包会随 RCL 发布 `_framework/blazor.modules.json` 等静态宿主资源，与引用 RCL 的 MAUI 应用自身资源冲突（`StaticWebAsset SourceType: Project` 重复，且 `StaticWebAsset Remove` 无法拦截——包资源在构建期才注入）。RCL 统一只引 `Microsoft.AspNetCore.Components.Web` + `Microsoft.Extensions.Http`（全 TFM），WebView 宿主能力由 MAUI 头项目自身提供。
  - **运行时配置提示**：`MauiProgram.cs` 中 `HttpClient.BaseAddress = https://localhost:5032`；Android 模拟器内 `localhost` 指向模拟器自身，真机/模拟器联调应改为 `http://10.0.2.2:5032`（宿主回环），待 P11.5 或真机联调时处理。
- [x] **P11.5 优化轨道 ✅（2026-08-31，性能/成本 + 安全/运维 双轨起步）**：
  - **性能/成本 — Ask 语义响应缓存（P11.5.1）**：`src/Api/Caching` 新增 `AskCacheOptions`（绑定 `P11Cache:Ask`，默认启用/TTL 60s/LRU 200）+ `IAskResponseCache` + `MemoryAskResponseCache`（基于 `IMemoryCache`）。键 = 租户 + 归一化问题（`Normalize` 折叠空白/去尾部标点/小写；GUID 与数字段已在 metrics 侧占位）；**只缓存 `BIResponse.Success==true`**（失败/被闸门阻断不入库，避免瞬时故障被钉死）；读写全程异常静默。接入点仅 `AskController`（可选构造参数 `IAskResponseCache? cache=null`，保持现有 2 参单测不破），支持 `?noCache=1` 旁路 + `X-Cache: HIT/MISS` 响应头。命中即跳过整条 BI 链路（省 LLM+DB）。**零回归**：Golden 走独立 `evaluation/golden-runtime` 端点、不经 `AskController`，缓存对其完全不可见。
  - **安全/运维 — 请求指标 + /metrics（P11.5.2）**：`ObservabilityMiddleware` 注入 `RequestMetricsCollector`（可选参数，未注册降级不采集），按「路由」聚合请求数/错误数(>=500)/客户端错(400-499)/平均延迟/**P95**/最大延迟；路由归一化（GUID→`{guid}`、数字段→`{n}`、限 200 桶防基数爆炸、每路由 512 样本环形）。新增 `GET /metrics` 匿名端点，输出 `routes[]` 与 `askCache{hits,misses,hitRate}`。后端 build 0 error；单测 **308/308**；端点实测 `/health`→被 `/metrics` 采集、`/api/ask` 无令牌仍 401（鉴权未破）。
  - **安全/运维 — 鉴权收尾（P11.5.3，2026-08-31）**：核心守卫（`AuthMiddleware` + `TokenService` HMAC 无状态令牌）**早已在 P11.0 落地并接进管道**（计划文档 §9.3/第 227 行「无鉴权中间件」为过时记录，已修正）。本轮补全「生产可用收口」：① `appsettings.json` 配置 `Auth:SigningKey`（强随机密钥），消除 `TokenService` 回退到硬编码 dev 默认值导致**可伪造 token** 的漏洞；② 前端 `AuthStore`（RCL，Scoped）落地的 `localStorage` 持久化 + 启动经 `GET /api/auth/me` 自举校验（刷新不掉登录）；③ `ApiClient` 在 401 时清 token 并触发 `AppState.SessionExpired`，`MainLayout` 订阅后跳 `/login`（过期会话自动回收）。三端构建 0 error（API/Web/MAUI-Win）；单测 **308/308**；运行时实测：无 token→401、坏 token→401、合法 token→200（`/api/auth/me` 返回正确声明）、`/metrics`→200。Golden 走独立 `evaluation/golden-runtime` 端点，与本链路完全隔离。
  - **待续**：BI 查询执行优化（分页/流式/连接池/索引）、前端体验细化、配置多环境/密钥管理、OpenAPI/Swagger（§9.4 暂缓项）。

**§12 用户自定义能力（接入 P11.3，已规划）**

- [x] ① 用户自定义组件库 —— **已于 M7-09 交付（2026-09-07）**：`Domain/Components/CustomComponentDefinition.cs` + `CustomComponentVersion` + `Application/Components/CustomComponentDslSerializer.cs` + `Api/Controllers/ComponentsController.cs` @ `api/components` + 迁移 `20260907053931_M7_09_CustomComponents` + `tests/CustomComponentsTests.cs`；前端 `ComponentGallery.razor` 已接真实编辑器。**剩余缺口**：App DSL 的 `AppComponentTypes` 为 6 类硬编码白名单，尚无 `CustomComponentRef` 机制（归 App DSL 演进，见 M12 增量项）
- [x] ② 用户自定义风格/样式 —— **已于 M7-10 交付（2026-09-07）**：自定义主题在 App / Dashboard / 主题资产间完成可授权复用，`AppDsl.ThemeKey` 引用链路打通；前端 `Themes.razor` 管理页就位
- [x] ③ Ask 多轮调整 + 发布为应用页面 —— **已于 M7-11 C3/C4 交付（2026-09-08）**：发布改「创建草稿 `POST api/apps` → `POST api/apps/{code}/publish`」两段式，修复假成功；`AskTurn` 增 `PublishStage`/`PublishCode` 支持重试复用

**§13 平台扩展（已规划）**

- [ ] 多类型数据库连接器（复用 `ISqlDialect`/`IDataSourceConnectionFactory`）
- [ ] 多 AI 模型 BYO（用户自绑定账号；含 P11.0 登录口令/外部 IdP 补齐）

---

## 统一护栏（贯穿所有阶段）

- [ ] 每个 P 阶段退出 = `Golden 18/18`
- [ ] 触及 `src/Application/BI`、`src/Domain/{Metadata,BiQuery}`、`src/Infrastructure/{Vector,Database}`、Resolution 的每次提交都重跑 Golden
- [ ] 契约文件 `Evaluation/Golden/query-plan-golden-v1.json` 不删不改
- [ ] 所有大改均为 git 提交，可 `git revert` 回退
- [ ] A4 上帝类拆分必须在 P6 之前收口

---

**立即下一步**：P3~P10 ✅ 全绿（用户产品路线 10 阶段全部完成，Golden 18/18 全程保持）。**P11.0 后端前置 ✅ 已完成**（api/ask + 鉴权中间件 + CORS + 限流 + /health；build 0 error；单测 308/308；Golden 18/18 PASS）。**下一步 P11.1 前端脚手架**（RCL + Blazor Web Head + MAUI Head，现有 API 零改动）。A3/A5 用户决定暂缓。
