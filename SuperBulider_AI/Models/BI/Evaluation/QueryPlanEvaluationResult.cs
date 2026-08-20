namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.4-D.2 QueryPlan Binding Consistency Evaluation 结果。
/// Golden Contract 与 Runtime QueryPlan 解耦，只记录评估结论与证据。
/// </summary>
public sealed class QueryPlanEvaluationResult
{
    public string CaseId { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public QueryPlanEvaluationSectionResult Intent { get; init; } = new();
    public QueryPlanEvaluationSectionResult Metrics { get; init; } = new();
    public QueryPlanEvaluationSectionResult Dimensions { get; init; } = new();
    public QueryPlanEvaluationSectionResult Filters { get; init; } = new();
    public QueryPlanEvaluationSectionResult Tables { get; init; } = new();
    public QueryPlanEvaluationSectionResult Joins { get; init; } = new();
    public QueryPlanEvaluationSectionResult QueryShape { get; init; } = new();
    public QueryPlanEvaluationSectionResult BindingConsistency { get; init; } = new();
}

public sealed class QueryPlanEvaluationSectionResult
{
    public bool Passed { get; init; }
    public string Reason { get; init; } = string.Empty;
}
