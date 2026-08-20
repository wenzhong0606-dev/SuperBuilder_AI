using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6.3.5-C.2 Semantic Applicability Evaluator。
/// Semantic Applicability 不只负责 Metric，还负责在 Golden 明确要求时解析 Filter / Dimension 的稳定物理绑定。
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
            return new SemanticApplicabilityResult { CaseId = goldenCase.Id, Question = goldenCase.Question, State = "NotApplicable", MetricType = "Unknown", Reason = "Golden Case does not define a Metric." };

        var metric = metrics[0];
        var metricType = metric.Aggregation == QueryAggregation.Count ? "EntityCount" : "ColumnMetric";
        // Metric 解析必须以 Golden 声明的 SemanticText 为检索入口，避免“最多/前10个/物料”等 Query Modifier 污染 Metric 召回。
        var results = await _semanticSearchService.SearchAsync(metric.SemanticText, topK);
        if (results.Count == 0)
            return new SemanticApplicabilityResult { CaseId = goldenCase.Id, Question = goldenCase.Question, MetricSemanticText = metric.SemanticText, MetricType = metricType, State = "NotResolved", Reason = "Semantic Search returned no candidates." };

        var semanticCandidates = results.Where(x => x.IsSemanticVector).GroupBy(GetCandidateBindingKey, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(x => x.Score).First()).OrderByDescending(x => x.Score).ToList();
        var entityCandidates = results.Where(x => x.Table is not null).Select(x => x.Table?.TableName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var topSemantic = semanticCandidates.FirstOrDefault();
        var secondSemantic = semanticCandidates.Skip(1).FirstOrDefault();
        double? scoreGap = topSemantic is not null && secondSemantic is not null ? topSemantic.Score - secondSemantic.Score : null;

        var metricApplicability = metricType == "EntityCount"
            ? EvaluateEntityCount(goldenCase, metric.SemanticText, metrics.Count, semanticCandidates, entityCandidates, topSemantic, secondSemantic, scoreGap)
            : EvaluateColumnMetric(goldenCase, metric.SemanticText, metrics.Count, semanticCandidates, entityCandidates, topSemantic, secondSemantic, scoreGap);

        if (!string.Equals(metricApplicability.State, "Resolved", StringComparison.OrdinalIgnoreCase))
            return metricApplicability;

        var filterResolutions = new List<SemanticApplicabilityFilterResolution>();
        if (goldenCase.Expected?.Filters is not null)
            foreach (var filter in goldenCase.Expected.Filters)
            {
                var resolution = await ResolveFilterAsync(filter, topK);
                if (resolution is null) return CopyWithFailure(metricApplicability, $"Filter 语义“{filter.SemanticText}”无法解析为稳定的 Metadata 物理绑定。");
                filterResolutions.Add(resolution);
            }

        var dimensionResolutions = new List<SemanticApplicabilityDimensionResolution>();
        if (goldenCase.Expected?.Dimensions is not null)
            foreach (var dimension in goldenCase.Expected.Dimensions)
            {
                var resolution = await ResolveDimensionAsync(dimension, topK);
                if (resolution is null) return CopyWithFailure(metricApplicability, $"Dimension 语义“{dimension.SemanticText}”无法解析为稳定的 Metadata 物理绑定。");
                dimensionResolutions.Add(resolution);
            }

        return new SemanticApplicabilityResult
        {
            CaseId = metricApplicability.CaseId, Question = metricApplicability.Question, MetricSemanticText = metricApplicability.MetricSemanticText,
            MetricType = metricApplicability.MetricType, State = metricApplicability.State, Reason = metricApplicability.Reason,
            SearchCandidate = metricApplicability.SearchCandidate, Resolution = metricApplicability.Resolution,
            FilterResolutions = filterResolutions, DimensionResolutions = dimensionResolutions, Evidence = metricApplicability.Evidence
        };
    }

    private async Task<SemanticApplicabilityFilterResolution?> ResolveFilterAsync(GoldenFilterExpectation filter, int topK)
    {
        if (string.IsNullOrWhiteSpace(filter.SemanticText)) return null;
        var results = await _semanticSearchService.SearchAsync(filter.SemanticText, topK);
        var candidates = results.Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null).GroupBy(GetCandidateBindingKey, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(x => x.Score).First()).OrderByDescending(x => x.Score).ToList();
        var candidate = candidates.FirstOrDefault(x => ContainsSemanticText(x, filter.SemanticText));
        if (candidate?.Table is null || candidate.Column is null) return null;
        return new SemanticApplicabilityFilterResolution { TableId = candidate.Table.Id, DataSourceId = candidate.Table.DataSourceId, ColumnId = candidate.Column.Id, SemanticText = filter.SemanticText, Table = candidate.Table.TableName, Column = candidate.Column.ColumnName, BusinessMeaning = candidate.Semantic?.BusinessMeaning, Score = candidate.Score };
    }

    private async Task<SemanticApplicabilityDimensionResolution?> ResolveDimensionAsync(GoldenDimensionExpectation dimension, int topK)
    {
        if (string.IsNullOrWhiteSpace(dimension.SemanticText)) return null;
        var results = await _semanticSearchService.SearchAsync(dimension.SemanticText, topK);
        var candidates = results.Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null).GroupBy(GetCandidateBindingKey, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(x => x.Score).First()).OrderByDescending(x => x.Score).ToList();
        var candidate = candidates.FirstOrDefault(x => ContainsSemanticText(x, dimension.SemanticText));
        if (candidate?.Table is null || candidate.Column is null) return null;
        return new SemanticApplicabilityDimensionResolution { TableId = candidate.Table.Id, DataSourceId = candidate.Table.DataSourceId, ColumnId = candidate.Column.Id, SemanticText = dimension.SemanticText, Table = candidate.Table.TableName, Column = candidate.Column.ColumnName, BusinessMeaning = candidate.Semantic?.BusinessMeaning, Score = candidate.Score };
    }

    private static SemanticApplicabilityResult CopyWithFailure(SemanticApplicabilityResult source, string reason) => new()
    {
        CaseId = source.CaseId, Question = source.Question, MetricSemanticText = source.MetricSemanticText, MetricType = source.MetricType,
        State = "NotResolved", Reason = reason, SearchCandidate = source.SearchCandidate, Resolution = source.Resolution, Evidence = source.Evidence
    };

    private static SemanticApplicabilityResult EvaluateColumnMetric(GoldenQueryCase goldenCase, string semanticText, int metricCount, IReadOnlyList<MetadataSemanticSearchResult> semanticCandidates, IReadOnlyList<string?> entityCandidates, MetadataSemanticSearchResult? topSemantic, MetadataSemanticSearchResult? secondSemantic, double? scoreGap)
    {
        if (topSemantic is null) return new SemanticApplicabilityResult { CaseId = goldenCase.Id, Question = goldenCase.Question, MetricSemanticText = semanticText, MetricType = "ColumnMetric", State = "NotResolved", Reason = "No semantic vector candidate was returned." };
        var topLexicalMatch = ContainsSemanticText(topSemantic, semanticText);
        var competingCandidates = semanticCandidates.Skip(1).Any(x => ContainsSemanticText(x, semanticText));
        var state = topLexicalMatch ? competingCandidates ? "Ambiguous" : "Resolved" : "NotResolved";
        return new SemanticApplicabilityResult { CaseId = goldenCase.Id, Question = goldenCase.Question, MetricSemanticText = semanticText, MetricType = "ColumnMetric", State = state, Reason = BuildColumnReason(state, topLexicalMatch, metricCount), SearchCandidate = ToCandidate(topSemantic), Resolution = state == "Resolved" ? ToResolution(topSemantic) : null, Evidence = new SemanticApplicabilityEvidence { SemanticCandidateExists = semanticCandidates.Count > 0, EntityCandidateExists = entityCandidates.Count > 0, DirectEntityCountEvidence = false, LexicalMatch = topLexicalMatch, CompetingCandidates = competingCandidates, TopScore = topSemantic.Score, SecondScore = secondSemantic?.Score, ScoreGap = scoreGap } };
    }

    private static SemanticApplicabilityResult EvaluateEntityCount(GoldenQueryCase goldenCase, string semanticText, int metricCount, IReadOnlyList<MetadataSemanticSearchResult> semanticCandidates, IReadOnlyList<string?> entityCandidates, MetadataSemanticSearchResult? topSemantic, MetadataSemanticSearchResult? secondSemantic, double? scoreGap)
    {
        if (topSemantic is null)
            return new SemanticApplicabilityResult { CaseId = goldenCase.Id, Question = goldenCase.Question, MetricSemanticText = semanticText, MetricType = "EntityCount", State = "NotResolved", Reason = "No semantic vector candidate was returned.", Evidence = new SemanticApplicabilityEvidence { SemanticCandidateExists = false, EntityCandidateExists = entityCandidates.Count > 0, DirectEntityCountEvidence = false } };

        var topLexicalMatch = ContainsSemanticText(topSemantic, semanticText);
        var entitySemanticText = ExtractEntitySemanticText(semanticText);
        var tableEntityCandidates = string.IsNullOrWhiteSpace(entitySemanticText)
            ? new List<MetadataSemanticSearchResult>()
            : semanticCandidates.Where(x => x.Table is not null && ContainsDirectEntityEvidence(x, entitySemanticText)).GroupBy(x => x.Table!.Id).Select(g => g.OrderByDescending(x => GetEntityEvidenceScore(x, entitySemanticText)).First()).OrderByDescending(x => GetEntityEvidenceScore(x, entitySemanticText)).ToList();

        var exactEntityCandidates = tableEntityCandidates.Where(x => MatchesExactEntityTableSemanticText(x, entitySemanticText)).GroupBy(x => x.Table!.Id).Select(g => g.OrderByDescending(x => GetEntityEvidenceScore(x, entitySemanticText)).First()).OrderByDescending(x => GetEntityEvidenceScore(x, entitySemanticText)).ToList();
        var entityCandidatesForResolution = exactEntityCandidates.Count > 0 ? exactEntityCandidates : tableEntityCandidates;
        var entityCandidate = entityCandidatesForResolution.FirstOrDefault();
        var secondEntityCandidate = entityCandidatesForResolution.Skip(1).FirstOrDefault();
        var entityScore = entityCandidate is null ? 0 : GetEntityEvidenceScore(entityCandidate, entitySemanticText);
        var secondEntityScore = secondEntityCandidate is null ? 0 : GetEntityEvidenceScore(secondEntityCandidate, entitySemanticText);

        var competingCandidates = entityCandidate is not null && secondEntityCandidate is not null && secondEntityScore >= entityScore - 8;
        var directEvidence = entityCandidate is not null && entityScore >= 60;
        if (directEvidence) topSemantic = entityCandidate;
        var state = directEvidence ? competingCandidates ? "Ambiguous" : "Resolved" : "NotResolved";

        return new SemanticApplicabilityResult
        {
            CaseId = goldenCase.Id, Question = goldenCase.Question, MetricSemanticText = semanticText, MetricType = "EntityCount", State = state,
            Reason = BuildEntityCountReason(state, metricCount, topLexicalMatch, entitySemanticText), SearchCandidate = ToCandidate(topSemantic), Resolution = state == "Resolved" ? ToResolution(topSemantic) : null,
            Evidence = new SemanticApplicabilityEvidence { SemanticCandidateExists = semanticCandidates.Count > 0, EntityCandidateExists = entityCandidates.Count > 0, DirectEntityCountEvidence = directEvidence, LexicalMatch = topLexicalMatch || !string.IsNullOrWhiteSpace(entityCandidate?.Table?.TableComment), CompetingCandidates = competingCandidates, TopScore = entityCandidate?.Score ?? topSemantic.Score, SecondScore = secondEntityCandidate?.Score ?? secondSemantic?.Score, ScoreGap = secondEntityCandidate is not null ? entityScore - secondEntityScore : scoreGap }
        };
    }

    private static bool ContainsDirectEntityEvidence(MetadataSemanticSearchResult candidate, string entitySemanticText)
    {
        if (candidate.Table is null || string.IsNullOrWhiteSpace(entitySemanticText)) return false;
        var normalizedEntity = NormalizeSemanticText(entitySemanticText);
        if (normalizedEntity.Length == 0) return false;
        var values = new[] { candidate.Table.TableName, candidate.Table.TableComment, candidate.Semantic?.BusinessMeaning, candidate.Semantic?.Keywords, candidate.Semantic?.Synonyms, candidate.Semantic?.ExampleQuestions, candidate.Semantic?.SearchText, candidate.Column?.ColumnName, candidate.Column?.ColumnComment };
        return values.Any(value => !string.IsNullOrWhiteSpace(value) && NormalizeSemanticText(value).Contains(normalizedEntity, StringComparison.OrdinalIgnoreCase));
    }

    private static double GetEntityEvidenceScore(MetadataSemanticSearchResult candidate, string entitySemanticText)
    {
        if (candidate.Table is null || string.IsNullOrWhiteSpace(entitySemanticText)) return 0;
        var normalizedEntity = NormalizeSemanticText(entitySemanticText);
        var score = 0d;
        if (NormalizeSemanticText(candidate.Table.TableComment ?? string.Empty).Equals(normalizedEntity, StringComparison.OrdinalIgnoreCase)) score += 100;
        if (NormalizeSemanticText(candidate.Table.TableName ?? string.Empty).Equals(normalizedEntity, StringComparison.OrdinalIgnoreCase)) score += 95;
        if (new[] { candidate.Semantic?.Keywords, candidate.Semantic?.Synonyms }.Any(value => ContainsExactSemanticToken(value, normalizedEntity))) score += 90;
        if (!string.IsNullOrWhiteSpace(candidate.Semantic?.BusinessMeaning) && NormalizeSemanticText(candidate.Semantic.BusinessMeaning).Contains(normalizedEntity, StringComparison.OrdinalIgnoreCase)) score += 80;
        if (!string.IsNullOrWhiteSpace(candidate.Column?.ColumnComment) && NormalizeSemanticText(candidate.Column.ColumnComment).Contains(normalizedEntity, StringComparison.OrdinalIgnoreCase)) score += 70;
        if (!string.IsNullOrWhiteSpace(candidate.Column?.ColumnName) && NormalizeSemanticText(candidate.Column.ColumnName).Contains(normalizedEntity, StringComparison.OrdinalIgnoreCase)) score += 65;

        var columnName = NormalizeSemanticText(candidate.Column?.ColumnName ?? string.Empty);
        var columnComment = NormalizeSemanticText(candidate.Column?.ColumnComment ?? string.Empty);
        var semanticMeaning = NormalizeSemanticText(candidate.Semantic?.BusinessMeaning ?? string.Empty);
        if (IsEntityIdentifierColumn(columnName, columnComment, semanticMeaning, normalizedEntity)) score += 20;

        if (IsLikelyForeignKeyColumn(columnName, columnComment, semanticMeaning)) score -= 100;
        if (IsLikelyMeasureColumn(columnName, columnComment, semanticMeaning)) score -= 35;
        return score;
    }

    private static bool ContainsExactSemanticToken(string? value, string normalizedEntity)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = NormalizeSemanticText(value);
        if (normalized.Equals(normalizedEntity, StringComparison.OrdinalIgnoreCase)) return true;
        var separators = new[] { ',', '，', ';', '；', '/', '、', '|', '\n', '\r' };
        return value.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(token => NormalizeSemanticText(token).Equals(normalizedEntity, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEntityIdentifierColumn(string columnName, string columnComment, string semanticMeaning, string normalizedEntity)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return false;
        if (columnName.Equals("code", StringComparison.OrdinalIgnoreCase) || columnName.EndsWith("code", StringComparison.OrdinalIgnoreCase) || columnName.Contains("number", StringComparison.OrdinalIgnoreCase) || columnName.Contains("no", StringComparison.OrdinalIgnoreCase)) return true;
        return columnComment.Contains("单号", StringComparison.OrdinalIgnoreCase) || columnComment.Contains("编码", StringComparison.OrdinalIgnoreCase) || semanticMeaning.Contains("业务编码", StringComparison.OrdinalIgnoreCase) || semanticMeaning.Contains("单号", StringComparison.OrdinalIgnoreCase) || semanticMeaning.Contains("唯一编号", StringComparison.OrdinalIgnoreCase) || semanticMeaning.Contains(normalizedEntity, StringComparison.OrdinalIgnoreCase) && (semanticMeaning.Contains("编码", StringComparison.OrdinalIgnoreCase) || semanticMeaning.Contains("编号", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLikelyForeignKeyColumn(string columnName, string columnComment, string semanticMeaning)
    {
        if (columnName.EndsWith("id", StringComparison.OrdinalIgnoreCase) || columnName.EndsWith("_id", StringComparison.OrdinalIgnoreCase)) return true;
        return columnComment.Contains("关联", StringComparison.OrdinalIgnoreCase) || semanticMeaning.Contains("关联", StringComparison.OrdinalIgnoreCase) || semanticMeaning.Contains("外键", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyMeasureColumn(string columnName, string columnComment, string semanticMeaning)
    {
        var text = $"{columnName}|{columnComment}|{semanticMeaning}";
        return new[] { "quantity", "amount", "price", "库存", "数量", "金额", "价格", "重量", "体积", "余额" }.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractEntitySemanticText(string semanticText)
    {
        var normalized = NormalizeSemanticText(semanticText);
        foreach (var suffix in new[] { "数量", "个数", "总数", "数目" })
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return normalized[..^suffix.Length];
        return string.Empty;
    }

    private static bool MatchesExactEntityTableSemanticText(MetadataSemanticSearchResult candidate, string entitySemanticText)
    {
        if (candidate.Table is null) return false;
        var normalizedEntity = NormalizeSemanticText(entitySemanticText);
        if (normalizedEntity.Length == 0) return false;
        return new[] { candidate.Table.TableComment, candidate.Table.TableName }.Any(value => !string.IsNullOrWhiteSpace(value) && NormalizeSemanticText(value).Equals(normalizedEntity, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsTableEntitySemanticText(MetadataSemanticSearchResult candidate, string entitySemanticText)
    {
        if (candidate.Table is null) return false;
        var normalizedEntity = NormalizeSemanticText(entitySemanticText);
        if (normalizedEntity.Length == 0) return false;
        return new[] { candidate.Table.TableName, candidate.Table.TableComment, candidate.Table.SearchText }.Any(value => !string.IsNullOrWhiteSpace(value) && NormalizeSemanticText(value).Contains(normalizedEntity, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsEntitySemanticText(MetadataSemanticSearchResult candidate, string entitySemanticText)
    {
        if (string.IsNullOrWhiteSpace(entitySemanticText)) return false;
        var normalizedEntity = NormalizeSemanticText(entitySemanticText);
        if (normalizedEntity.Length == 0) return false;
        return new[] { candidate.Table?.TableName, candidate.Table?.TableComment, candidate.Table?.SearchText, candidate.Semantic?.BusinessMeaning, candidate.Semantic?.Keywords, candidate.Semantic?.Synonyms, candidate.Semantic?.ExampleQuestions, candidate.Semantic?.SearchText, candidate.Column?.ColumnName, candidate.Column?.ColumnComment }.Any(value => !string.IsNullOrWhiteSpace(value) && NormalizeSemanticText(value).Contains(normalizedEntity, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsSemanticText(MetadataSemanticSearchResult candidate, string semanticText)
    {
        var normalized = NormalizeSemanticText(semanticText);
        if (normalized.Length == 0) return false;
        return new[] { candidate.Table?.TableName, candidate.Table?.TableComment, candidate.Table?.SearchText, candidate.Column?.ColumnName, candidate.Column?.ColumnComment, candidate.Semantic?.BusinessMeaning, candidate.Semantic?.Keywords, candidate.Semantic?.Synonyms, candidate.Semantic?.ExampleQuestions, candidate.Semantic?.SearchText }.Any(value => !string.IsNullOrWhiteSpace(value) && NormalizeSemanticText(value).Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeSemanticText(string value) => new(value.Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-').ToArray());
    private static string GetCandidateBindingKey(MetadataSemanticSearchResult candidate) => $"{candidate.Table?.Id ?? 0}:{candidate.Column?.Id ?? 0}";

    private static SemanticApplicabilityCandidate? ToCandidate(MetadataSemanticSearchResult? candidate)
    {
        if (candidate?.Table is null || candidate.Column is null) return null;
        return new SemanticApplicabilityCandidate { VectorType = candidate.VectorType, VectorId = candidate.VectorId, Score = candidate.Score, Table = candidate.Table.TableName, Column = candidate.Column.ColumnName, BusinessMeaning = candidate.Semantic?.BusinessMeaning };
    }

    private static SemanticApplicabilityResolution? ToResolution(MetadataSemanticSearchResult? candidate)
    {
        if (candidate?.Table is null || candidate.Column is null) return null;
        return new SemanticApplicabilityResolution { TableId = candidate.Table.Id, DataSourceId = candidate.Table.DataSourceId, ColumnId = candidate.Column.Id, Table = candidate.Table.TableName, Column = candidate.Column.ColumnName, BusinessMeaning = candidate.Semantic?.BusinessMeaning, Score = candidate.Score };
    }

    private static string BuildColumnReason(string state, bool lexicalMatch, int metricCount) => metricCount > 1 ? $"Primary Metric semantic applicability={state}; Golden Case contains {metricCount} metrics and the primary metric was evaluated first." : state switch { "Resolved" => "Direct semantic candidate matched the requested metric semantic text.", "Ambiguous" => "Multiple semantic candidates contain direct evidence for the Golden metric; Applicability remains Ambiguous.", _ when !lexicalMatch => "Top semantic candidate does not provide sufficient lexical evidence for the Golden metric.", _ => "No stable semantic applicability could be resolved." };
    private static string BuildEntityCountReason(string state, int metricCount, bool topLexicalMatch, string entitySemanticText) => metricCount > 1 ? $"Primary EntityCount semantic applicability={state}; Golden Case contains {metricCount} metrics and the primary metric was evaluated first." : state switch { "Resolved" => string.IsNullOrWhiteSpace(entitySemanticText) ? "Direct EntityCount semantic evidence matched an existing table/column binding." : "Direct EntityCount semantic evidence matched a stable business-entity table binding.", "Ambiguous" => "Multiple table-level semantic candidates contain direct EntityCount evidence; Applicability remains Ambiguous.", _ when !topLexicalMatch => "Top semantic candidate does not provide sufficient evidence for the Golden metric.", _ => "No direct EntityCount semantic evidence could be resolved from the current semantic candidates." };
}
