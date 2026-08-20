using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

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

    private readonly QueryPlanMetricScoringService _metricScoringService;

    public QueryPlanEvaluationScoringService(QueryPlanMetricScoringService metricScoringService)
    {
        _metricScoringService = metricScoringService;
    }

    public QueryPlanEvaluationResult Score(QueryPlanEvaluationResult evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        var metricScore = evaluation.Metrics;
        if (evaluation.MetricExpectations is not null || evaluation.ActualMetrics is not null)
        {
            var detailed = _metricScoringService.Evaluate(evaluation.MetricExpectations, evaluation.ActualMetrics);
            metricScore = new QueryPlanEvaluationSectionResult
            {
                Passed = detailed.Passed,
                Score = detailed.Score,
                Reason = detailed.Reason,
                Details = detailed.Items
            };
        }

        var sections = new[]
        {
            (Name: "Intent", Result: evaluation.Intent),
            (Name: "Metrics", Result: metricScore),
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
            Score = Math.Clamp(x.Result.Score, 0d, 1d),
            Passed = x.Result.Passed,
            Reason = x.Result.Reason
        }).ToList();

        var overall = scores.Sum(x => x.Weight * x.Score);
        var decision = overall >= 0.90 ? QueryPlanEvaluationDecision.Pass
            : overall >= 0.60 ? QueryPlanEvaluationDecision.Partial
            : QueryPlanEvaluationDecision.Fail;

        return new QueryPlanEvaluationResult
        {
            CaseId = evaluation.CaseId,
            Passed = decision == QueryPlanEvaluationDecision.Pass,
            Decision = decision,
            OverallScore = Math.Round(overall * 100d, 2),
            DimensionScores = scores,
            Intent = evaluation.Intent,
            Metrics = metricScore,
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
}
