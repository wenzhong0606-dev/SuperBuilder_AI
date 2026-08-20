using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.6.2 / C.14
/// 将 QueryPlan Evaluation、Semantic Evidence 与既有 Phase 2.4
/// Confidence / Decision Gate 串成统一诊断流程。
///
/// C.14 Evaluation Decision Gate：
/// QueryPlan Evaluation 是 SQL Builder 之前的硬安全门。
/// 即使 Confidence Score 很高，只要 Evaluation 或 Semantic Evidence
/// 明确失败，就不得通过 Decision Gate 进入 SQL Builder。
/// </summary>
public sealed class QueryPlanEvaluationConfidenceService
{
    private const double EvaluationPassConfidenceBoost = 0.10;

    private readonly QueryPlanEvaluator _queryPlanEvaluator;
    private readonly QueryPlanEvaluationConfidenceEvidenceAdapter _evidenceAdapter;
    private readonly IQueryPlanConfidenceService _confidenceService;
    private readonly IQueryPlanDecisionGate _decisionGate;

    public QueryPlanEvaluationConfidenceService(
        QueryPlanEvaluator queryPlanEvaluator,
        QueryPlanEvaluationConfidenceEvidenceAdapter evidenceAdapter,
        IQueryPlanConfidenceService confidenceService,
        IQueryPlanDecisionGate decisionGate)
    {
        _queryPlanEvaluator = queryPlanEvaluator;
        _evidenceAdapter = evidenceAdapter;
        _confidenceService = confidenceService;
        _decisionGate = decisionGate;
    }

    public async Task<QueryPlanEvaluationConfidenceResult> EvaluateAsync(
        string caseId,
        string question,
        GoldenQueryExpectation expected,
        QueryPlan runtimePlan,
        QueryPlanValidationPipelineResult validationResult,
        QueryPlanRepairTrace? repairTrace,
        SemanticApplicabilityResult? applicability = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(runtimePlan);
        ArgumentNullException.ThrowIfNull(validationResult);

        cancellationToken.ThrowIfCancellationRequested();

        var evaluation = _queryPlanEvaluator.Evaluate(caseId, expected, runtimePlan);

        QueryPlanSemanticEvidenceResult? semanticEvidence = null;
        if (applicability is not null)
        {
            semanticEvidence = _queryPlanEvaluator.EvaluateSemanticEvidence(
                caseId,
                expected,
                runtimePlan,
                applicability);
        }

        var confidence = await _confidenceService.EvaluateAsync(
            runtimePlan,
            validationResult,
            repairTrace,
            question,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var evaluationEvidence = _evidenceAdapter.Adapt(
            evaluation,
            semanticEvidence);

        // =========================================================
        // Phase 2.6 C.13 — Evaluation-aware Confidence
        // =========================================================
        //
        // QueryPlan Confidence 原本只使用 Semantic / Binding /
        // Validation / Repair Evidence。对于 Golden Runtime，
        // QueryPlan Evaluation 是已经针对 Golden Expected 做过的
        // 结构化验证，因此在 Evaluation 完整通过时应成为正向证据。
        //
        // 这里采用固定、可解释的 +0.10 Boost，而不是直接绕过
        // Decision Gate：
        //
        // Base Confidence 0.72
        //     + Evaluation Pass 0.10
        //     = High Confidence 0.82
        //
        // Evaluation 失败绝不加分；后续 C.14 Hard Blocking 仍然
        // 会把 Evaluation Failure 注入 BlockingReasons。
        //
        // 只有 Evaluation、Semantic Evidence、Validation、Repair
        // 都处于安全状态时才允许获得该 Boost。
        if (CanUseEvaluationPassAsConfidenceEvidence(
                evaluation,
                semanticEvidence,
                validationResult))
        {
            confidence.Score = Math.Min(
                1.0,
                confidence.Score + EvaluationPassConfidenceBoost);

            confidence.Level = DetermineConfidenceLevel(confidence.Score);
            confidence.CanProceed =
                confidence.Score >= 0.80
                && confidence.Evidence.ValidationErrorCount == 0
                && !confidence.Evidence.RepairStalled
                && !confidence.Evidence.RepairLoopDetected
                && !confidence.Evidence.RepairFailed
                && !confidence.Evidence.MaxRepairAttemptsReached;

            confidence.Reasons.Add(
                $"QueryPlan Evaluation 通过，作为 Evaluation-aware Confidence 正向证据，Confidence Boost = {EvaluationPassConfidenceBoost:F2}。");
        }

        // =========================================================
        // Phase 2.6 C.14 — Evaluation Decision Gate
        // =========================================================
        //
        // Confidence 是“这个 QueryPlan 看起来有多可靠”；
        // Evaluation 是“这个 QueryPlan 是否满足 Golden 预期”。
        // 两者不能互相覆盖。
        //
        // 因此：
        //
        // Evaluation = FAIL
        //     ↓
        // Hard Blocking
        //     ↓
        // Decision Gate = Reject
        //
        // 即使 Confidence = High / Score >= 0.80，
        // 也绝对不能因为高分而 Proceed。
        //
        // Negative Golden Case 的“正确失败”由 GoldenDatasetRegressionEvaluator
        // 解释为 Expected Outcome 满足；这里仍然必须让 Runtime Decision = Reject，
        // 从而阻止错误 QueryPlan 进入 SQL Builder。
        if (!evaluation.Passed)
        {
            AddBlockingReason(
                confidence,
                "QueryPlan Evaluation 未通过，禁止进入 SQL Builder。");
        }

        if (semanticEvidence is not null && !semanticEvidence.Passed)
        {
            AddBlockingReason(
                confidence,
                "QueryPlan Semantic Evidence 未通过，禁止进入 SQL Builder。");
        }

        // C.6.2 原有 Decision Gate 继续负责 Confidence Level、Validation、Repair
        // 等安全规则；C.14 在调用 Gate 前把 Evaluation Failure 注入同一套
        // BlockingReasons，因此最终 Trace 仍然保持统一，不引入第二套 Decision 模型。
        var decision = _decisionGate.Evaluate(confidence);

        var passed = evaluation.Passed
                     && (semanticEvidence is null || semanticEvidence.Passed)
                     && decision.Decision == QueryPlanDecisionType.Proceed;

        return new QueryPlanEvaluationConfidenceResult
        {
            CaseId = caseId,
            Question = question ?? string.Empty,
            Evaluation = evaluation,
            SemanticEvidence = semanticEvidence,
            ConfidenceEvidence = evaluationEvidence,
            Confidence = confidence,
            Decision = decision,
            Passed = passed
        };
    }

    private static bool CanUseEvaluationPassAsConfidenceEvidence(
        QueryPlanEvaluationResult evaluation,
        QueryPlanSemanticEvidenceResult? semanticEvidence,
        QueryPlanValidationPipelineResult validationResult)
    {
        if (!evaluation.Passed)
            return false;

        if (semanticEvidence is not null && !semanticEvidence.Passed)
            return false;

        var validation = validationResult.ValidationResult;
        if (validation.ErrorItems.Any())
            return false;

        var trace = validationResult.RepairTrace;
        if (trace.Status is QueryPlanRepairTraceStatus.Stalled
            or QueryPlanRepairTraceStatus.LoopDetected
            or QueryPlanRepairTraceStatus.Failed
            or QueryPlanRepairTraceStatus.MaxAttemptsReached)
        {
            return false;
        }

        return true;
    }

    private static QueryPlanConfidenceLevel DetermineConfidenceLevel(double score)
    {
        if (score >= 0.80)
            return QueryPlanConfidenceLevel.High;

        if (score >= 0.60)
            return QueryPlanConfidenceLevel.Medium;

        return QueryPlanConfidenceLevel.Low;
    }

    private static void AddBlockingReason(
        QueryPlanConfidence confidence,
        string reason)
    {
        if (confidence.BlockingReasons.Any(x =>
                string.Equals(x, reason, StringComparison.Ordinal)))
        {
            return;
        }

        confidence.BlockingReasons.Add(reason);
    }
}
