using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.6.2 Evaluation-aware Confidence 统一结果。
/// 保留现有 Phase 2.4 Confidence / Decision Contract，
/// 仅把 Phase 2.6 Evaluation Evidence 汇总到同一诊断结果。
/// </summary>
public sealed class QueryPlanEvaluationConfidenceResult
{
    public string CaseId { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public QueryPlanEvaluationResult Evaluation { get; init; } = new();
    public QueryPlanSemanticEvidenceResult? SemanticEvidence { get; init; }
    public QueryPlanConfidenceEvidence ConfidenceEvidence { get; init; } = new();
    public QueryPlanConfidence Confidence { get; init; } = new();
    public QueryPlanDecision Decision { get; init; } = new();
    public bool Passed { get; init; }
}
