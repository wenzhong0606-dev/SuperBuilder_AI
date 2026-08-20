namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.6.2 Evaluation-aware Confidence 结果。
/// 保留原有 Confidence 与 Decision Contract，同时记录 Evaluation Evidence。
/// </summary>
public sealed class QueryPlanEvaluationConfidenceResult
{
    public string CaseId { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public QueryPlanEvaluationResult Evaluation { get; init; } = new();
    public QueryPlanSemanticEvidenceResult? SemanticEvidence { get; init; }
    public QueryPlanConfidence Confidence { get; init; } = new();
    public QueryPlanDecisionGateResult Decision { get; init; } = new();
    public bool Passed { get; init; }
}
