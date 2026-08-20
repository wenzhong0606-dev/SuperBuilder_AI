using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.6.3
/// 根据 Golden Dataset 的 Evaluation-aware Confidence 结果，
/// 统计 Confidence Level 与 Evaluation / Decision 的一致性。
/// 不修改 Confidence 算法，只负责校准与诊断。
/// </summary>
public sealed class GoldenConfidenceCalibrationEvaluator
{
    public GoldenConfidenceCalibrationScorecard Evaluate(
        IEnumerable<QueryPlanEvaluationConfidenceResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var items = results.ToList();
        var cases = items.Select(ToCase).ToList();

        var evaluationPassed = cases.Count(x => x.EvaluationPassed);
        var proceed = cases.Count(x => IsDecision(x.Decision, QueryPlanDecisionType.Proceed));
        var confirm = cases.Count(x => IsDecision(x.Decision, QueryPlanDecisionType.Confirm));
        var reject = cases.Count(x => IsDecision(x.Decision, QueryPlanDecisionType.Reject));

        var high = cases.Where(x => IsLevel(x.ConfidenceLevel, "High")).ToList();
        var medium = cases.Where(x => IsLevel(x.ConfidenceLevel, "Medium")).ToList();
        var low = cases.Where(x => IsLevel(x.ConfidenceLevel, "Low")).ToList();

        var warnings = new List<string>();

        var highPass = Rate(high, x => x.EvaluationPassed);
        var mediumPass = Rate(medium, x => x.EvaluationPassed);
        var lowPass = Rate(low, x => x.EvaluationPassed);

        var highProceed = Rate(high, x => IsDecision(x.Decision, QueryPlanDecisionType.Proceed));
        var mediumConfirm = Rate(medium, x => IsDecision(x.Decision, QueryPlanDecisionType.Confirm));
        var lowReject = Rate(low, x => IsDecision(x.Decision, QueryPlanDecisionType.Reject));

        if (high.Any() && highPass < 0.90)
            warnings.Add($"High Confidence Evaluation Pass Rate is only {highPass:P1}.");

        if (low.Any() && lowPass > 0.50)
            warnings.Add($"Low Confidence contains {lowPass:P1} Evaluation PASS cases; Confidence may be too conservative.");

        var agreement = Rate(cases, x =>
            x.EvaluationPassed
                ? IsDecision(x.Decision, QueryPlanDecisionType.Proceed)
                : IsDecision(x.Decision, QueryPlanDecisionType.Confirm) || IsDecision(x.Decision, QueryPlanDecisionType.Reject));

        if (cases.Any() && agreement < 0.90)
            warnings.Add($"Evaluation/Decision agreement is only {agreement:P1}.");

        return new GoldenConfidenceCalibrationScorecard
        {
            Total = items.Count,
            Evaluated = cases.Count,
            EvaluationPassed = evaluationPassed,
            EvaluationFailed = cases.Count - evaluationPassed,
            DecisionProceed = proceed,
            DecisionConfirm = confirm,
            DecisionReject = reject,
            EvaluationDecisionAgreementRate = agreement,
            HighConfidencePassRate = highPass,
            MediumConfidencePassRate = mediumPass,
            LowConfidencePassRate = lowPass,
            HighConfidenceProceedRate = highProceed,
            MediumConfidenceConfirmRate = mediumConfirm,
            LowConfidenceRejectRate = lowReject,
            CalibrationWarnings = warnings
        };
    }

    private static GoldenConfidenceCalibrationCase ToCase(QueryPlanEvaluationConfidenceResult result)
    {
        var decision = result.Decision.Decision;
        var agreement = result.Evaluation.Passed
            ? decision == QueryPlanDecisionType.Proceed
            : decision == QueryPlanDecisionType.Confirm || decision == QueryPlanDecisionType.Reject;

        return new GoldenConfidenceCalibrationCase
        {
            CaseId = result.CaseId,
            ConfidenceLevel = result.Confidence.Level.ToString(),
            ConfidenceScore = result.Confidence.Score,
            EvaluationPassed = result.Evaluation.Passed,
            Decision = decision.ToString(),
            Agreement = agreement
        };
    }

    private static bool IsLevel(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    // GoldenConfidenceCalibrationCase 为诊断 DTO，Decision 按合同保存为 string。
    // 在校准阶段统一从 string 解析为 QueryPlanDecisionType，再进行强类型比较，
    // 避免 string 与 enum 直接传参导致 CS1503。
    private static bool IsDecision(string actual, QueryPlanDecisionType expected) =>
        Enum.TryParse<QueryPlanDecisionType>(actual, ignoreCase: true, out var parsed) && parsed == expected;

    private static double Rate<T>(IEnumerable<T> items, Func<T, bool> predicate)
    {
        var list = items.ToList();
        return list.Count == 0 ? 0d : (double)list.Count(predicate) / list.Count;
    }
}