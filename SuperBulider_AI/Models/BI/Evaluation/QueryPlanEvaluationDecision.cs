namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6.3.5-C.3 QueryPlan Evaluation Gate 决策结果。
/// 仅根据前置 Evaluation Evidence 决定是否允许继续进入 QueryPlan Evaluation。
/// </summary>
public sealed class QueryPlanEvaluationGateDecision
{
    public string CaseId { get; init; } = string.Empty;

    public string Decision { get; init; } = string.Empty;

    public bool Blocking { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string ApplicabilityState { get; init; } = string.Empty;
}
