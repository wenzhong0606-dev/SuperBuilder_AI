using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.13.2：只聚合已经完成 Section Evaluation 的结果。
/// 不重新执行 Metric Evaluation，避免同一 QueryPlan 在不同层被重复计算。
/// </summary>
public sealed class QueryPlanEvaluationScoringService
{
    private static readonly IReadOnlyDictionary<string, double> DefaultWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
    {
        ["Intent"] = 0.10, ["Metrics"] = 0.25, ["Dimensions"] = 0.15, ["Filters"] = 0.10,
        ["Tables"] = 0.15, ["Joins"] = 0.10, ["QueryShape"] = 0.10, ["BindingConsistency"] = 0.05
    };

    public QueryPlanEvaluationResult Score(QueryPlanEvaluationResult evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        var sections = new[]
        {
            (Name: "Intent", Result: evaluation.Intent), (Name: "Metrics", Result: evaluation.Metrics),
            (Name: "Dimensions", Result: evaluation.Dimensions), (Name: "Filters", Result: evaluation.Filters),
            (Name: "Tables", Result: evaluation.Tables), (Name: "Joins", Result: evaluation.Joins),
            (Name: "QueryShape", Result: evaluation.QueryShape), (Name: "BindingConsistency", Result: evaluation.BindingConsistency)
        };
        var scores = sections.Select(x => new QueryPlanEvaluationDimensionScore { Dimension = x.Name, Weight = DefaultWeights[x.Name], Score = x.Result.Score, Passed = x.Result.Passed, Reason = x.Result.Reason }).ToList();
        var overall = scores.Sum(x => x.Weight * Math.Clamp(x.Score, 0d, 1d));
        var decision = overall >= 0.90 ? QueryPlanEvaluationOutcome.Pass : overall >= 0.60 ? QueryPlanEvaluationOutcome.Partial : QueryPlanEvaluationOutcome.Fail;
        return new QueryPlanEvaluationResult
        {
            CaseId = evaluation.CaseId, Passed = decision == QueryPlanEvaluationOutcome.Pass, Decision = decision, OverallScore = Math.Round(overall * 100d, 2), DimensionScores = scores,
            Intent = evaluation.Intent, Metrics = evaluation.Metrics, Dimensions = evaluation.Dimensions, Filters = evaluation.Filters, Tables = evaluation.Tables, Joins = evaluation.Joins,
            QueryShape = evaluation.QueryShape, BindingConsistency = evaluation.BindingConsistency, MetricExpectations = evaluation.MetricExpectations, ActualMetrics = evaluation.ActualMetrics
        };
    }
}
