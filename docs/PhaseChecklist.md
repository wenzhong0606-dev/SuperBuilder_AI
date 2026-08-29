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

### P5 — Multi-Language Runtime
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
**P5.3 本地化资源 + 语义标签解析服务 + 链路接入（⬜ 待做 · 高风险）**
- [ ] `Localization` 资源（zh-CN/zh-TW/en-US/ja-JP/ko-KR）
- [ ] 语义标签解析服务接入元数据/向量检索（高风险，必跑 Golden）
**P5.4 AI 意图语言无关化 + 多语言 Golden 复跑 + 验收（⬜ 待做）**
- [ ] AI 意图理解语言无关化（问句归一化到语义概念，剥离语言相关表述）
- [ ] **P5 总验收**：build 绿 + **Golden 18/18**（多语言问句重跑）+ 语义映射测试

### P6 — Low-code BI Engine ⚠️（前置：A4 完成）
- [ ] `Dashboard/Page/Widget/Chart/Table/KPI/Filter/Text/AI Insight/Query` DSL 模型
- [ ] `DashboardDSL` 序列化 + `LowcodeRenderer`
- [ ] `DashboardController`（生产）+ 编辑器前端占位
- [ ] **验收**：build 绿 + **Golden 18/18** + DSL 渲染冒烟

### P7 — Multi-Theme / Style Engine
- [ ] `Theme` 聚合（Brand/Color/Typography/Layout/.../ChartPalette/Component/DashboardTemplate）
- [ ] `ThemeContext` 接入 PlatformContext；Tenant→Theme→Workspace→Dashboard 级联
- [ ] **验收**：build 绿 + **Golden 18/18** + 主题切换渲染测试

### P8 — AI App Builder
- [ ] `AppPlan/PagePlan/ComponentPlan` 模型
- [ ] `AppBuilderAgent` 编排
- [ ] `AppBuilderController`
- [ ] **验收**：build 绿 + **Golden 18/18** + 端到端应用生成冒烟

### P9 — AI Agent / Copilot
- [ ] `AgentPlanner` / `ToolRegistry` / `AgentController`
- [ ] 异常检测/原因分析链路（销售额→同比→环比→区域→客户→产品→渠道）
- [ ] **验收**：build 绿 + **Golden 18/18** + 工具选择测试

### P10 — Enterprise / SaaS
- [ ] `Identity`(User/Role/Permission) / `AuditLog` / `Billing`/`Quota`
- [ ] `Observability` 中间件；CI 架构依赖校验（NetArchTest）
- [ ] **验收**：build 绿 + **Golden 18/18** + 安全/审计基线测试

---

## 统一护栏（贯穿所有阶段）
- [ ] 每个 P 阶段退出 = `Golden 18/18`
- [ ] 触及 `src/Application/BI`、`src/Domain/{Metadata,BiQuery}`、`src/Infrastructure/{Vector,Database}`、Resolution 的每次提交都重跑 Golden
- [ ] 契约文件 `Evaluation/Golden/query-plan-golden-v1.json` 不删不改
- [ ] 所有大改均为 git 提交，可 `git revert` 回退
- [ ] A4 上帝类拆分必须在 P6 之前收口

---
**立即下一步**：P3 ✅、P4 Multi-Tenant Platform Core ✅ 全绿（P4.1~P4.4 均 ✅）。A3/A5 用户决定暂缓。**P5 Multi-Language Runtime 进行中**：**P5.1 LocaleContext + PlatformContext 接入 ✅（单测 26/26，Golden 18/18 PASS）**、**P5.2 语义多语言标签持久化 ✅（SemanticLabel + Migration，单测 36/36，端点读写往返 200，Golden 18/18 PASS）**；下一步 **P5.3 本地化资源（zh-CN/zh-TW/en-US/ja-JP/ko-KR）+ 语义标签接入元数据/向量检索（高风险，必跑 Golden）**，随后 P5.4 意图语言无关化与多语言 Golden 复跑。每步 build + Golden。
