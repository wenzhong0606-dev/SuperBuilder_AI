using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.13.2：只聚合已经完成 Section Evaluation 的结果。
/// 不重新执行 Metric Evaluation，避免同一 QueryPlan 在不同层被重复计算。
///
/// Evaluation Contract：
/// 1. Score 表示整体质量，不等价于通过。
/// 2. Section Passed 表示对应 Golden Assertion 是否满足。
/// 3. 任一 Section Assertion 失败时，整体不得为 Pass。
/// 4. 只有所有 Section Assertion 通过且整体分数达到 Pass 阈值时，整体才允许 Pass。
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

        var scores = sections.Select(x => new QueryPlanEvaluationDimensionScore
        {
            Dimension = x.Name,
            Weight = DefaultWeights[x.Name],
            Score = x.Result.Score,
            Passed = x.Result.Passed,
            Reason = x.Result.Reason
        }).ToList();

        var overall = scores.Sum(x => x.Weight * Math.Clamp(x.Score, 0d, 1d));
        var allSectionsPassed = scores.All(x => x.Passed);
        var decision = DetermineOutcome(overall, allSectionsPassed);

        return new QueryPlanEvaluationResult
        {
            CaseId = evaluation.CaseId,
            Passed = decision == QueryPlanEvaluationOutcome.Pass,
            Decision = decision,
            OverallScore = Math.Round(overall * 100d, 2),
            DimensionScores = scores,
            Intent = evaluation.Intent,
            Metrics = evaluation.Metrics,
            Dimensions = evaluation.Dimensions,
            Filters = evaluation.Filters,
            Tables = evaluation.Tables,
            Joins = evaluation.Joins,
            QueryShape = evaluation.QueryShape,
            BindingConsistency = evaluation.BindingConsistency,
            MetricExpectations = evaluation.MetricExpectations,
            ActualMetrics = evaluation.ActualMetrics
        };
    }

    private static QueryPlanEvaluationOutcome DetermineOutcome(double overall, bool allSectionsPassed)
    {
        if (allSectionsPassed && overall >= 0.90)
            return QueryPlanEvaluationOutcome.Pass;

        return overall >= 0.60
            ? QueryPlanEvaluationOutcome.Partial
            : QueryPlanEvaluationOutcome.Fail;
    }
}
