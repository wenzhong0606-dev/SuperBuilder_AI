> **治理声明**：本文档属于长期参考资料，不负责维护当前任务状态。当前状态与优先级按 `docs/README.md` 的治理顺序判定：源码/测试 → Master → Milestone → Active Plan → Backlog。与当前实现冲突时必须以当前证据更新 Master/Backlog，而不是按本文历史描述重复开发。
>
> **文档角色**：架构参考（非计划，供 MDP 参考）

# SuperBuilder AI Native BI — 目标架构（Clean Architecture + DDD）

> 本文定义 SuperBuilder_AI 项目按 **Clean Architecture + 领域驱动设计（DDD）** 重构后的目标文件结构与依赖规则。
> 配套主文档：`docs/Master_Development_Plan.md`（战略总账）与 `docs/Development_Backlog.md`（未完成任务总账）。
> 历史计划文档已归档于 `docs/plans/archive/`。

---

## 1. 设计原则

1. **依赖指向内核**：`Api → Application → Domain`；`Infrastructure → Application + Domain`。Domain 不依赖任何外层。
2. **依赖倒置（DIP）**：所有跨层/外部依赖通过端口（接口）声明在 Application 或 Domain，由 Infrastructure 实现。禁止在 Application/Api 直接 `new` 或依赖具体基础设施类。
3. **限界上下文（Bounded Context）隔离**：Organization / Metadata / BI Query / Business Entity / Golden-Evaluation 各自独立，跨上下文仅通过明确契约（DTO/只读模型）交互，消除循环依赖。
4. **聚合根（Aggregate Root）内聚**：每个聚合根拥有其子实体/值对象，外部只能通过根访问；仓储以聚合根为粒度。
5. **上帝类拆分**：超大服务按职责拆为领域服务/应用服务/编排器。
6. **测试与诊断代码非生产**：诊断/夹具控制器与 CSV 夹具服务不进入生产依赖图。

---

## 2. 目标解决方案与目录树

```
SuperBuilder_AI.slnx
├─ src/
│  ├─ SuperBuilder_AI.Domain/                 # 零外设依赖
│  │  ├─ SharedKernel/                        # BaseEntity, 枚举, JsonConverter, 通用 VO
│  │  ├─ Organization/                        # Tenant(AR) → DataSource(AR)
│  │  ├─ Metadata/                            # MetadataTable(AR) → Column → Semantic; LearningRecord
│  │  ├─ BiQuery/                             # QueryPlan(AR) + 值对象; QueryPlanSemanticResolution(只读)
│  │  ├─ BusinessEntity/                      # BusinessEntity(AR) + 子实体; PhysicalBinding(映射AR)
│  │  └─ Golden/                              # GoldenQueryDataset(AR), GoldenBaseline(AR), 评测结果
│  │
│  ├─ SuperBuilder_AI.Application/            # 依赖 Domain
│  │  ├─ Ports/                               # 所有对外抽象接口(IRepository/I*Service...)
│  │  ├─ Organization/  Metadata/  BiQuery/  BusinessEntity/  Golden/
│  │  └─ Common/                              # 映射器、管道、编排助手
│  │
│  ├─ SuperBuilder_AI.Infrastructure/         # 依赖 Application + Domain
│  │  ├─ Persistence/                         # SuperBIContext, Configurations, Migrations, 仓储实现
│  │  ├─ Vector/                              # Qdrant 适配器
│  │  ├─ Llm/                                 # Qwen / Embedding 适配器 (+Fake 桩)
│  │  └─ Database/                            # 多库读取器, 连接工厂, 执行器, SQL Dialect
│  │
│  └─ SuperBuilder_AI.Api/                    # 依赖 Application
│     ├─ Controllers/                         # 生产/管理控制器
│     ├─ Diagnostics/                         # 诊断/测试/夹具控制器(非生产)
│     ├─ Program.cs                           # 组合根
│     ├─ Views/  wwwroot/
│
├─ tests/
│  ├─ SuperBuilder_AI.UnitTests/
│  └─ SuperBuilder_AI.IntegrationTests/       # Golden 运行时回归(对应 18-case 契约)
│
├─ docs/
│  ├─ Master_Development_Plan.md
│  ├─ ARCHITECTURE.md
│  └─ plans/archive/                          # 历史计划文档
└─ SuperBuilder_AI/                           # 旧单体项目(重构期保留, 逐步废弃)
   └─ (现有 Configuration/Controllers/Data/... )
```

> 说明：完整多项目拆分（Phase 4.3）是目标终点；Phase 4.2 可在现有单项目内先以 `src/{Domain,Application,Infrastructure,Api}` 文件夹落地同样的分层与命名空间，零构建中断。

---

## 3. 限界上下文与聚合根

| 限界上下文 | 聚合根 | 主要子对象 / 值对象 | 当前位置（重构前） |
|---|---|---|---|
| Organization | `Tenant` → `DataSource` | — | `Models/Organization/Tenant.cs`, `Models/Metadata/DataSource.cs` |
| Metadata | `MetadataTable` → `MetadataColumn` → `MetadataSemantic` | `MetadataLearningRecord` | `Models/Metadata/*` |
| BI Query | `QueryPlan` | `QueryIntent/Table/Field/Metric/Dimension/Filter/Order/Join`（VO）、`QueryPlanSemanticResolution`（只读） | `Models/BI/*` |
| Business Entity | `BusinessEntity`（→Keys/Attributes/Metrics/Relationships）；`PhysicalBinding`（映射 AR） | — | `Models/BI/Entity/*` |
| Golden / Evaluation | `GoldenQueryDataset`（→Case→Expectation）；`GoldenBaseline` | `SemanticApplicabilityResult`, `QueryPlanEvaluationResult` 及各 Scorecard | `Models/BI/Evaluation/*` |
| Shared Kernel | — | `BaseEntity`, `QueryAggregation`, `DimensionResolutionType`, `FlexibleStringListConverter` | `Models/BaseEntity.cs`, `Models/BI/*`(枚举) |

---

## 4. 当前文件 → 目标位置 映射（分组）

### 4.1 Domain 层
| 当前文件 | 目标 |
|---|---|
| `Models/BaseEntity.cs`, `Models/BI/FlexibleStringListConverter.cs` | `Domain/SharedKernel/` |
| `Models/Organization/Tenant.cs` | `Domain/Organization/` |
| `Models/Metadata/{DataSource,MetadataTable,MetadataColumn,MetadataSemantic,MetadataLearningRecord,MetadataSearchResult}.cs` | `Domain/Metadata/` |
| `Models/BI/{QueryIntent,QueryPlan,QueryTable,QueryField,QueryMetric,QueryDimension,QueryFilter,QueryOrder,QueryJoin,QueryJoinCandidate,QueryAggregation,SqlQuery,QueryResult,QueryAnswer,VisualizationSuggestion,SemanticValidationError,QuerySemanticValidationResult,QueryPlanValidationContext,QueryPlanValidationIssue,QueryPlanValidationResult,QueryPlanValidationPipelineResult,QueryPlanRepairRequest,QueryPlanRepairResult,QueryPlanRepairTrace,QueryPlanConfidence,QueryPlanConfidenceEvidence,QueryPlanConfidenceLevel,QueryPlanDecision,QueryPlanDecisionTrace,QueryPlanDecisionType,QueryPlanExplanation,QueryRepairAction,QueryRepairResult,DimensionResolutionEvidence,DimensionResolutionType}.cs` | `Domain/BiQuery/` |
| `Models/BI/QueryPlanSemanticResolution.cs` | `Domain/BiQuery/`（只读模型） |
| `Models/BI/Entity/{BusinessEntity,BusinessEntityKey,BusinessEntityAttribute,BusinessEntityMetric,BusinessEntityRelationship,PhysicalBinding}.cs` | `Domain/BusinessEntity/` |
| `Models/BI/Evaluation/*`（Golden*, SemanticApplicabilityResult, QueryPlanEvaluation*） | `Domain/Golden/` |
| `Models/AI/*`（部分纯 VO 如 `MetadataSemanticSearchResult`,`VectorSearchResult` 仅承载数据，归属 Domain 对应上下文；含 LLM 业务语义的归入 Application DTO） | `Domain/{Metadata,BiQuery}/` 或 `Application/Metadata/` |

> 领域服务（纯规则、无 IO）也置于 Domain 对应上下文：`QueryPlanDecisionGate`、`QuerySemanticValidator`、`QueryPlanMetadataValidator`、`QueryJoinInferenceService`、`QueryIntentNormalizer`，以及 `Evaluation/` 下全部 `*ScoringService`、`*Evaluator`(纯)、`GoldenDatasetQualityGate`、`GoldenDatasetCoverageAnalyzer`、`GoldenScenarioGapGenerator`、`GoldenCaseDraftGenerator`、`GoldenConfidenceCalibrationEvaluator`、`QueryPlanSemanticResolutionFactory`、`QueryPlanEvaluationGate`。

### 4.2 Application 层（服务 + 端口）
| 当前文件 | 目标 |
|---|---|
| `Interfaces/**`（全部契约） | `Application/Ports/`（按 BC 分包） |
| `Services/BI/{QueryUnderstandingService,QueryPlanBuilder*.cs,QueryPlanRepairService,QueryPlanConfidenceService,QueryPlanValidationPipeline,QueryPlanContextBuilder,QueryPlanExplainabilityService,ResultUnderstandingService,MetadataContextBuilder,MetadataPromptBuilder,MetadataSearchTextBuilder}.cs` | `Application/BiQuery/` |
| `Services/BI/Entity/{EntityQueryPlanMapper,BusinessEntityService,PhysicalBindingResolver}.cs` | `Application/BusinessEntity/` |
| `Services/BI/Planning/Intent/QueryIntentNormalizer.cs` | `Domain/BiQuery/`（纯规则，见 4.1）或 `Application/BiQuery/` |
| `Services/{MetadataScannerService,MetadataSemanticService,MetadataSemanticSearchService,MetadataSearchService,MetadataVectorService,MetadataVectorIndexService,MetadataCsvFixtureService}.cs` | `Application/Metadata/` |
| `Services/BI/Evaluation/{GoldenDatasetRunner,GoldenDatasetRuntimeService,GoldenBaseline*Service,GoldenConfidenceCalibration*Service,GoldenScenarioGapGenerator,GoldenCaseDraftGenerator,GoldenQueryDatasetSerializer}.cs` | `Application/Golden/` |
| `Services/BI/Evaluation/{QueryPlanEvaluator,QueryPlanEvaluationConfidenceService,QueryPlanEvaluationConfidenceEvidenceAdapter,SemanticApplicabilityEvaluator}.cs` | `Application/Golden/`（编排+适配；纯规则部分见 4.1） |

### 4.3 Infrastructure 层
| 当前文件 | 目标 |
|---|---|
| `Data/SuperBIContext.cs`, `Data/Configurations/*`, `Migrations/*` | `Infrastructure/Persistence/` |
| `Services/QdrantService.cs` | `Infrastructure/Vector/` |
| `Services/{QwenService,QwenEmbeddingService,FakeEmbeddingService}.cs` | `Infrastructure/Llm/` |
| `Services/MySqlMetadataReader.cs` | `Infrastructure/Database/`（补 `SqlServer/PostgreSql` 读取器） |
| `Services/Database/{DataSourceConnectionFactory,QueryExecutionService}.cs` | `Infrastructure/Database/` |
| `Infrastructure/Database/*`（已正确放置） | 保持 `Infrastructure/Database/` |

### 4.4 Api 层
| 当前文件 | 目标 |
|---|---|
| `Program.cs` | `Api/Program.cs` |
| `Controllers/{Home,Metadata,MetadataVector,Qdrant}Controller.cs` | `Api/Controllers/` |
| `Controllers/{AITest,LocalC133Test,SemanticApplicabilityDiagnostics,BusinessEntityGoldenFixture,BusinessEntityRuntimeVerification,GoldenBaselineDiagnostics,GoldenBaselineLifecycleDiagnostics,GoldenDatasetRuntime,EvaluationDiagnostics,MetadataFixture,QueryPlanConfidenceDiagnostics,LocalRuntimeDiagnostics}Controller.cs` | `Api/Diagnostics/`（非生产，限制路由前缀） |
| `Views/`, `wwwroot/` | `Api/Views/`, `Api/wwwroot/` |
| `Configuration/{EmbeddingOptions,QdrantOptions}.cs` | `Infrastructure/`（选项对象）或 `Application/Common/Options/` |

---

## 5. 上帝类拆分（DDD 职责分解）

| 现状“上帝类” | 拆分方案 |
|---|---|
| `QueryPlanBuilder`（主 + 8 个 partial，约 7k 行） | `BusinessTermExtractor` + `TableSelector` + `FieldResolver` + `JoinBuilder`（领域服务） + 瘦编排器 `QueryPlanBuilder`（仅协调） |
| `BIConversationService`（端到端内联编排） | `QueryPlanPipeline`（应用编排器）委托各步骤处理器 |
| `QueryPlanValidator`（DB 访问 + 规则混合） | `IMetadataRepository` 端口 + 纯 `QueryPlanValidator`（领域服务） |
| `SemanticApplicabilityEvaluator`（26KB） | `ApplicabilityResolver` + `Metric/Filter/Dimension/TableResolver`（领域服务） |
| `GoldenDatasetRunner`（13KB，9 依赖） | 应用用例，委托 `SemanticApplicabilityEvaluator` + `QueryPlanEvaluator` + `QueryPlanEvaluationConfidenceService` |
| `QueryPlanEvaluator`（18KB，私有解析助手） | 既有分段 ScoringService 保留；抽取私有 `EvaluateMetrics/Dimensions/...` 为领域服务 |

---

## 6. 冗余与重复（待合并/删除，当前任务见 `docs/Development_Backlog.md`）

- `Models/BI/Evaluation/GoldenBaselinePersistenceRecord.cs` 与 `GoldenBaseline.cs` 字段重复 → 保留 `GoldenBaseline` + 持久化 DTO（`GoldenBaselinePersistenceRecord` 仅作存储投影，精简字段）。
- `Models/BI/QueryPlanSemanticResolution.cs` 与 `Models/BI/Evaluation/SemanticApplicabilityResult(+*Resolution)` 形状近重复 → 抽取共享 Kernel 的解析契约。
- `SemanticApplicabilityFilterResolution`（空子类）→ 并入 `MetricResolution`。
- `Models/BI/QueryRepairAction.cs` / `QueryRepairResult.cs`：遗留死代码（仅互相引用，无任何外部使用）→ **已删除（本次）**。
- 三套搜索结果 DTO（`MetadataSearchResult` / `MetadataSemanticSearchResult` / `VectorSearchResult`）→ 以 `MetadataSemanticSearchResult` 为规范，其余降级为内部投影。
- `DimensionResolutionType`/`DimensionExecutionCapability` 枚举 vs `QueryDimension`/`DimensionResolutionEvidence` 中字符串字段不一致 → 统一为枚举。
- `Evaluation/DebugOutputs/`（38 个调试 JSON/py/txt）：无代码引用 → **已删除（本次）**。

---

## 7. 依赖规则与防护

- Domain 项目 **不得** 引用 `Microsoft.EntityFrameworkCore`、`Qdrant.Client`、`System.Net.Http`、`Microsoft.Data.SqlClient` 等外层包。
- Application 项目 **不得** 引用 `Infrastructure` 项目；仅引用其自身 Ports 与 Domain。
- 外部依赖（EF/Qdrant/HTTP/DB）只在 `Infrastructure` 出现；组合根在 `Api/Program.cs`。
- 建议在 CI 增加 **架构依赖校验**（如 `NetArchTest` / `ArchUnitNET`）断言上述方向，防止回归。
