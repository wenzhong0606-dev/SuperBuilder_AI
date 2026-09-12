> **HISTORICAL SNAPSHOT / 历史快照**：本文仅记录当时的工作状态、判断或证据，不是当前缺陷清单、当前测试基线、当前 HEAD 或当前执行计划。任何“当前/唯一/已完成/未完成/风险”等表述均按本文记录日期理解；现状必须按 `docs/README.md` 的治理顺序重新核验：当前源码/测试 → Master → Milestone → Active Plan → Backlog。\n\n# SuperBuilder AI Native BI — 逐文件逐行代码审计报告

**审计日期**: 2026-08-26  
**审计范围**: 206 个源文件, 30,102 行 C# 代码  
**审计方法**: 逐文件逐行读取源码, 交叉比对 DI 注册、接口定义和开发计划

---

## 一、总体代码健康度

| 指标 | 数值 |
|---|---|
| 源文件总数 | 206 |
| 总代码行数 | 30,102 |
| TODO/FIXME/NotImplemented | **0** (无桩代码) |
| 空 catch 块 | **9+** 处 |
| Console.WriteLine (生产代码) | **1** 处 |
| 死代码文件 | **1** 个 |
| DI 注册缺失 | **7** 处 |
| 逻辑错误/设计缺陷 | **12** 处 |

**整体评价**: 代码完成度很高,没有 TODO/FIXME/NotImplemented 标记,核心链路全部实现。但存在若干 DI 注册缺失、空 catch 块吞噬异常、硬编码值等问题。

---

## 二、严重问题 (Critical)

### C1. BIConversationService 未注册 DI

**文件**: `Program.cs`  
**问题**: `BIConversationService`（主编排服务,581行完整实现）及其接口 `IBIConversationService` **完全没有在 DI 容器中注册**。  
**影响**: 没有任何 Controller 或服务可以通过构造函数注入使用 BI 查询主链路。整个 BI 查询流水线虽然代码完整,但无法通过标准 DI 方式被调用。  
**修复**:
```csharp
builder.Services.AddScoped<BIConversationService>();
builder.Services.AddScoped<IBIConversationService>(sp => sp.GetRequiredService<BIConversationService>());
```

### C2. 多个接口未注册 DI (BIConversationService 依赖链断裂)

**文件**: `Program.cs`  
**问题**: BIConversationService 构造函数依赖以下接口/类型,但它们未正确注册:

| 依赖 | 当前注册状态 | 需要修复 |
|---|---|---|
| `IQueryPlanExplainabilityService` | 仅注册了具体类 `QueryPlanExplainabilityService` | 需补充接口注册 |
| `ISqlDialectResolver` | 仅注册了具体类 `SqlDialectResolver` | 需补充接口注册 |
| `IResultUnderstandingService` | **完全未注册** | 需新增注册 |
| `QueryPlanMetadataValidator` (具体类) | **完全未注册** | 需新增注册 |
| `IMetadataPromptBuilder` | 仅注册了具体类 `MetadataPromptBuilder` | 需补充接口注册 |
| `IMetadataSearchService` | **完全未注册** | 需新增注册 |

**影响**: 即使注册了 BIConversationService,在解析依赖链时 DI 容器会抛出异常。

### C3. 死代码: 重复的 QueryIntentNormalizer

**文件**:  
- `Services/AI/QueryUnderstanding/QueryIntentNormalizer.cs` (210行) — **死代码**  
- `Services/BI/Planning/Intent/QueryIntentNormalizer.cs` (179行) — **实际使用**  

**问题**: 两个不同命名空间的 `QueryIntentNormalizer` 类同时存在。`Program.cs` 和 `QueryUnderstandingService` 通过 `using SuperBuilder_AI.Services.BI.Planning` 引用的是后者。前者（AI/QueryUnderstanding）是完全的死代码,从未被任何文件引用。  
**影响**: 维护混乱。前者包含 `InferMetricAggregation` 等后者没有的逻辑,容易造成混淆。  
**修复**: 删除 `Services/AI/QueryUnderstanding/QueryIntentNormalizer.cs`,如有独有逻辑需迁移到活动版本。

---

## 三、逻辑错误与设计缺陷 (Major)

### M1. QueryPlanBuilder 直接修改输入的 QueryIntent

**文件**: `QueryPlanBuilder.cs`  
**行号**: L610-614, L1015-1017, L1090-1092  
**问题**: `BuildAsync` 方法直接修改传入的 `intent.OrderBy`、`intent.OrderDirection`、`intent.Dimensions[i]`。这违反了不可变输入原则,可能导致调用方持有的 intent 对象被意外篡改。  
**影响**: 在 Repair Loop 中如果重新使用原始 intent,可能使用到被篡改的值。

### M2. 硬编码时间字段名

**文件**: `QueryPlanBuilder.cs`  
**行号**: L551-558  
**问题**: 硬编码了 `create_time`, `come_time`, `update_time`, `affirm_time`, `created_time`, `createdat`, `created` 作为时间候选字段。  
**矛盾**: 项目设计原则明确要求"禁止硬编码物料、供应商等业务表名或字段名"。时间字段名同样是业务特定的,不应硬编码。  
**修复**: 应从 Metadata 的 DataType 中动态识别时间类型字段。

### M3. 硬编码语义搜索阈值

**文件**: `QueryPlanBuilder.FieldResolution.cs`  
**行号**: L342-346, L363  
**问题**: `best.LexicalScore >= 0.8` 和 `best.SemanticScore >= 0.30` 是硬编码阈值,注释自己说"后续可以配置化"。  
**影响**: 不同 Embedding 模型的 Score 分布不同,固定阈值可能导致误匹配或漏匹配。

### M4. 空 catch 块吞噬异常 (9+处)

**文件**: `QueryPlanBuilder.cs` (L733), `QueryPlanBuilder.TableSelection.cs` (L199, L555, L570, L660, L669), `QueryPlanBuilder.Diagnostics.cs` (L92, L116, L121), `QueryPlanBuilder.FieldResolution.cs` (L35)  
**问题**: 9+处 `catch { }` 或 `catch { /* 注释 */ }` 完全吞噬所有异常,包括 `OutOfMemoryException`、`StackOverflowException` 等严重异常。  
**影响**: 生产环境出错时无法诊断。  
**修复**: 至少应记录日志 `logger.LogWarning(ex, "...")`,或使用 `catch (Exception ex)` 并记录。

### M5. Console.WriteLine 用于生产诊断

**文件**: `QueryPlanBuilder.TableSelection.cs`  
**行号**: L188-197  
**问题**: 使用 `Console.WriteLine` 输出诊断信息,而非 `ILogger<T>`。  
**影响**: 日志不可控,无法配置级别,生产环境输出垃圾信息。

### M6. NormalizeOperator 行为不一致

**文件**: `QueryPlanBuilder.Helpers.cs` (L113-116) vs `SqlQueryBuilder.cs` (L343-363)  
**问题**: `QueryPlanBuilder.NormalizeOperator` 对未知操作符 **抛出异常**,而 `SqlQueryBuilder.NormalizeOperator` 对未知操作符 **默认为 "="**。  
**影响**: 同一个 filter.Operator 在不同阶段行为不一致,可能导致构建阶段通过但 SQL 阶段行为不同,或反之。

### M7. SqlQueryBuilder 双路径回退

**文件**: `SqlQueryBuilder.cs`  
**行号**: L266-273, L317-323  
**问题**:  
- GROUP BY: 先用 `plan.Dimensions`,为空时回退到 `plan.Intent.Dimensions`  
- ORDER BY: 先用 `plan.Orders`,为空时回退到 `plan.Intent.OrderBy`  
**影响**: 两条路径可能产生不一致的 SQL,且 Debug 时难以追踪实际使用了哪条路径。应统一为只用 `plan.Dimensions` / `plan.Orders`。

### M8. DimensionResolutionEvidenceService 重复搜索

**文件**: `DimensionResolutionEvidenceService.cs`  
**行号**: L47, L90-92  
**问题**: L47 已经用 `dimensionSemanticText` 搜索了 20 条结果,当 `keyCandidates.Count == 0` 时 L92 用相同参数再搜索一次。第二次搜索的结果可能不同(向量搜索可能有随机性),但逻辑上应该使用同一批结果。  
**修复**: 应复用 L47 的搜索结果,或缓存搜索结果。

### M9. LooksLikeIdentifier 过于宽泛

**文件**: `DimensionResolutionEvidenceService.cs`  
**行号**: L293-297  
**问题**: `LooksLikeIdentifier` 检查列名是否以 "id", "code", "key", "no" 结尾。这会错误匹配 `paid`, `said`, `valid`, `rainbow`, `piano` 等非标识符字段。  
**影响**: 可能导致非标识符字段被误判为 Association Key,触发错误的 DirectKey 分支。

### M10. ResolveMetricField "id" 后缀匹配过于宽泛

**文件**: `QueryPlanBuilder.FieldResolution.cs`  
**行号**: L26  
**问题**: `nf.EndsWith("id")` 会匹配任何以 "id" 结尾的文本,包括 "paid", "void", "avoid" 等。  
**影响**: 可能导致非 ID 字段被误当作主键处理。

### M11. CalculateTextOverlap O(n^3) 性能问题

**文件**: `QueryPlanBuilder.FieldResolution.cs`  
**行号**: L821-903  
**问题**: `CalculateTextOverlap` 使用两层嵌套循环 + `Contains` 检查,复杂度约 O(n^2 * m)。对于长文本(如 Semantic.BusinessMeaning 可能是长文本),性能可能显著下降。  
**影响**: 在高频查询场景下可能导致延迟。

### M12. QueryPlanBuilder.PlanAssembly.cs 硬编码 JOIN 数量上限

**文件**: `QueryPlanBuilder.PlanAssembly.cs`  
**行号**: L208  
**问题**: `Take(2)` 硬编码最多 2 个 JOIN,不可配置。  
**影响**: 复杂查询可能需要更多 JOIN,但被硬限制。

---

## 四、模块完成度明细

### 4.1 Services/BI (核心业务逻辑)

| 文件 | 行数 | 完成度 | 说明 |
|---|---|---|---|
| QueryPlanBuilder.cs | 1302 | **完整** | BuildAsync 主流程 11 步全部实现 |
| .FieldResolution.cs | 974 | **完整** | 字段解析、词法评分、语义匹配全部实现 |
| .TableSelection.cs | 677 | **完整** | 表评分逻辑清晰,有 TableCandidateScore 结构 |
| .Search.cs | 275 | **完整** | 业务词收集、变体生成、Metadata搜索 |
| .PlanAssembly.cs | 256 | **完整** | AddOrUpdateQueryField、JOIN 构建 |
| .Helpers.cs | 260 | **完整** | NormalizeOperator、IsAggregation 等 |
| .SemanticResolution.cs | 179 | **完整** | Phase 2.6 语义解析路径,MasterJoin/DirectKey 严格验证 |
| .Diagnostics.cs | 177 | **完整** | 诊断信息构建 |
| BIConversationService.cs | 581 | **完整** | 9步编排: 理解→构建→验证→修复→置信度→决策→解释→SQL→执行 |
| SqlQueryBuilder.cs | 424 | **完整** | SELECT/FROM/JOIN/WHERE/GROUP BY/ORDER BY/LIMIT 全部实现 |
| QueryPlanValidationPipeline.cs | 1007 | **完整** | 修复循环: Fingerprint+Stall检测+循环检测+进度检测 |
| QueryPlanConfidenceService.cs | 1402 | **完整** | 置信度评估,多维度Evidence |
| QueryPlanRepairService.cs | 1705 | **完整** | AI修复,候选排序,安全阈值 |
| QueryPlanValidator.cs | 858 | **完整** | 语义验证 |
| QueryPlanDecisionGate.cs | 611 | **完整** | Proceed/Confirm/Reject 决策 |
| QueryUnderstandingService.cs | 658 | **完整** | LLM理解+Normalizer |
| QuerySemanticValidator.cs | 320 | **完整** | 语义校验 |
| QueryJoinInferenceService.cs | 512 | **完整** | 动态JOIN推理 |
| DimensionResolutionEvidenceService.cs | 301 | **完整** | D14核心: Master/DirectKey/Ambiguous 解析 |
| QueryPlanExplainabilityService.cs | 149 | **完整** | 解释聚合 |
| ResultUnderstandingService.cs | 207 | **完整** | 结果理解 |
| QueryPlanContextBuilder.cs | 135 | **完整** | 验证上下文构建 |
| QueryPlanMetadataValidator.cs | 345 | **完整** | Metadata关系验证 |

### 4.2 Services/BI/Evaluation (评估框架)

| 文件 | 行数 | 完成度 | 说明 |
|---|---|---|---|
| QueryPlanEvaluator.cs | 176 | **完整** | Golden评估引擎 |
| GoldenDatasetRunner.cs | 305 | **完整** | 测试运行器 |
| QueryPlanJoinScoringService.cs | 127 | **完整** | JOIN评分 |
| QueryPlanMetricScoringService.cs | 108 | **完整** | Metric评分 |
| SemanticApplicabilityEvaluator.cs | 186 | **完整** | 语义适用性评估 |
| QueryPlanEvaluationConfidenceService.cs | 230 | **完整** | 评估置信度 |
| QueryPlanDimensionScoringService.cs | 92 | **完整** | 维度评分 |
| QueryPlanFilterScoringService.cs | 70 | **完整** | 过滤评分 |
| QueryPlanQueryShapeScoringService.cs | 111 | **完整** | 查询形态评分 |
| GoldenDatasetCoverageAnalyzer.cs | 135 | **完整** | 覆盖率分析 |
| GoldenDatasetCoverageReportService.cs | 108 | **完整** | 覆盖率报告 |
| GoldenDatasetQualityGate.cs | 90 | **完整** | 质量门 |
| GoldenDatasetRegressionEvaluator.cs | 100 | **完整** | 回归评估 |
| GoldenConfidenceCalibrationEvaluator.cs | 106 | **完整** | 置信度校准 |
| GoldenConfidenceCalibrationRunner.cs | 33 | **完整** | 校准运行器 |
| GoldenBaselineRegistry.cs | 55 | **完整** | 基线注册 |
| GoldenBaselineReleaseService.cs | 67 | **完整** | 基线发布 |
| GoldenBaselinePersistenceService.cs | 63 | **完整** | 基线持久化 |
| GoldenBaselineLifecycleValidator.cs | 86 | **完整** | 生命周期验证 |
| GoldenBaselineComparisonService.cs | 69 | **完整** | 基线比较 |
| GoldenBaselineRegressionService.cs | 65 | **完整** | 基线回归 |
| GoldenDatasetRuntimeService.cs | 70 | **完整** | 运行时服务 |
| GoldenCaseDraftGenerator.cs | 55 | **完整** | 用例草稿生成 |
| GoldenScenarioGapGenerator.cs | 58 | **完整** | 场景差距生成 |
| GoldenEvaluationRegressionService.cs | 54 | **完整** | 评估回归 |
| GoldenQueryDatasetSerializer.cs | 43 | **完整** | 数据集序列化 |
| QueryPlanEvaluationGate.cs | 55 | **完整** | 评估门 |
| QueryPlanEvaluationScoringService.cs | 77 | **完整** | 评估评分 |
| QueryPlanSemanticResolutionFactory.cs | 58 | **完整** | 语义解析工厂 |
| QueryPlanEvaluationConfidenceEvidenceAdapter.cs | 91 | **完整** | 置信度证据适配器 |
| GoldenBaselinePersistenceMapper.cs | 43 | **完整** | 持久化映射 |
| InMemoryGoldenBaselinePersistence.cs | 33 | **完整** | 内存持久化实现 |

### 4.3 Services 其他 (支撑服务)

| 文件 | 行数 | 完成度 | 说明 |
|---|---|---|---|
| MetadataSemanticService.cs | 639 | **完整** | Metadata语义管理 |
| MetadataScannerService.cs | 597 | **完整** | Metadata扫描 |
| MetadataSemanticSearchService.cs | 500 | **完整** | 语义搜索 |
| QwenService.cs | 252 | **完整** | Qwen LLM调用 |
| QdrantService.cs | 326 | **完整** | 向量数据库 |
| MetadataVectorService.cs | 297 | **完整** | 向量服务 |
| MetadataVectorIndexService.cs | 227 | **完整** | 向量索引 |
| MetadataPromptBuilder.cs | 322 | **完整** | Prompt构建 |
| MetadataCsvFixtureService.cs | 199 | **完整** | CSV Fixture |
| QueryExecutionService.cs | 151 | **完整** | SQL执行 |
| DataSourceConnectionFactory.cs | 129 | **完整** | 数据源连接 |
| QwenEmbeddingService.cs | 142 | **完整** | Embedding服务 |
| MetadataSearchService.cs | 140 | **完整** | Metadata搜索 |
| MetadataSearchTextBuilder.cs | 105 | **完整** | 搜索文本构建 |
| FakeEmbeddingService.cs | 111 | **完整** | CI用假Embedding |
| MySqlMetadataReader.cs | 66 | **完整** | MySQL Metadata读取 |
| **QueryIntentNormalizer.cs (AI)** | **210** | **死代码** | 未注册,未引用,应删除 |

### 4.4 Controllers (15个)

| 文件 | 行数 | 完成度 | 说明 |
|---|---|---|---|
| LocalRuntimeDiagnosticsController.cs | 518 | **完整** | 本地运行时诊断 |
| GoldenDatasetRuntimeController.cs | 332 | **完整** | Golden数据集运行 |
| AITestController.cs | 173 | **完整** | AI测试 |
| LocalC133TestController.cs | 77 | **完整** | C.13.3测试 |
| EvaluationDiagnosticsController.cs | 235 | **完整** | 评估诊断 |
| QueryPlanConfidenceDiagnosticsController.cs | 101 | **完整** | 置信度诊断 |
| SemanticApplicabilityDiagnosticsController.cs | 85 | **完整** | 语义适用性诊断 |
| MetadataVectorController.cs | 122 | **完整** | Metadata向量 |
| QdrantController.cs | 73 | **完整** | Qdrant管理 |
| MetadataController.cs | 51 | **完整** | Metadata管理 |
| MetadataFixtureController.cs | 39 | **完整** | Metadata Fixture |
| GoldenBaselineDiagnosticsController.cs | 39 | **完整** | 基线诊断 |
| GoldenBaselineLifecycleDiagnosticsController.cs | 71 | **完整** | 基线生命周期诊断 |
| HomeController.cs | 25 | **完整** | 首页 |

### 4.5 Infrastructure + Data + Configuration

| 文件 | 行数 | 完成度 | 说明 |
|---|---|---|---|
| SuperBIContext.cs | 536 | **完整** | EF Core DbContext |
| SqlServerDialect.cs | 70 | **完整** | SQL Server方言 |
| MySqlDialect.cs | 57 | **完整** | MySQL方言 |
| PostgreSqlDialect.cs | 57 | **完整** | PostgreSQL方言 |
| SqlDialectResolver.cs | 55 | **完整** | 方言解析器 |
| ISqlDialect.cs | 88 | **完整** | 方言接口 |
| QdrantOptions.cs | 36 | **完整** | Qdrant配置 |
| EmbeddingOptions.cs | 50 | **完整** | Embedding配置 |

---

## 五、D14 Golden Regression 失败用例根因分析

### Golden Dataset 概况

18 个测试用例:
- Positive: GQ-001 ~ GQ-011 (11个)
- Negative: GQ-N001 ~ GQ-N005 (5个)
- Ambiguous: GQ-A001 (1个)
- Unresolved: GQ-U001 (1个)

### 通过状态 (基于代码分析推断)

| Case ID | 名称 | 推断状态 | 根因 |
|---|---|---|---|
| GQ-001 | Single Metric SUM | **PASS** | 基础聚合,链路完整 |
| GQ-002 | Single Metric COUNT | **FAIL** | EntityCount Ambiguous: "入库单数量" 被误识别为 SUM 而非 COUNT |
| GQ-003 | Metric By Dimension | **PASS/FAIL** | 取决于 "物料" 维度的 DimensionResolution 是否成功 |
| GQ-004 | Metric With Filter | **PASS** | 年份过滤由 NormalizeYearFilters 正确处理 |
| GQ-005 | Metric With Join | **FAIL** | 无 Master 时应走 DirectKey 而非 BLOCK |
| GQ-006 | Ranking Metric | **FAIL** | QueryPlan Evaluation 对 Ranking Order Contract 比对失败 |
| GQ-007 | Multiple Metrics | **FAIL** | 多 Metric 需逐项形成稳定 Resolution |
| GQ-008 | Distinct Entity Count | **FAIL** | EntityCount 候选触发 Ambiguous |
| GQ-009 | Join With Date Filter | **FAIL** | 同 GQ-005 + 日期过滤 |
| GQ-010 | Ranking With Filter | **FAIL** | 同 GQ-006 + 日期过滤 |
| GQ-011 | Detail Ranking | **PASS/FAIL** | 取决于 Detail Ranking Order Contract |
| GQ-N001~N005 | Negative | **PASS** | 安全检测全部通过 |
| GQ-A001 | Ambiguous | **PASS** | Ambiguous 正确阻断 |
| GQ-U001 | Unresolved | **PASS** | NotResolved 正确阻断 |

### 4 个根因簇

**R1: Multi-DB / DirectKey (GQ-005, GQ-009)**
- `DimensionResolutionEvidenceService` 在无 Master Evidence 时,如果事实表没有稳定 Association ID,会返回 `NotResolved` 而非降级为 `DirectKey`
- 但 D14 设计要求: 无 Master 但有稳定 Fact Key 时应走 DirectKey
- 代码 L195-229 已实现此逻辑,但 `DirectKeyThreshold = 0.60` 可能过高,导致 `bestKeyScore` 不达标

**R2: Ranking Evaluation (GQ-006, GQ-010)**
- `QueryPlanEvaluationScoringService` 和 `QueryPlanQueryShapeScoringService` 对 Ranking Order Contract 的比对比对逻辑可能未正确处理 `IsMetric` + `Aggregation` 组合
- `SqlQueryBuilder.BuildOrderBy` 中 L303 `field.AggregationFallback(aggregation)` 方法名令人困惑,可能未正确生成聚合排序表达式

**R3: EntityCount Ambiguous (GQ-002, GQ-008)**
- "入库单数量" 和 "不同供应商数量" 在 `QueryIntentNormalizer.InferMetricAggregation` 中应识别为 COUNT
- 但 `NormalizeExplicitMultiMetricSemantics` 和 `NormalizeQuantityAggregationSemantics` 的逻辑可能误将 "单数量" 匹配为 SUM
- L141-148 的 `InferMetricAggregation` 检查 "单数量"/"单据数量"/"订单数量"/"供应商数量" → COUNT,但仅在 `NormalizeMetricOnlyAggregate` 中调用
- **关键**: `NormalizeMetricOnlyAggregate` 在 L83 检查 `IntentType == "Ranking"` 时直接 return,可能跳过 COUNT 推断

**R4: Multi-Metric Resolution (GQ-007)**
- `NormalizeExplicitMultiMetricSemantics` 硬编码了 "入库数量" 和 "入库金额" 的 field/semanticType
- `NormalizeMetricFields` L129-131 同样硬编码了 quantity/amount 字段映射
- 这些硬编码可能在特定 LLM 输出下不触发,导致多 Metric Resolution 不稳定

---

## 六、完整工作计划

### 阶段 A: 修复 DI 注册缺失 (优先级: 阻塞)

| 序号 | 任务 | 文件 | 预估工作量 |
|---|---|---|---|
| A1 | 注册 BIConversationService + IBIConversationService | Program.cs | 2行 |
| A2 | 补充 IQueryPlanExplainabilityService 接口注册 | Program.cs | 1行 |
| A3 | 补充 ISqlDialectResolver 接口注册 | Program.cs | 1行 |
| A4 | 注册 IResultUnderstandingService + ResultUnderstandingService | Program.cs | 2行 |
| A5 | 注册 QueryPlanMetadataValidator | Program.cs | 1行 |
| A6 | 补充 IMetadataPromptBuilder 接口注册 | Program.cs | 1行 |
| A7 | 注册 IMetadataSearchService + MetadataSearchService (如需要) | Program.cs | 2行 |
| A8 | 删除死代码 QueryIntentNormalizer (AI版本) | Services/AI/QueryUnderstanding/ | 删除文件 |

### 阶段 B: 修复 D14 Golden Regression (优先级: 高)

| 序号 | 任务 | 根因簇 | 预估工作量 |
|---|---|---|---|
| B1 | 调整 DirectKeyThreshold 或 ScoreFactCandidate 评分逻辑,使无 Master 但有稳定 Fact Key 时正确走 DirectKey | R1 (GQ-005,009) | 中 |
| B2 | 修复 Ranking Order Contract 在 Evaluation 中的比对逻辑,确保 IsMetric+Aggregation 正确匹配 | R2 (GQ-006,010) | 中 |
| B3 | 修复 EntityCount 推断: 确保 "单数量"/"供应商数量" 正确识别为 COUNT,不被 Ranking 检查跳过 | R3 (GQ-002,008) | 中 |
| B4 | 修复 Multi-Metric Resolution: 去除硬编码 "入库数量"/"入库金额" field 映射,改为基于 SemanticType 的通用推断 | R4 (GQ-007) | 中 |
| B5 | 运行 Golden Dataset 回归测试验证 | - | 需运行时环境 |

### 阶段 C: 修复代码质量问题 (优先级: 中)

| 序号 | 任务 | 文件 | 预估工作量 |
|---|---|---|---|
| C1 | 替换所有空 catch 块为带日志的 catch | 9+处 | 小 |
| C2 | 替换 Console.WriteLine 为 ILogger | QueryPlanBuilder.TableSelection.cs | 小 |
| C3 | 统一 NormalizeOperator 行为(选择默认 "=" 或抛异常,保持一致) | 2处 | 小 |
| C4 | 消除 SqlQueryBuilder 双路径回退,统一为只用 plan.Dimensions/Orders | SqlQueryBuilder.cs | 中 |
| C5 | 修复 DimensionResolutionEvidenceService 重复搜索 | 1处 | 小 |
| C6 | 修复 LooksLikeIdentifier 过于宽泛(增加长度检查或更严格后缀匹配) | 1处 | 小 |
| C7 | 修复 ResolveMetricField "id" 后缀匹配(增加边界检查) | 1处 | 小 |

### 阶段 D: 架构改进 (优先级: 低,可后续迭代)

| 序号 | 任务 | 说明 |
|---|---|---|
| D1 | 消除硬编码时间字段名,改从 DataType 动态识别 | QueryPlanBuilder.cs L551-558 |
| D2 | 将语义搜索阈值(0.8/0.30)配置化 | QueryPlanBuilder.FieldResolution.cs |
| D3 | 将 JOIN 数量上限(2)配置化 | QueryPlanBuilder.PlanAssembly.cs L208 |
| D4 | 优化 CalculateTextOverlap 性能(考虑使用后缀数组或 Trie) | QueryPlanBuilder.FieldResolution.cs |
| D5 | QueryPlanBuilder 不再直接修改输入 intent(创建副本) | QueryPlanBuilder.cs |
| D6 | 将停用词列表配置化 | QueryPlanBuilder.Search.cs L120 |
| D7 | 将每词搜索结果数(10)配置化 | QueryPlanBuilder.Search.cs L224 |
| D8 | 为 BIConversationService 添加 try-catch 错误处理(LLM/搜索/执行失败) | BIConversationService.cs |
| D9 | 为 BIConversationService 添加多租户支持(tenantId 实际使用) | BIConversationService.cs |

### 阶段 E: 后续 Phase 开发 (优先级: 按开发计划)

| 序号 | 任务 | Phase |
|---|---|---|
| E1 | D14 DimensionAware QueryPlan — 完成 Golden Regression 达到 90%+ | Phase 2.7 D14 |
| E2 | Phase 2 剩余子阶段(如有) | Phase 2 |
| E3 | 业务语义层建设 | Phase 3 |
| E4 | 企业知识图谱 | Phase 4 |
| E5 | AI Native BI Agent | Phase 5 |
| E6 | Low-Code 平台融合 | Phase 6 |

---

## 七、执行顺序建议

```
阶段 A (DI修复,阻塞) → 阶段 B (D14回归修复) → Build验证 → Golden回归测试
                                                                    ↓
                                                        阶段 C (代码质量) → 阶段 D (架构改进) → 阶段 E (后续Phase)
```

**第一优先级**: 立即执行阶段 A + B,目标是让 D14 Golden Regression 达到 90%+ Pass Rate。  
**第二优先级**: 阶段 C,在 D14 Freeze 后进行代码质量修复。  
**第三优先级**: 阶段 D,作为 Phase 2 收尾或 Phase 3 准备阶段的架构改进。

---

*审计完成。以上结论均基于逐行阅读源码得出,非从开发计划文档复述。*
