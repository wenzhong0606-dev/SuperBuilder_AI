using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBulider_AI.Tests;

/// <summary>
/// Deterministic C.11.4 fixture for exercising the real evaluation/confidence composition
/// without requiring Qwen, Qdrant or a live database.
/// </summary>
internal sealed class GoldenEvaluationIntegrationFixture
{
    public QueryPlanEvaluationConfidenceService CreateService(
        QueryPlanEvaluator evaluator,
        QueryPlanEvaluationConfidenceEvidenceAdapter evidenceAdapter,
        IQueryPlanConfidenceService confidenceService,
        IQueryPlanDecisionGate decisionGate)
        => new(evaluator, evidenceAdapter, confidenceService, decisionGate);

    public static QueryPlan BuildPlan(string semanticText, QueryAggregation aggregation = QueryAggregation.Sum)
    {
        return new QueryPlan
        {
            IsAggregate = true,
            Metrics =
            {
                new QueryMetric
                {
                    SemanticText = semanticText,
                    Aggregation = aggregation
                }
            }
        };
    }

    public static QueryPlanValidationPipelineResult Valid(QueryPlan plan)
        => new()
        {
            Plan = plan,
            IsValid = true
        };
}
