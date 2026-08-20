namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.4.8 QueryPlan Semantic Resolution Evidence。
/// 将 Semantic Applicability 的解析结果与 Runtime QueryPlan 的物理绑定进行独立评价。
/// </summary>
public sealed class QueryPlanSemanticEvidenceResult
{
    public string CaseId { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string GoldenSemanticText { get; init; } = string.Empty;
    public string ApplicabilityState { get; init; } = string.Empty;
    public bool ResolutionExists { get; init; }
    public bool RuntimeMetricExists { get; init; }
    public bool MetricFieldMatchesResolution { get; init; }
    public bool TableBindingMatchesResolution { get; init; }
    public bool DataSourceBindingMatchesResolution { get; init; }
    public double? ResolutionScore { get; init; }
}
