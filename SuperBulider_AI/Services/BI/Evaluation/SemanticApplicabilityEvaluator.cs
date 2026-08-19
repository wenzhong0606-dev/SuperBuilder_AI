using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Models.AI;
using SuperBulider_AI.Models.BI;
using SuperBulider_AI.Models.BI.Evaluation;

namespace SuperBulider_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6.3.5-C.2 Semantic Applicability Evaluator。
///
/// 职责：
/// 1. 根据 Golden Metric 判断 Metric Type。
/// 2. 分析当前运行时 Metadata Semantic Search 候选。
/// 3. 输出结构化 Applicability Evidence。
///
/// 不负责 Metadata 修复、QueryPlan 修改、SQL Builder 或最终 Confidence 评分。
/// </summary>
public sealed class SemanticApplicabilityEvaluator
{
    private readonly IMetadataSemanticSearchService _semanticSearchService;

    public SemanticApplicabilityEvaluator(
        IMetadataSemanticSearchService semanticSearchService)
    {
        _semanticSearchService = semanticSearchService;
    }

    public async Task<SemanticApplicabilityResult> EvaluateAsync(
        GoldenQueryCase goldenCase,
        int topK = 10)
    {
        ArgumentNullException.ThrowIfNull(goldenCase);

        var metric = goldenCase.Expected?.Metrics?.SingleOrDefault();

        if (metric is null)
        {
            return new SemanticApplicabilityResult
            {
                CaseId = goldenCase.Id,
                Question = goldenCase.Question,
                State = "NotApplicable",
                MetricType = "Unknown",
                Reason = "Golden Case does not define a Metric."
            };
        }

        var metricType = metric.Aggregation == QueryAggregation.Count
            ? "EntityCount"
            : "ColumnMetric";

        var results = await _semanticSearchService
            .SearchAsync(goldenCase.Question, topK);

        if (results.Count == 0)
        {
            return new SemanticApplicabilityResult
            {
                CaseId = goldenCase.Id,
                Question = goldenCase.Question,
                MetricSemanticText = metric.SemanticText,
                MetricType = metricType,
                State = "NotResolved",
                Reason = "Semantic Search returned no candidates."
            };
        }

        var semanticCandidates = results
            .Where(x => x.IsSemanticVector)
            .OrderByDescending(x => x.Score)
            .ToList();

        var entityCandidates = results
            .Where(x => x.Table is not null)
            .Select(x => x.Table?.TableName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var topSemantic = semanticCandidates.FirstOrDefault();
        var secondSemantic = semanticCandidates.Skip(1).FirstOrDefault();

        var scoreGap = topSemantic is not null && secondSemantic is not null
            ? topSemantic.Score - secondSemantic.Score
            : null;

        return metricType == "EntityCount"
            ? EvaluateEntityCount(
                goldenCase,
                metric.SemanticText,
                semanticCandidates,
                entityCandidates,
                topSemantic,
                secondSemantic,
                scoreGap)
            : EvaluateColumnMetric(
                goldenCase,
                metric.SemanticText,
                semanticCandidates,
                entityCandidates,
                topSemantic,
                secondSemantic,
                scoreGap);
    }

    private static SemanticApplicabilityResult EvaluateColumnMetric(
        GoldenQueryCase goldenCase,
        string semanticText,
        IReadOnlyList<MetadataSemanticSearchResult> semanticCandidates,
        IReadOnlyList<string?> entityCandidates,
        MetadataSemanticSearchResult? topSemantic,
        MetadataSemanticSearchResult? secondSemantic,
        double? scoreGap)
    {
        if (topSemantic is null)
        {
            return new SemanticApplicabilityResult
            {
                CaseId = goldenCase.Id,
                Question = goldenCase.Question,
                MetricSemanticText = semanticText,
                MetricType = "ColumnMetric",
                State = "NotResolved",
                Reason = "No semantic vector candidate was returned."
            };
        }

        var lexicalMatch = ContainsSemanticText(topSemantic, semanticText);

        // C.2 第一版仅记录竞争证据，不把 ScoreGap 固化为最终业务阈值。
        var competingCandidates = secondSemantic is not null;

        var state = lexicalMatch && !competingCandidates
            ? "Resolved"
            : competingCandidates
                ? "Ambiguous"
                : "NotResolved";

        return new SemanticApplicabilityResult
        {
            CaseId = goldenCase.Id,
            Question = goldenCase.Question,
            MetricSemanticText = semanticText,
            MetricType = "ColumnMetric",
            State = state,
            Reason = BuildColumnReason(state, lexicalMatch),
            Candidate = ToCandidate(topSemantic),
            Evidence = new SemanticApplicabilityEvidence
            {
                SemanticCandidateExists = semanticCandidates.Count > 0,
                EntityCandidateExists = entityCandidates.Count > 0,
                DirectEntityCountEvidence = false,
                LexicalMatch = lexicalMatch,
                CompetingCandidates = competingCandidates,
                TopScore = topSemantic.Score,
                SecondScore = secondSemantic?.Score,
                ScoreGap = scoreGap
            }
        };
    }

    private static SemanticApplicabilityResult EvaluateEntityCount(
        GoldenQueryCase goldenCase,
        string semanticText,
        IReadOnlyList<MetadataSemanticSearchResult> semanticCandidates,
        IReadOnlyList<string?> entityCandidates,
        MetadataSemanticSearchResult? topSemantic,
        MetadataSemanticSearchResult? secondSemantic,
        double? scoreGap)
    {
        // 当前 MetadataSemanticSearchResult 只有 table / column / semantic 三类向量。
        // 即使发现实体表，也不能仅凭表存在认定 COUNT(entity) 已被语义解析。
        const bool directEntityCountEvidence = false;

        return new SemanticApplicabilityResult
        {
            CaseId = goldenCase.Id,
            Question = goldenCase.Question,
            MetricSemanticText = semanticText,
            MetricType = "EntityCount",
            State = directEntityCountEvidence ? "Resolved" : "NotResolved",
            Reason = directEntityCountEvidence
                ? "Direct EntityCount semantic evidence was found."
                : "Current Semantic Metadata contains entity/table candidates, " +
                  "but no direct EntityCount semantic evidence.",
            Candidate = topSemantic is null ? null : ToCandidate(topSemantic),
            Evidence = new SemanticApplicabilityEvidence
            {
                SemanticCandidateExists = semanticCandidates.Count > 0,
                EntityCandidateExists = entityCandidates.Count > 0,
                DirectEntityCountEvidence = directEntityCountEvidence,
                LexicalMatch = false,
                CompetingCandidates = secondSemantic is not null,
                TopScore = topSemantic?.Score,
                SecondScore = secondSemantic?.Score,
                ScoreGap = scoreGap
            }
        };
    }

    private static bool ContainsSemanticText(
        MetadataSemanticSearchResult candidate,
        string semanticText)
    {
        if (string.IsNullOrWhiteSpace(semanticText))
        {
            return false;
        }

        var values = new[]
        {
            candidate.Semantic?.BusinessMeaning,
            candidate.Semantic?.Keywords,
            candidate.Semantic?.Synonyms,
            candidate.Semantic?.ExampleQuestions
        };

        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Contains(semanticText, StringComparison.OrdinalIgnoreCase));
    }

    private static SemanticApplicabilityCandidate ToCandidate(
        MetadataSemanticSearchResult candidate)
    {
        return new SemanticApplicabilityCandidate
        {
            VectorType = candidate.VectorType,
            VectorId = candidate.VectorId,
            Score = candidate.Score,
            Table = candidate.Table?.TableName,
            Column = candidate.Column?.ColumnName,
            BusinessMeaning = candidate.Semantic?.BusinessMeaning
        };
    }

    private static string BuildColumnReason(
        string state,
        bool lexicalMatch)
    {
        return state switch
        {
            "Resolved" =>
                $"Top semantic candidate matches the Golden metric semantics. LexicalMatch={lexicalMatch}.",
            "Ambiguous" =>
                "Multiple semantic candidates were returned; C.2 does not use a Score threshold to select one.",
            _ =>
                "Top semantic candidate does not provide sufficient evidence for the Golden metric."
        };
    }
}
