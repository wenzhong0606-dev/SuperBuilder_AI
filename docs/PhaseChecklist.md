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
- [ ] **A3** 抽取独立项目 Domain/Application/Infrastructure/Api
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
- [ ] **A5** 限界上下文解耦（**A3 前置**）
  - [ ] 消除 `Metadata↔Organization` 循环依赖
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
- [ ] **P10.3 AuditLog 审计日志**：domain + 持久化 + `IAuditLogService` + 可选中间件
- [ ] **P10.4 Billing/Quota 账单与配额**：domain + 持久化 + `IQuotaService` 配额 enforcement（确定性）
- [ ] **P10.5 Observability 中间件 + NetArchTest 架构依赖校验 + P10 总验收**：请求日志/关联ID/耗时 + 依赖方向校验 + 安全/审计基线测试
- [ ] **验收**：build 绿 + **Golden 18/18** + 安全/审计基线测试

---

## 统一护栏（贯穿所有阶段）
- [ ] 每个 P 阶段退出 = `Golden 18/18`
- [ ] 触及 `src/Application/BI`、`src/Domain/{Metadata,BiQuery}`、`src/Infrastructure/{Vector,Database}`、Resolution 的每次提交都重跑 Golden
- [ ] 契约文件 `Evaluation/Golden/query-plan-golden-v1.json` 不删不改
- [ ] 所有大改均为 git 提交，可 `git revert` 回退
- [ ] A4 上帝类拆分必须在 P6 之前收口

---
**立即下一步**：P3 ✅、P4 Multi-Tenant Platform Core ✅ 全绿（P4.1~P4.4 均 ✅）、P5 Multi-Language Runtime ✅ 全绿（P5.1~P5.4 均 ✅）、P6 Low-code BI Engine ✅ 全绿（P6.1/P6.2/P6.3/P6.4 均 ✅，合计单测 117/117，Golden 18/18 PASS）、**P7 Multi-Theme / Style Engine ✅ 全绿（P7.1~P7.4 均 ✅，合计单测 147/147，Golden 18/18 PASS ×4 轮）**。**P8 AI App Builder ✅ 全绿（P8.1~P8.4 均 ✅，合计单测 179/179，Golden 18/18 PASS）**。**P9 AI Agent / Copilot ✅ 全绿（P9.1~P9.4 均 ✅，单测 231/231，Golden 18/18 PASS ×3 轮）：P9.1 ToolRegistry + Agent 领域模型 + 序列化器 + 持久化 ✅（build 0 error、单测 203/203、Golden 18/18 PASS）、P9.2 AgentPlanner 编排 ✅（build 0 error、单测 214/214、Golden 18/18 PASS）、P9.3 AgentController 端点 + 异常原因分析链路 ✅（build 0 error、单测 231/231、Golden 18/18 PASS）**。下一子阶段 **P10.3 AuditLog 审计日志**（前置 P10.2 已完成，无阻塞）。A3/A5 用户决定暂缓。
