using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.6.2
/// 将 QueryPlan Evaluation、Semantic Evidence 与既有 Phase 2.4
/// Confidence / Decision Gate 串成统一诊断流程。
///
/// 重要边界：本服务不重算 Confidence，也不修改既有 Confidence 权重。
/// Evaluation Evidence 当前作为独立证据返回，供后续 C.6.3 Calibration 使用。
/// </summary>
public sealed class QueryPlanEvaluationConfidenceService
{
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

        // C.6.2 明确不把 Evaluation Evidence 伪装成原 Confidence Score 的输入。
        // Phase 2.4 ConfidenceService 当前拥有固定内部 Evidence/Weight 计算契约。
        // 本结果将两套证据并列保存，C.6.3 再进行 Golden Calibration。
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
}
