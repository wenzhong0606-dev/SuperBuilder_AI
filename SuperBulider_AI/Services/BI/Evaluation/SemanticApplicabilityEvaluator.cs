using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6.3.5-C.2 Semantic Applicability Evaluator。
/// </summary>
public sealed class SemanticApplicabilityEvaluator
{
    private readonly IMetadataSemanticSearchService _semanticSearchService;

    public SemanticApplicabilityEvaluator(IMetadataSemanticSearchService semanticSearchService)
    {
        _semanticSearchService = semanticSearchService;
    }

    public async Task<SemanticApplicabilityResult> EvaluateAsync(GoldenQueryCase goldenCase, int topK = 10)
    {
        ArgumentNullException.ThrowIfNull(goldenCase);

        var metrics = goldenCase.Expected?.Metrics ?? new List<GoldenMetricExpectation>();
        if (metrics.Count == 0)
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

        var metric = metrics[0];
        var metricType = metric.Aggregation == QueryAggregation.Count ? "EntityCount" : "ColumnMetric";
        var results = await _semanticSearchService.SearchAsync(goldenCase.Question, topK);

        if (results.Count == 0)
        {
            return new SemanticApplicabilityResult
            {
                CaseId = goldenCase.Id,
                Question = goldenCase.Question,
                MetricSemanticText = metric.SemanticText,
                MetricType = metricType,
                State = "NotResolved",
                Reason = metrics.Count > 1
                    ? $"Semantic Search returned no candidates for the primary metric; Golden Case contains {metrics.Count} metrics."
                    : "Semantic Search returned no candidates."
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
        double? scoreGap = topSemantic is not null && secondSemantic is not null
            ? topSemantic.Score - secondSemantic.Score
            : null;

        return metricType == "EntityCount"
            ? EvaluateEntityCount(goldenCase, metric.SemanticText, metrics.Count, semanticCandidates, entityCandidates, topSemantic, secondSemantic, scoreGap)
            : EvaluateColumnMetric(goldenCase, metric.SemanticText, metrics.Count, semanticCandidates, entityCandidates, topSemantic, secondSemantic, scoreGap);
    }

    private static SemanticApplicabilityResult EvaluateColumnMetric(
        GoldenQueryCase goldenCase,
        string semanticText,
        int metricCount,
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

        var topLexicalMatch = ContainsSemanticText(topSemantic, semanticText);
        var competingCandidates = semanticCandidates
            .Skip(1)
            .Any(x => ContainsSemanticText(x, semanticText));

        var state = topLexicalMatch
            ? competingCandidates ? "Ambiguous" : "Resolved"
            : "NotResolved";

        return new SemanticApplicabilityResult
        {
            CaseId = goldenCase.Id,
            Question = goldenCase.Question,
            MetricSemanticText = semanticText,
            MetricType = "ColumnMetric",
            State = state,
            Reason = BuildColumnReason(state, topLexicalMatch, metricCount),
            SearchCandidate = ToCandidate(topSemantic),
            Resolution = state == "Resolved" ? ToResolution(topSemantic) : null,
            Evidence = new SemanticApplicabilityEvidence
            {
                SemanticCandidateExists = semanticCandidates.Count > 0,
                EntityCandidateExists = entityCandidates.Count > 0,
                DirectEntityCountEvidence = false,
                LexicalMatch = topLexicalMatch,
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
        int metricCount,
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
                MetricType = "EntityCount",
                State = "NotResolved",
                Reason = "No semantic vector candidate was returned.",
                Evidence = new SemanticApplicabilityEvidence
                {
                    SemanticCandidateExists = false,
                    EntityCandidateExists = entityCandidates.Count > 0,
                    DirectEntityCountEvidence = false
                }
            };
        }

        var directEvidence = ContainsSemanticText(topSemantic, semanticText)
            && topSemantic.Table is not null
            && topSemantic.Column is not null;

        var competingCandidates = semanticCandidates
            .Skip(1)
            .Any(x => ContainsSemanticText(x, semanticText));

        var state = directEvidence
            ? competingCandidates ? "Ambiguous" : "Resolved"
            : "NotResolved";

        return new SemanticApplicabilityResult
        {
            CaseId = goldenCase.Id,
            Question = goldenCase.Question,
            MetricSemanticText = semanticText,
            MetricType = "EntityCount",
            State = state,
            Reason = BuildEntityCountReason(state, metricCount),
            SearchCandidate = ToCandidate(topSemantic),
            Resolution = state == "Resolved" ? ToResolution(topSemantic) : null,
            Evidence = new SemanticApplicabilityEvidence
            {
                SemanticCandidateExists = semanticCandidates.Count > 0,
                EntityCandidateExists = entityCandidates.Count > 0,
                DirectEntityCountEvidence = directEvidence,
                LexicalMatch = ContainsSemanticText(topSemantic, semanticText),
                CompetingCandidates = competingCandidates,
                TopScore = topSemantic.Score,
                SecondScore = secondSemantic?.Score,
                ScoreGap = scoreGap
            }
        };
    }

    private static bool ContainsSemanticText(MetadataSemanticSearchResult candidate, string semanticText)
    {
        if (string.IsNullOrWhiteSpace(semanticText))
            return false;

        var values = new[]
        {
            candidate.Semantic?.BusinessMeaning,
            candidate.Semantic?.Keywords,
            candidate.Semantic?.Synonyms,
            candidate.Semantic?.ExampleQuestions,
            candidate.Semantic?.SearchText,
            candidate.Column?.ColumnName,
            candidate.Table?.TableName
        };

        if (values.Any(value =>
                !string.IsNullOrWhiteSpace(value) &&
                value.Contains(semanticText, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Qdrant 已经完成语义召回；Applicability 不应再要求候选文本逐字等于 Golden metric。
        // 对中文业务短语使用字符集合重叠作为补充证据，解决“入库单数量”与“入库数量”这类
        // 业务同义/量词差异，同时保持至少两个有效字符的最低证据门槛。
        var metricChars = NormalizeSemanticCharacters(semanticText);
        if (metricChars.Count < 2)
            return false;

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeSemanticCharacters)
            .Any(candidateChars =>
                candidateChars.Count >= 2 &&
                metricChars.Intersect(candidateChars).Count() >= Math.Max(2, (int)Math.Ceiling(metricChars.Count * 0.5)));
    }

    private static HashSet<char> NormalizeSemanticCharacters(string text)
    {
        var ignored = new HashSet<char> { ' ', '\t', '\r', '\n', ',', '，', '。', '、', '的', '了', '请', '查', '询' };
        return text
            .Where(c => !ignored.Contains(c))
            .ToHashSet();
    }

    private static SemanticApplicabilityCandidate ToCandidate(MetadataSemanticSearchResult candidate)
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

    private static SemanticApplicabilityResolution? ToResolution(MetadataSemanticSearchResult candidate)
    {
        var table = candidate.Table?.TableName;
        var column = candidate.Column?.ColumnName;

        if (candidate.Table is null || candidate.Column is null
            || string.IsNullOrWhiteSpace(table)
            || string.IsNullOrWhiteSpace(column))
            return null;

        return new SemanticApplicabilityResolution
        {
            TableId = candidate.Table.Id,
            DataSourceId = candidate.Table.DataSourceId,
            ColumnId = candidate.Column.Id,
            Table = table,
            Column = column,
            BusinessMeaning = candidate.Semantic?.BusinessMeaning,
            Score = candidate.Score
        };
    }

    private static string BuildColumnReason(string state, bool lexicalMatch, int metricCount)
    {
        var metricNote = metricCount > 1
            ? $" Golden Case contains {metricCount} metrics; the first metric is used as Applicability primary evidence, while all metrics remain subject to QueryPlan Evaluation."
            : string.Empty;

        return state switch
        {
            "Resolved" => $"Top semantic candidate matches the Golden metric semantics. LexicalMatch={lexicalMatch}.{metricNote}",
            "Ambiguous" => $"Multiple semantic candidates contain direct evidence for the Golden metric; C.2 does not use a Score threshold to select one.{metricNote}",
            _ => $"Top semantic candidate does not provide sufficient evidence for the Golden metric.{metricNote}"
        };
    }

    private static string BuildEntityCountReason(string state, int metricCount)
    {
        var metricNote = metricCount > 1
            ? $" Golden Case contains {metricCount} metrics; the first metric is used as Applicability primary evidence."
            : string.Empty;

        return state switch
        {
            "Resolved" => $"Direct EntityCount semantic evidence matched an existing table/column binding.{metricNote}",
            "Ambiguous" => $"Multiple semantic candidates contain direct EntityCount evidence; Applicability remains Ambiguous.{metricNote}",
            _ => $"No direct EntityCount semantic evidence could be resolved from the current semantic candidates.{metricNote}"
        };
    }
}
