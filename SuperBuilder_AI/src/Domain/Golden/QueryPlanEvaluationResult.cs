namespace SuperBuilder_AI.Models.BI.Evaluation;

public sealed class QueryPlanEvaluationResult
{
    public string CaseId { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public QueryPlanEvaluationOutcome Decision { get; init; } = QueryPlanEvaluationOutcome.Fail;
    public double OverallScore { get; init; }
    public IReadOnlyList<QueryPlanEvaluationDimensionScore> DimensionScores { get; init; } = Array.Empty<QueryPlanEvaluationDimensionScore>();
    public QueryPlanEvaluationSectionResult Intent { get; init; } = new();
    public QueryPlanEvaluationSectionResult Metrics { get; init; } = new();
    public QueryPlanEvaluationSectionResult Dimensions { get; init; } = new();
    public QueryPlanEvaluationSectionResult Filters { get; init; } = new();
    public QueryPlanEvaluationSectionResult Tables { get; init; } = new();
    public QueryPlanEvaluationSectionResult Joins { get; init; } = new();
    public QueryPlanEvaluationSectionResult QueryShape { get; init; } = new();
    public QueryPlanEvaluationSectionResult BindingConsistency { get; init; } = new();
    public IReadOnlyList<GoldenMetricExpectation>? MetricExpectations { get; init; }
    public IReadOnlyList<QueryMetric>? ActualMetrics { get; init; }
}

/// <summary>
/// QueryPlan Evaluation 本身的评分结果。
/// 与 QueryPlanEvaluationGateDecision（前置语义 Gate 决策）严格区分。
/// </summary>
public enum QueryPlanEvaluationOutcome
{
    Fail,
    Partial,
    Pass
}

public sealed class QueryPlanEvaluationDimensionScore
{
    public string Dimension { get; init; } = string.Empty;
    public double Weight { get; init; }
    public double Score { get; init; }
    public bool Passed { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class QueryPlanEvaluationSectionResult
{
    public bool Passed { get; init; }
    public double Score { get; init; }
    public string Reason { get; init; } = string.Empty;
    public object? Details { get; init; }
}
