using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.6.1
/// 将 Phase 2.6 QueryPlan Evaluation Evidence 转换为 Phase 2.4
/// QueryPlanConfidenceEvidence。
///
/// 本适配器不重新计算 Confidence，也不执行 Decision Gate。
/// 对于 Phase 2.6 当前没有直接提供的证据维度，保持默认值，
/// 不伪造 CandidateRanking / Validation / Repair Evidence。
/// </summary>
public sealed class QueryPlanEvaluationConfidenceEvidenceAdapter
{
    public QueryPlanConfidenceEvidence Adapt(
        QueryPlanEvaluationResult evaluation,
        QueryPlanSemanticEvidenceResult? semanticEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        var evidence = new QueryPlanConfidenceEvidence();

        // ---------------------------------------------------------
        // Structural Evaluation Evidence
        // ---------------------------------------------------------
        // 当前 Evaluation Contract 是 bool + Reason，没有连续评分。
        // 因此这里采用明确的二值证据：PASS=1，FAIL=0。
        // 不将其伪装成原始 Semantic Search score。
        evidence.TableEvidenceAvailable = true;
        evidence.FieldEvidenceAvailable = true;
        evidence.MetricEvidenceAvailable = true;
        evidence.DimensionEvidenceAvailable = true;
        evidence.FilterEvidenceAvailable = true;

        evidence.TableMatchScore = ToScore(evaluation.Tables.Passed);
        evidence.FieldMatchScore = ToScore(evaluation.BindingConsistency.Passed);
        evidence.MetricMatchScore = ToScore(evaluation.Metrics.Passed);
        evidence.DimensionMatchScore = ToScore(evaluation.Dimensions.Passed);
        evidence.FilterMatchScore = ToScore(evaluation.Filters.Passed);

        // QueryPlanEvaluator 当前没有单独的 Validation Score。
        // Query Shape / Intent 属于结构评价证据，不映射到既有 ValidationScore，
        // 避免改变 Phase 2.4 Confidence 的既有权重语义。

        // ---------------------------------------------------------
        // Semantic Resolution Evidence
        // ---------------------------------------------------------
        if (semanticEvidence is not null)
        {
            evidence.SemanticEvidenceAvailable = true;
            evidence.SemanticMatchScore =
                semanticEvidence.ResolutionScore.HasValue
                    ? Clamp(semanticEvidence.ResolutionScore.Value)
                    : ToScore(semanticEvidence.Passed);

            // Metric/Field/Table 的 Semantic Resolution 与 Runtime Binding
            // 是 C.4.8 的直接证据；只有明确存在对应 Resolution 时才提升可用性。
            evidence.MetricEvidenceAvailable =
                semanticEvidence.RuntimeMetricExists;

            evidence.MetricMatchScore =
                semanticEvidence.MetricFieldMatchesResolution
                    ? 1d
                    : 0d;

            evidence.TableEvidenceAvailable =
                semanticEvidence.TableBindingMatchesResolution;

            evidence.TableMatchScore =
                semanticEvidence.TableBindingMatchesResolution
                    ? 1d
                    : 0d;

            if (semanticEvidence.ResolutionScore.HasValue)
            {
                evidence.CandidateRankingScore =
                    Clamp(semanticEvidence.ResolutionScore.Value);
            }
        }

        // ---------------------------------------------------------
        // Binding Consistency
        // ---------------------------------------------------------
        // BindingConsistency 是 Phase 2.6 的显式 Evaluation 证据。
        // 当前 ConfidenceEvidence 没有独立 Binding 字段，因此只能通过
        // Field Evidence 表达，而不改变 Confidence 权重结构。
        if (evaluation.BindingConsistency.Passed)
        {
            evidence.FieldMatchScore = Math.Max(
                evidence.FieldMatchScore,
                1d);
        }

        return evidence;
    }

    private static double ToScore(bool passed) => passed ? 1d : 0d;

    private static double Clamp(double value) =>
        Math.Clamp(value, 0d, 1d);
}
