using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.10.1 converts existing evaluator sections into a weighted scorecard.
/// This is intentionally separate from QueryPlanEvaluator so the existing boolean gate remains backward compatible.
/// </summary>
public sealed class QueryPlanEvaluationScoringService
{
    private static readonly IReadOnlyDictionary<string, double> DefaultWeights =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Intent"] = 0.10,
            ["Metrics"] = 0.25,
            ["Dimensions"] = 0.15,
            ["Filters"] = 0.10,
            ["Tables"] = 0.15,
            ["Joins"] = 0.10,
            ["QueryShape"] = 0.10,
            ["BindingConsistency"] = 0.05
        };

    public QueryPlanEvaluationResult Score(QueryPlanEvaluationResult evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        var sections = new[]
        {
            (Name: "Intent", Result: evaluation.Intent),
            (Name: "Metrics", Result: evaluation.Metrics),
            (Name: "Dimensions", Result: evaluation.Dimensions),
            (Name: "Filters", Result: evaluation.Filters),
            (Name: "Tables", Result: evaluation.Tables),
            (Name: "Joins", Result: evaluation.Joins),
            (Name: "QueryShape", Result: evaluation.QueryShape),
            (Name: "BindingConsistency", Result: evaluation.BindingConsistency)
        };

        var scores = sections.Select(x => new QueryPlanEvaluationDimensionScore
        {
            Dimension = x.Name,
            Weight = DefaultWeights[x.Name],
            Score = x.Result.Passed ? 1d : 0d,
            Passed = x.Result.Passed,
            Reason = x.Result.Reason
        }).ToList();

        var overall = scores.Sum(x => x.Weight * x.Score);
        var decision = overall >= 0.90
            ? QueryPlanEvaluationDecision.Pass
            : overall >= 0.60
                ? QueryPlanEvaluationDecision.Partial
                : QueryPlanEvaluationDecision.Fail;

        return new QueryPlanEvaluationResult
        {
            CaseId = evaluation.CaseId,
            Passed = decision == QueryPlanEvaluationDecision.Pass,
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
            BindingConsistency = evaluation.BindingConsistency
        };
    }
}
