using SuperBulider_AI.Models.BI.Evaluation;

namespace SuperBulider_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6.3.5-C.3 QueryPlan Evaluation Gate。
/// 不重新执行 Semantic Search，不修改 Metadata、QueryPlan 或 SQL。
/// 仅消费 C.2 SemanticApplicabilityResult，并做 PASS / BLOCK / REVIEW 决策。
/// </summary>
public sealed class QueryPlanEvaluationGate
{
    public QueryPlanEvaluationDecision Evaluate(SemanticApplicabilityResult applicability)
    {
        ArgumentNullException.ThrowIfNull(applicability);

        return applicability.State switch
        {
            "Resolved" => new QueryPlanEvaluationDecision
            {
                CaseId = applicability.CaseId,
                Decision = "PASS",
                Blocking = false,
                Reason = "Semantic applicability is resolved; QueryPlan Evaluation may continue.",
                ApplicabilityState = applicability.State
            },

            "NotResolved" => new QueryPlanEvaluationDecision
            {
                CaseId = applicability.CaseId,
                Decision = "BLOCK",
                Blocking = true,
                Reason = applicability.Reason ?? "Semantic applicability is not resolved.",
                ApplicabilityState = applicability.State
            },

            "Ambiguous" => new QueryPlanEvaluationDecision
            {
                CaseId = applicability.CaseId,
                Decision = "REVIEW",
                Blocking = true,
                Reason = applicability.Reason ?? "Semantic applicability is ambiguous and requires review.",
                ApplicabilityState = applicability.State
            },

            _ => new QueryPlanEvaluationDecision
            {
                CaseId = applicability.CaseId,
                Decision = "BLOCK",
                Blocking = true,
                Reason = $"Unsupported semantic applicability state: {applicability.State}.",
                ApplicabilityState = applicability.State
            }
        };
    }
}
