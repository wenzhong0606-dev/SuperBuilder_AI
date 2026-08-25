using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.13.2：统一 Semantic Applicability。
/// Metric / Filter / Dimension / Table 都必须先完成稳定语义解析，再允许 QueryPlan 消费。
/// </summary>
public sealed class SemanticApplicabilityEvaluator
{
    private const double AmbiguityScoreGapThreshold = 0.05d;
    private readonly IMetadataSemanticSearchService _semanticSearchService;

    public SemanticApplicabilityEvaluator(IMetadataSemanticSearchService semanticSearchService)
    {
        _semanticSearchService = semanticSearchService ?? throw new ArgumentNullException(nameof(semanticSearchService));
    }

    public async Task<SemanticApplicabilityResult> EvaluateAsync(GoldenQueryCase goldenCase, int topK = 10)
    {
        ArgumentNullException.ThrowIfNull(goldenCase);
        var metrics = goldenCase.Expected?.Metrics ?? new List<GoldenMetricExpectation>();
        if (metrics.Count == 0)
            return new() { CaseId = goldenCase.Id, Question = goldenCase.Question, State = "NotApplicable", MetricType = "Unknown", Reason = "Golden Case does not define a Metric." };

        var metricResolutions = new List<SemanticApplicabilityMetricResolution>();
        SemanticApplicabilityResult? firstMetricResult = null;
        for (var i = 0; i < metrics.Count; i++)
        {
            var metric = metrics[i];
            var result = await ResolveMetricAsync(goldenCase, metric, metrics.Count, topK);
            if (firstMetricResult is null) firstMetricResult = result;
            if (!string.Equals(result.State, "Resolved", StringComparison.OrdinalIgnoreCase))
                return result with { MetricResolutions = metricResolutions };
            if (result.Resolution is null)
                return CopyWithFailure(result, $"Metric “{metric.SemanticText}” 已标记 Resolved，但没有稳定 Resolution。");
            metricResolutions.Add(new SemanticApplicabilityMetricResolution
            {
                TableId = result.Resolution.TableId, DataSourceId = result.Resolution.DataSourceId, ColumnId = result.Resolution.ColumnId,
                SemanticText = metric.SemanticText, Table = result.Resolution.Table, Column = result.Resolution.Column,
                BusinessMeaning = result.Resolution.BusinessMeaning, Score = result.Resolution.Score
            });
        }

        var filterResolutions = new List<SemanticApplicabilityFilterResolution>();
        foreach (var filter in goldenCase.Expected?.Filters ?? new List<GoldenFilterExpectation>())
        {
            var resolution = await ResolveFilterAsync(filter, topK);
            if (resolution is null) return CopyWithFailure(firstMetricResult!, $"Filter 语义“{filter.SemanticText}”无法解析为稳定的 Metadata 物理绑定。");
            filterResolutions.Add(resolution);
        }

        var dimensionResolutions = new List<SemanticApplicabilityDimensionResolution>();
        foreach (var dimension in goldenCase.Expected?.Dimensions ?? new List<GoldenDimensionExpectation>())
        {
            var resolution = await ResolveDimensionAsync(dimension, topK);
            if (resolution is null) return CopyWithFailure(firstMetricResult!, $"Dimension 语义“{dimension.SemanticText}”无法解析为稳定的 Metadata 物理绑定。");
            dimensionResolutions.Add(resolution);
        }

        var tableResolutions = await ResolveTablesAsync(goldenCase.Expected?.Tables, metricResolutions, filterResolutions, dimensionResolutions, topK);
        if (goldenCase.Expected?.Tables is not null && tableResolutions.Count != goldenCase.Expected.Tables.Count)
            return CopyWithFailure(firstMetricResult!, "Golden Table SemanticText 没有形成逐项稳定 Table Resolution。");

        return new SemanticApplicabilityResult
        {
            CaseId = firstMetricResult!.CaseId, Question = firstMetricResult.Question, MetricSemanticText = firstMetricResult.MetricSemanticText,
            MetricType = firstMetricResult.MetricType, State = "Resolved", Reason = metrics.Count > 1 ? $"All {metrics.Count} Golden Metrics、Filters、Dimensions、Tables 均已完成稳定 Semantic Resolution。" : "Golden Metrics、Filters、Dimensions、Tables 均已完成稳定 Semantic Resolution。",
            SearchCandidate = firstMetricResult.SearchCandidate, Resolution = firstMetricResult.Resolution, MetricResolutions = metricResolutions,
            FilterResolutions = filterResolutions, DimensionResolutions = dimensionResolutions, TableResolutions = tableResolutions,
            Evidence = firstMetricResult.Evidence
        };
    }

    private async Task<SemanticApplicabilityResult> ResolveMetricAsync(GoldenQueryCase goldenCase, GoldenMetricExpectation metric, int metricCount, int topK)
    {
        if (string.IsNullOrWhiteSpace(metric.SemanticText)) return new() { CaseId = goldenCase.Id, Question = goldenCase.Question, MetricSemanticText = metric.SemanticText, MetricType = "Unknown", State = "NotResolved", Reason = "Metric SemanticText 为空。" };
        var metricType = metric.Aggregation == QueryAggregation.Count ? "EntityCount" : "ColumnMetric";
        var results = await _semanticSearchService.SearchAsync(metric.SemanticText, topK);
        if (results.Count == 0) return new() { CaseId = goldenCase.Id, Question = goldenCase.Question, MetricSemanticText = metric.SemanticText, MetricType = metricType, State = "NotResolved", Reason = "Semantic Search returned no candidates." };
        var candidates = results.Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null).GroupBy(GetCandidateBindingKey, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(x => x.Score).First()).OrderByDescending(x => x.Score).ToList();
        if (candidates.Count == 0) return new() { CaseId = goldenCase.Id, Question = goldenCase.Question, MetricSemanticText = metric.SemanticText, MetricType = metricType, State = "NotResolved", Reason = "No semantic vector candidate with physical binding was returned." };
        var top = candidates[0];
        var second = candidates.Skip(1).FirstOrDefault();
        var lexical = ContainsSemanticText(top, metric.SemanticText);
        var gap = second is null ? (double?)null : top.Score - second.Score;
        var competing = second is not null && ContainsSemanticText(second, metric.SemanticText) && gap <= AmbiguityScoreGapThreshold;

        if (metricType == "EntityCount")
        {
            var entityText = ExtractEntitySemanticText(metric.SemanticText);
            var entityCandidates = string.IsNullOrWhiteSpace(entityText) ? new List<MetadataSemanticSearchResult>() : candidates.Where(x => ContainsDirectEntityEvidence(x, entityText)).OrderByDescending(x => GetEntityEvidenceScore(x, entityText)).ToList();
            var entity = entityCandidates.FirstOrDefault();
            var entityScore = entity is null ? 0 : GetEntityEvidenceScore(entity, entityText);
            var secondEntity = entityCandidates.Skip(1).FirstOrDefault();
            var entityGap = secondEntity is null ? (double?)null : entityScore - GetEntityEvidenceScore(secondEntity, entityText);
            var entityCompeting = secondEntity is not null && entityGap <= 8;
            if (entity is not null && entityScore >= 60)
            {
                top = entity;
                competing = entityCompeting;
                lexical = true;
                gap = entityGap;
            }
            else
                return new() { CaseId = goldenCase.Id, Question = goldenCase.Question, MetricSemanticText = metric.SemanticText, MetricType = metricType, State = "NotResolved", Reason = "No direct EntityCount semantic evidence could be resolved.", SearchCandidate = ToCandidate(top), Evidence = new SemanticApplicabilityEvidence { SemanticCandidateExists = candidates.Count > 0, DirectEntityCountEvidence = false, LexicalMatch = lexical, CompetingCandidates = false, TopScore = top.Score, SecondScore = second?.Score, ScoreGap = gap } };
        }

        var state = lexical ? competing ? "Ambiguous" : "Resolved" : "NotResolved";
        return new SemanticApplicabilityResult
        {
            CaseId = goldenCase.Id, Question = goldenCase.Question, MetricSemanticText = metric.SemanticText, MetricType = metricType, State = state,
            Reason = state == "Resolved" ? $"Metric SemanticText 已稳定绑定到 {top.Table!.TableName}.{top.Column!.ColumnName}。" : state == "Ambiguous" ? "存在分差不足的竞争 Semantic Candidate，禁止继续构建 QueryPlan。" : "Top Candidate 缺少 Golden SemanticText 的直接语义证据。",
            SearchCandidate = ToCandidate(top), Resolution = state == "Resolved" ? ToResolution(top) : null,
            Evidence = new SemanticApplicabilityEvidence { SemanticCandidateExists = candidates.Count > 0, EntityCandidateExists = results.Any(x => x.Table is not null), DirectEntityCountEvidence = metricType == "EntityCount", LexicalMatch = lexical, CompetingCandidates = competing, TopScore = top.Score, SecondScore = second?.Score, ScoreGap = gap }
        };
    }

    private async Task<SemanticApplicabilityFilterResolution?> ResolveFilterAsync(GoldenFilterExpectation filter, int topK)
    {
        if (string.IsNullOrWhiteSpace(filter.SemanticText)) return null;
        var candidates = (await _semanticSearchService.SearchAsync(filter.SemanticText, topK)).Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null).GroupBy(GetCandidateBindingKey, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(x => x.Score).First()).OrderByDescending(x => x.Score).ToList();
        var direct = candidates.Where(x => ContainsSemanticText(x, filter.SemanticText)).ToList();
        if (direct.Count == 0) return null;
        if (direct.Count > 1 && direct[0].Score - direct[1].Score <= AmbiguityScoreGapThreshold) return null;
        var c = direct[0];
        return new() { TableId = c.Table!.Id, DataSourceId = c.Table.DataSourceId, ColumnId = c.Column!.Id, SemanticText = filter.SemanticText, Table = c.Table.TableName, Column = c.Column.ColumnName, BusinessMeaning = c.Semantic?.BusinessMeaning, Score = c.Score };
    }

    private async Task<SemanticApplicabilityDimensionResolution?> ResolveDimensionAsync(GoldenDimensionExpectation dimension, int topK)
    {
        if (string.IsNullOrWhiteSpace(dimension.SemanticText)) return null;

        var queries = BuildDimensionSearchQueries(dimension.SemanticText);
        var allCandidates = new List<MetadataSemanticSearchResult>();
        foreach (var query in queries)
            allCandidates.AddRange(await _semanticSearchService.SearchAsync(query, topK));

        var candidates = allCandidates
            .Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null)
            .GroupBy(GetCandidateBindingKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score)
            .ToList();

        var direct = candidates
            .Where(x => ContainsSemanticText(x, dimension.SemanticText) || ContainsDimensionEntityEvidence(x, dimension.SemanticText))
            .ToList();

        if (direct.Count == 0)
            return null;

        if (direct.Count > 1 && direct[0].Score - direct[1].Score <= AmbiguityScoreGapThreshold)
            return null;

        var c = direct[0];
        return new()
        {
            TableId = c.Table!.Id,
            DataSourceId = c.Table.DataSourceId,
            ColumnId = c.Column!.Id,
            SemanticText = dimension.SemanticText,
            Table = c.Table.TableName,
            Column = c.Column.ColumnName,
            BusinessMeaning = c.Semantic?.BusinessMeaning,
            Score = c.Score
        };
    }

    private static IReadOnlyList<string> BuildDimensionSearchQueries(string semanticText)
    {
        var normalized = NormalizeSemanticText(semanticText);
        var queries = new List<string> { semanticText };
        if (normalized.Length > 0)
        {
            foreach (var suffix in new[] { "名称", "编码", "ID" })
            {
                var query = normalized + suffix;
                if (!queries.Contains(query, StringComparer.OrdinalIgnoreCase))
                    queries.Add(query);
            }
        }
        return queries;
    }

    private static bool ContainsDimensionEntityEvidence(MetadataSemanticSearchResult candidate, string dimensionText)
    {
        var normalized = NormalizeSemanticText(dimensionText);
        if (normalized.Length == 0 || candidate.Table is null)
            return false;

        var values = new[]
        {
            candidate.Table.TableName,
            candidate.Table.TableComment,
            candidate.Table.SearchText,
            candidate.Column?.ColumnName,
            candidate.Column?.ColumnComment,
            candidate.Semantic?.BusinessMeaning,
            candidate.Semantic?.Keywords,
            candidate.Semantic?.Synonyms,
            candidate.Semantic?.SearchText,
            candidate.Semantic?.ExampleQuestions
        };

        return values.Any(x => !string.IsNullOrWhiteSpace(x)
            && NormalizeSemanticText(x).Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<SemanticApplicabilityTableResolution>> ResolveTablesAsync(IReadOnlyList<GoldenTableExpectation>? expected, IReadOnlyList<SemanticApplicabilityMetricResolution> metrics, IReadOnlyList<SemanticApplicabilityFilterResolution> filters, IReadOnlyList<SemanticApplicabilityDimensionResolution> dimensions, int topK)
    {
        if (expected is null) return Array.Empty<SemanticApplicabilityTableResolution>();
        var results = new List<SemanticApplicabilityTableResolution>();
        foreach (var table in expected)
        {
            if (string.IsNullOrWhiteSpace(table.SemanticText)) return Array.Empty<SemanticApplicabilityTableResolution>();
            var candidates = (await _semanticSearchService.SearchAsync(table.SemanticText, topK)).Where(x => x.IsSemanticVector && x.Table is not null).GroupBy(x => $"{x.Table!.Id}:{x.Table.DataSourceId}", StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(x => x.Score).First()).OrderByDescending(x => x.Score).ToList();
            var direct = candidates.Where(x => ContainsTableSemanticText(x, table.SemanticText)).ToList();
            if (direct.Count == 0) return Array.Empty<SemanticApplicabilityTableResolution>();
            if (direct.Count > 1 && direct[0].Score - direct[1].Score <= AmbiguityScoreGapThreshold) return Array.Empty<SemanticApplicabilityTableResolution>();
            var c = direct[0];
            results.Add(new() { TableId = c.Table!.Id, DataSourceId = c.Table.DataSourceId, SemanticText = table.SemanticText, Table = c.Table.TableName, BusinessMeaning = c.Semantic?.BusinessMeaning, Score = c.Score });
        }
        return results;
    }

    private static SemanticApplicabilityResult CopyWithFailure(SemanticApplicabilityResult source, string reason) => new() { CaseId = source.CaseId, Question = source.Question, MetricSemanticText = source.MetricSemanticText, MetricType = source.MetricType, State = "NotResolved", Reason = reason, SearchCandidate = source.SearchCandidate, Resolution = source.Resolution, MetricResolutions = source.MetricResolutions, FilterResolutions = source.FilterResolutions, DimensionResolutions = source.DimensionResolutions, TableResolutions = source.TableResolutions, Evidence = source.Evidence };

    private static bool ContainsDirectEntityEvidence(MetadataSemanticSearchResult candidate, string entityText) => candidate.Table is not null && new[] { candidate.Table.TableName, candidate.Table.TableComment, candidate.Semantic?.BusinessMeaning, candidate.Semantic?.Keywords, candidate.Semantic?.Synonyms, candidate.Semantic?.SearchText, candidate.Column?.ColumnName, candidate.Column?.ColumnComment }.Any(x => !string.IsNullOrWhiteSpace(x) && NormalizeSemanticText(x).Contains(NormalizeSemanticText(entityText), StringComparison.OrdinalIgnoreCase));
    private static double GetEntityEvidenceScore(MetadataSemanticSearchResult candidate, string entityText)
    {
        if (candidate.Table is null) return 0;
        var e = NormalizeSemanticText(entityText); var score = 0d;
        if (NormalizeSemanticText(candidate.Table.TableComment ?? string.Empty).Equals(e, StringComparison.OrdinalIgnoreCase)) score += 100;
        if (NormalizeSemanticText(candidate.Table.TableName ?? string.Empty).Equals(e, StringComparison.OrdinalIgnoreCase)) score += 95;
        if (!string.IsNullOrWhiteSpace(candidate.Semantic?.BusinessMeaning) && NormalizeSemanticText(candidate.Semantic.BusinessMeaning).Contains(e, StringComparison.OrdinalIgnoreCase)) score += 80;
        if (!string.IsNullOrWhiteSpace(candidate.Column?.ColumnComment) && NormalizeSemanticText(candidate.Column.ColumnComment).Contains(e, StringComparison.OrdinalIgnoreCase)) score += 70;
        if (!string.IsNullOrWhiteSpace(candidate.Column?.ColumnName) && NormalizeSemanticText(candidate.Column.ColumnName).Contains(e, StringComparison.OrdinalIgnoreCase)) score += 65;
        var column = NormalizeSemanticText(candidate.Column?.ColumnName ?? string.Empty);
        if (column.EndsWith("id", StringComparison.OrdinalIgnoreCase) || column.EndsWith("_id", StringComparison.OrdinalIgnoreCase)) score -= 100;
        return score;
    }

    private static string ExtractEntitySemanticText(string text)
    {
        var n = NormalizeSemanticText(text);
        foreach (var suffix in new[] { "数量", "个数", "总数", "数目" }) if (n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return n[..^suffix.Length];
        return string.Empty;
    }

    private static bool ContainsSemanticText(MetadataSemanticSearchResult c, string text) => ContainsAny(c, text, c.Table?.TableName, c.Table?.TableComment, c.Table?.SearchText, c.Column?.ColumnName, c.Column?.ColumnComment, c.Semantic?.BusinessMeaning, c.Semantic?.Keywords, c.Semantic?.Synonyms, c.Semantic?.ExampleQuestions, c.Semantic?.SearchText);
    private static bool ContainsTableSemanticText(MetadataSemanticSearchResult c, string text) => ContainsAny(c, text, c.Table?.TableName, c.Table?.TableComment, c.Table?.SearchText, c.Semantic?.BusinessMeaning, c.Semantic?.Keywords, c.Semantic?.Synonyms, c.Semantic?.SearchText);
    private static bool ContainsAny(MetadataSemanticSearchResult _, string text, params string?[] values) { var n = NormalizeSemanticText(text); return n.Length > 0 && values.Any(v => !string.IsNullOrWhiteSpace(v) && NormalizeSemanticText(v).Contains(n, StringComparison.OrdinalIgnoreCase)); }
    private static string NormalizeSemanticText(string value) => new(value.Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-').ToArray());
    private static string GetCandidateBindingKey(MetadataSemanticSearchResult c) => $"{c.Table?.Id ?? 0}:{c.Column?.Id ?? 0}";
    private static SemanticApplicabilityCandidate? ToCandidate(MetadataSemanticSearchResult? c) => c?.Table is null || c.Column is null ? null : new() { VectorType = c.VectorType, VectorId = c.VectorId, Score = c.Score, Table = c.Table.TableName, Column = c.Column.ColumnName, BusinessMeaning = c.Semantic?.BusinessMeaning };
    private static SemanticApplicabilityResolution? ToResolution(MetadataSemanticSearchResult? c) => c?.Table is null || c.Column is null ? null : new() { TableId = c.Table.Id, DataSourceId = c.Table.DataSourceId, ColumnId = c.Column.Id, Table = c.Table.TableName, Column = c.Column.ColumnName, BusinessMeaning = c.Semantic?.BusinessMeaning, Score = c.Score };
}
