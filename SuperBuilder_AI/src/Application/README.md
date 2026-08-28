# Application 层（SuperBuilder_AI.Application）

用例编排与应用服务层。**仅依赖 Domain 层**（及 Domain 中定义的端口接口）。

包含：
- `Ports/` — 所有向外依赖的抽象接口（`I*Repository`、`IMetadataSemanticSearchService`、`IEmbeddingService`、`IQdrantService`、`IQwenService`、`IDataSourceConnectionFactory`、`IQueryExecutionService` 等）。当前散落在 `Interfaces/` 的全部契约在此归集。
- `Organization/`、`Metadata/`、`BiQuery/`、`BusinessEntity/`、`Golden/` — 各上下文的应用服务与用例：
  - `BiQuery/`：`QueryUnderstandingService`、`QueryPlanBuilder`（瘦编排器）、`QueryPlanRepairService`、`QueryPlanConfidenceService`、`QueryPlanValidationPipeline`、`BIConversationService`→`QueryPlanPipeline`、`ResultUnderstandingService`、`MetadataContextBuilder`、`QueryPlanContextBuilder`、`QueryPlanExplainabilityService`、`MetadataPromptBuilder`、`MetadataSearchTextBuilder`。
  - `Metadata/`：`MetadataScannerService`、`MetadataSemanticService`、`MetadataSemanticSearchService`、`MetadataSearchService`、`MetadataVectorService`、`MetadataVectorIndexService`、`MetadataCsvFixtureService`（测试夹具，建议移入 tests）。
  - `Golden/`：`GoldenDatasetRunner`、`GoldenDatasetRuntimeService`、`GoldenBaseline*Service`、`GoldenConfidenceCalibration*`、`GoldenScenarioGapGenerator`、`GoldenCaseDraftGenerator`、`GoldenQueryDatasetSerializer`。
- `Common/` — 映射器、管道、共享编排助手。

> 完整目录树与文件映射见 `docs/ARCHITECTURE.md`。
