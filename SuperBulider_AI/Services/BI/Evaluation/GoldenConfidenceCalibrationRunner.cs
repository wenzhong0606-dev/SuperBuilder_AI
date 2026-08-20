using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.6.3.2
/// 将 Golden Dataset 中已构建的 Evaluation-aware Confidence 结果批量汇总到 Calibration。
/// 本 Runner 不改变 Confidence 算法，仅负责批量编排。
/// </summary>
public sealed class GoldenConfidenceCalibrationRunner
{
    private readonly GoldenConfidenceCalibrationEvaluator _evaluator;

    public GoldenConfidenceCalibrationRunner(GoldenConfidenceCalibrationEvaluator evaluator)
    {
        _evaluator = evaluator;
    }

    public GoldenConfidenceCalibrationScorecard Run(
        IEnumerable<QueryPlanEvaluationConfidenceResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return _evaluator.Evaluate(results);
    }

    public GoldenConfidenceCalibrationScorecard RunSingle(
        QueryPlanEvaluationConfidenceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return _evaluator.Evaluate(new[] { result });
    }
}
