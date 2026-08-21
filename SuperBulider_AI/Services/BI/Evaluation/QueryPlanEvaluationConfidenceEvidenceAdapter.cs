using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.6.1 / C.13.2
/// 将 QueryPlan Evaluation 与 Semantic Resolution Evidence 转换为既有
/// QueryPlanConfidenceEvidence。适配器不重新计算 Confidence，也不执行 Decision。
/// </summary>
public sealed class QueryPlanEvaluationConfidenceEvidenceAdapter
{
    public QueryPlanConfidenceEvidence Adapt(
        QueryPlanEvaluationResult evaluation,
        QueryPlanSemanticEvidenceResult? semanticEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        var evidence = new QueryPlanConfidenceEvidence
        {
            TableEvidenceAvailable = true,
            FieldEvidenceAvailable = true,
            MetricEvidenceAvailable = true,
            DimensionEvidenceAvailable = true,
            FilterEvidenceAvailable = true,
            TableMatchScore = ToScore(evaluation.Tables.Passed),
            FieldMatchScore = ToScore(evaluation.BindingConsistency.Passed),
            MetricMatchScore = ToScore(evaluation.Metrics.Passed),
            DimensionMatchScore = ToScore(evaluation.Dimensions.Passed),
            FilterMatchScore = ToScore(evaluation.Filters.Passed)
        };

        if (semanticEvidence is not null)
        {
            evidence.SemanticEvidenceAvailable = true;

            var semanticItems = semanticEvidence.Metrics
                .Concat(semanticEvidence.Dimensions)
                .Concat(semanticEvidence.Filters)
                .Concat(semanticEvidence.Tables)
                .ToList();

            evidence.SemanticMatchScore = semanticItems.Count == 0
                ? ToScore(semanticEvidence.Passed)
                : semanticItems.Average(x => x.Passed ? 1d : 0d);

            if (semanticEvidence.Metrics.Count > 0)
            {
                evidence.MetricEvidenceAvailable = true;
                evidence.MetricMatchScore = semanticEvidence.Metrics.Average(x => x.Passed ? 1d : 0d);
            }

            if (semanticEvidence.Dimensions.Count > 0)
            {
                evidence.DimensionEvidenceAvailable = true;
                evidence.DimensionMatchScore = semanticEvidence.Dimensions.Average(x => x.Passed ? 1d : 0d);
            }

            if (semanticEvidence.Filters.Count > 0)
            {
                evidence.FilterEvidenceAvailable = true;
                evidence.FilterMatchScore = semanticEvidence.Filters.Average(x => x.Passed ? 1d : 0d);
            }

            if (semanticEvidence.Tables.Count > 0)
            {
                evidence.TableEvidenceAvailable = true;
                evidence.TableMatchScore = semanticEvidence.Tables.Average(x => x.Passed ? 1d : 0d);
            }

            var scores = semanticItems
                .Where(x => x.ResolutionScore.HasValue)
                .Select(x => Clamp(x.ResolutionScore!.Value))
                .ToList();

            if (scores.Count > 0)
                evidence.CandidateRankingScore = scores.Average();
        }

        if (evaluation.BindingConsistency.Passed)
        {
            evidence.FieldMatchScore = Math.Max(evidence.FieldMatchScore, 1d);
        }

        return evidence;
    }

    private static double ToScore(bool passed) => passed ? 1d : 0d;

    private static double Clamp(double value) => Math.Clamp(value, 0d, 1d);
}
