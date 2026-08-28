# Domain 层（SuperBuilder_AI.Domain）

企业核心与业务规则所在层。**不依赖任何其它层**（零外设依赖）。

包含：
- `SharedKernel/` — `BaseEntity`、`FlexibleStringListConverter`、通用枚举（如 `QueryAggregation`、`DimensionResolutionType`）、跨上下文值对象。
- `Organization/` — 限界上下文：租户。`Tenant`(AR) → `DataSource`(AR)。
- `Metadata/` — 限界上下文：元数据。`MetadataTable`(AR) → `MetadataColumn` → `MetadataSemantic`；`MetadataLearningRecord`。
- `BiQuery/` — 限界上下文：查询计划。`QueryPlan`(AR) 与其值对象 `QueryIntent/QueryTable/QueryField/QueryMetric/QueryDimension/QueryFilter/QueryOrder/QueryJoin`，以及只读模型 `QueryPlanSemanticResolution`。
- `BusinessEntity/` — 限界上下文：业务实体。`BusinessEntity`(AR) + `Keys/Attributes/Metrics/Relationships`；映射聚合 `PhysicalBinding`。
- `Golden/` — 限界上下文：评测。`GoldenQueryDataset`(AR)、`GoldenBaseline`(AR)、`SemanticApplicabilityResult`、`QueryPlanEvaluationResult` 及其分数字典。

领域服务（纯业务规则，无 IO）：`QueryPlanDecisionGate`、`QuerySemanticValidator`、`QueryPlanMetadataValidator`、`QueryJoinInferenceService`（启发式）、`QueryIntentNormalizer`、各 `*ScoringService`、各 `*Evaluator`(纯)、`GoldenDatasetQualityGate` 等。

> 完整目录树与文件映射见 `docs/ARCHITECTURE.md`。
