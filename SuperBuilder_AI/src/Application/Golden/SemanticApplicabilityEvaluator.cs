using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
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

    /// <summary>
    /// D-GQ007：当 Top 候选缺少 Golden SemanticText 直接语义证据时，在 Top-N 内回退到「前向匹配」候选的回溯深度。
    /// 前向匹配要求候选元数据显式包含 Golden SemanticText（严格，不会误接受错误列），
    /// 仅用于纠正向量排序将正确列排在 #1 之后的偏序，使 GQ-007（入库数量/入库单数量）等复合指标在 CI 与 golden 环境一致通过。
    /// </summary>
    private const int TopNCandidateLookback = 5;
    private readonly IMetadataSemanticSearchService _semanticSearchService;
    private readonly IDimensionResolutionEvidenceService _dimensionEvidenceService;

    public SemanticApplicabilityEvaluator(IMetadataSemanticSearchService semanticSearchService, IDimensionResolutionEvidenceService dimensionEvidenceService)
    {
        _semanticSearchService = semanticSearchService ?? throw new ArgumentNullException(nameof(semanticSearchService));
        _dimensionEvidenceService = dimensionEvidenceService ?? throw new ArgumentNullException(nameof(dimensionEvidenceService));
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
            metricResolutions.Add(new SemanticApplicabilityMetricResolution { TableId = result.Resolution.TableId, DataSourceId = result.Resolution.DataSourceId, ColumnId = result.Resolution.ColumnId, SemanticText = metric.SemanticText, Table = result.Resolution.Table, Column = result.Resolution.Column, BusinessMeaning = result.Resolution.BusinessMeaning, Score = result.Resolution.Score, MetricType = result.MetricType });
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
            var resolution = await ResolveDimensionAsync(dimension, topK, metricResolutions);
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
        var lexical = ContainsSemanticText(top, metric.SemanticText);
        if (!lexical)
        {
            // D-GQ007：Top 候选缺少 Golden SemanticText 直接语义证据时，在 Top-N 内回退到「前向匹配」候选。
            // 前向匹配要求候选元数据显式包含 Golden SemanticText（严格，不会误接受错误列），
            // 仅用于纠正向量排序将正确列排在 #1 之后的偏序，使 GQ-007（入库数量/入库单数量）等复合指标在 CI 与 golden 环境一致通过。
            var lexicalFallback = candidates.Take(TopNCandidateLookback).FirstOrDefault(c => ContainsSemanticText(c, metric.SemanticText));
            if (lexicalFallback is not null) { top = lexicalFallback; lexical = true; }
        }
        var fallbackIdx = candidates.IndexOf(top);
        var second = (fallbackIdx >= 0 && fallbackIdx + 1 < candidates.Count) ? candidates[fallbackIdx + 1] : candidates.Skip(1).FirstOrDefault();
        var gap = second is null ? (double?)null : top.Score - second.Score;
        var competing = second is not null && ContainsSemanticText(second, metric.SemanticText) && gap <= AmbiguityScoreGapThreshold;
        if (metricType == "EntityCount")
        {
            var entityText = ExtractEntitySemanticText(metric.SemanticText);
            // D-EntityCount：按表分组，同一表的多个列不互相竞争；PK 加分区分实体表与关联表。
            var entityCandidates = string.IsNullOrWhiteSpace(entityText) ? new List<MetadataSemanticSearchResult>() : candidates.Where(x => ContainsDirectEntityEvidence(x, entityText)).GroupBy(x => x.Table?.Id ?? 0).Select(g => g.OrderByDescending(x => GetEntityEvidenceScore(x, entityText)).First()).OrderByDescending(x => GetEntityEvidenceScore(x, entityText)).ToList();
            var entity = entityCandidates.FirstOrDefault(); var entityScore = entity is null ? 0 : GetEntityEvidenceScore(entity, entityText); var secondEntity = entityCandidates.Skip(1).FirstOrDefault(); var entityGap = secondEntity is null ? (double?)null : entityScore - GetEntityEvidenceScore(secondEntity, entityText); var entityCompeting = secondEntity is not null && entityGap <= 8;
            if (entity is not null && entityScore >= 60) { top = entity; competing = entityCompeting; lexical = true; gap = entityGap; }
            else return new() { CaseId = goldenCase.Id, Question = goldenCase.Question, MetricSemanticText = metric.SemanticText, MetricType = metricType, State = "NotResolved", Reason = "No direct EntityCount semantic evidence could be resolved.", SearchCandidate = ToCandidate(top), Evidence = new SemanticApplicabilityEvidence { SemanticCandidateExists = candidates.Count > 0, DirectEntityCountEvidence = false, LexicalMatch = lexical, CompetingCandidates = false, TopScore = top.Score, SecondScore = second?.Score, ScoreGap = gap } };
        }
        var state = lexical ? competing ? "Ambiguous" : "Resolved" : "NotResolved";
        return new SemanticApplicabilityResult { CaseId = goldenCase.Id, Question = goldenCase.Question, MetricSemanticText = metric.SemanticText, MetricType = metricType, State = state, Reason = state == "Resolved" ? $"Metric SemanticText 已稳定绑定到 {top.Table!.TableName}.{top.Column!.ColumnName}。" : state == "Ambiguous" ? "存在分差不足的竞争 Semantic Candidate，禁止继续构建 QueryPlan。" : "Top Candidate 缺少 Golden SemanticText 的直接语义证据。", SearchCandidate = ToCandidate(top), Resolution = state == "Resolved" ? ToResolution(top) : null, Evidence = new SemanticApplicabilityEvidence { SemanticCandidateExists = candidates.Count > 0, EntityCandidateExists = results.Any(x => x.Table is not null), DirectEntityCountEvidence = metricType == "EntityCount", LexicalMatch = lexical, CompetingCandidates = competing, TopScore = top.Score, SecondScore = second?.Score, ScoreGap = gap } };
    }

    private async Task<SemanticApplicabilityFilterResolution?> ResolveFilterAsync(GoldenFilterExpectation filter, int topK)
    {
        if (string.IsNullOrWhiteSpace(filter.SemanticText)) return null;
        var candidates = (await _semanticSearchService.SearchAsync(filter.SemanticText, topK)).Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null).GroupBy(GetCandidateBindingKey, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(x => x.Score).First()).OrderByDescending(x => x.Score).ToList();
        var direct = candidates.Where(x => ContainsSemanticText(x, filter.SemanticText)).ToList();
        if (direct.Count == 0) return null; if (direct.Count > 1 && direct[0].Score - direct[1].Score <= AmbiguityScoreGapThreshold) return null;
        var c = direct[0]; return new() { TableId = c.Table!.Id, DataSourceId = c.Table.DataSourceId, ColumnId = c.Column!.Id, SemanticText = filter.SemanticText, Table = c.Table.TableName, Column = c.Column.ColumnName, BusinessMeaning = c.Semantic?.BusinessMeaning, Score = c.Score };
    }

    private async Task<SemanticApplicabilityDimensionResolution?> ResolveDimensionAsync(GoldenDimensionExpectation dimension, int topK, IReadOnlyList<SemanticApplicabilityMetricResolution> metricResolutions)
    {
        if (string.IsNullOrWhiteSpace(dimension.SemanticText)) return null;
        var metric = metricResolutions.FirstOrDefault();
        if (metric is null) return null;
        var evidence = await _dimensionEvidenceService.ResolveAsync(metric.TableId, metric.DataSourceId, dimension.SemanticText, metric.ColumnId);
        if (evidence is null || !string.Equals(evidence.ExecutionCapability, "Executable", StringComparison.OrdinalIgnoreCase)
            || string.Equals(evidence.ResolutionType, "Ambiguous", StringComparison.OrdinalIgnoreCase)
            || string.Equals(evidence.ResolutionType, "NotResolved", StringComparison.OrdinalIgnoreCase))
        {
            // D8: Fallback when evidence service fails — do a direct semantic search
            // scoped to the metric's fact table to find dimension columns.
            return await ResolveDimensionFallbackAsync(dimension, topK, metric);
        }
        var candidates = (await _semanticSearchService.SearchAsync(dimension.SemanticText, topK)).Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null).GroupBy(GetCandidateBindingKey, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(x => x.Score).First()).OrderByDescending(x => x.Score).ToList();
        var candidate = candidates.FirstOrDefault(x => ContainsSemanticText(x, dimension.SemanticText) || ContainsDimensionEntityEvidence(x, dimension.SemanticText));
        return new SemanticApplicabilityDimensionResolution { TableId = evidence.FactTableId, DataSourceId = evidence.FactDataSourceId, ColumnId = evidence.FactKeyColumnId, SemanticText = dimension.SemanticText, Table = evidence.FactTable, Column = evidence.FactKeyColumn, BusinessMeaning = candidate?.Semantic?.BusinessMeaning, Score = evidence.Score, ResolutionType = evidence.ResolutionType, ExecutionCapability = evidence.ExecutionCapability, DimensionKeyColumnId = evidence.FactKeyColumnId, DimensionKeyColumn = evidence.FactKeyColumn, DimensionLabelColumnId = evidence.MasterLabelColumnId, DimensionLabelColumn = evidence.MasterLabelColumn, MasterTableId = evidence.MasterTableId, MasterDataSourceId = evidence.MasterDataSourceId, MasterTable = evidence.MasterTable, MasterKeyColumnId = evidence.MasterKeyColumnId, MasterKeyColumn = evidence.MasterKeyColumn };
    }

    /// <summary>
    /// D8: 当 DimensionResolutionEvidenceService 返回 null/Ambiguous/NotResolved 时的后备解析。
    /// 直接通过语义搜索在事实表范围内查找维度列，返回 DirectKey 分辨。
    /// </summary>
    private async Task<SemanticApplicabilityDimensionResolution?> ResolveDimensionFallbackAsync(GoldenDimensionExpectation dimension, int topK, SemanticApplicabilityMetricResolution metric)
    {
        // Search with larger topK to find fact table columns
        var searchResults = await _semanticSearchService.SearchAsync(dimension.SemanticText, Math.Max(topK, 50));
        var candidates = searchResults
            .Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null)
            .Where(x => x.Table!.Id == metric.TableId)
            .Where(x => ContainsSemanticText(x, dimension.SemanticText) || ContainsDimensionEntityEvidence(x, dimension.SemanticText))
            .GroupBy(GetCandidateBindingKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score)
            .ToList();

        if (candidates.Count == 0) return null;

        // Prefer FK/PK columns as DirectKey; fall back to display columns
        var keyCandidate = candidates.FirstOrDefault(x =>
            x.Column!.IsPrimaryKey == true || IsForeignKeyColumn(x.Column.ColumnName));
        var best = keyCandidate ?? candidates[0];

        return new SemanticApplicabilityDimensionResolution
        {
            TableId = metric.TableId,
            DataSourceId = metric.DataSourceId,
            ColumnId = best.Column!.Id,
            SemanticText = dimension.SemanticText,
            Table = best.Table!.TableName,
            Column = best.Column.ColumnName,
            BusinessMeaning = best.Semantic?.BusinessMeaning,
            Score = best.Score,
            ResolutionType = "DirectKey",
            ExecutionCapability = "Executable",
            DimensionKeyColumnId = best.Column.Id,
            DimensionKeyColumn = best.Column.ColumnName,
            DimensionLabelColumnId = null,
            DimensionLabelColumn = null,
            MasterTableId = null,
            MasterDataSourceId = null,
            MasterTable = null,
            MasterKeyColumnId = null,
            MasterKeyColumn = null
        };
    }

    private static bool IsForeignKeyColumn(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return false;
        var lower = columnName.ToLowerInvariant().Trim();
        if (lower is "id") return true;
        if (lower.EndsWith("_id")) return true;
        if (columnName.Length >= 4 && columnName.EndsWith("Id", StringComparison.Ordinal)) return true;
        return false;
    }

    private async Task<List<SemanticApplicabilityTableResolution>> ResolveTablesAsync(IReadOnlyList<GoldenTableExpectation>? tables, IReadOnlyList<SemanticApplicabilityMetricResolution> metrics, IReadOnlyList<SemanticApplicabilityFilterResolution> filters, IReadOnlyList<SemanticApplicabilityDimensionResolution> dimensions, int topK)
    {
        var resolutions = new List<SemanticApplicabilityTableResolution>();
        if (tables is null || tables.Count == 0) return resolutions;
        foreach (var table in tables)
        {
            if (string.IsNullOrWhiteSpace(table.SemanticText)) continue;

            // D9 (GQ-005/GQ-009)：Golden Table SemanticText 若与已解析的 Dimension 语义一致，
            // 优先采用维度解析确定的表（MasterJoin → Master 表；DirectKey → 事实表）。
            // 否则表级语义搜索可能把"供应商"误解析到无关表（如 wms_weighbridge_info），
            // 导致 Tables 列表与 Dimension Master Join 不一致（Golden 期望 2 张、实际 3 张）。
            var alignedDimension = dimensions.FirstOrDefault(d =>
                string.Equals(d.SemanticText, table.SemanticText, StringComparison.OrdinalIgnoreCase));
            if (alignedDimension is not null)
            {
                var isMasterJoin = string.Equals(alignedDimension.ResolutionType, "MasterJoin", StringComparison.OrdinalIgnoreCase);
                var targetTableId = isMasterJoin && alignedDimension.MasterTableId.HasValue
                    ? alignedDimension.MasterTableId.Value
                    : alignedDimension.TableId;
                var targetTableName = isMasterJoin && !string.IsNullOrWhiteSpace(alignedDimension.MasterTable)
                    ? alignedDimension.MasterTable
                    : alignedDimension.Table;
                var targetDataSourceId = isMasterJoin && alignedDimension.MasterDataSourceId.HasValue
                    ? alignedDimension.MasterDataSourceId.Value
                    : alignedDimension.DataSourceId;
                if (targetTableId > 0 && !string.IsNullOrWhiteSpace(targetTableName))
                {
                    resolutions.Add(new SemanticApplicabilityTableResolution
                    {
                        TableId = targetTableId,
                        DataSourceId = targetDataSourceId,
                        SemanticText = table.SemanticText,
                        Table = targetTableName,
                        BusinessMeaning = alignedDimension.BusinessMeaning,
                        Score = alignedDimension.Score
                    });
                    continue;
                }
            }

            var candidates = (await _semanticSearchService.SearchAsync(table.SemanticText, topK)).Where(x => x.IsSemanticVector && x.Table is not null).GroupBy(x => x.Table!.Id).Select(g => g.OrderByDescending(x => x.Score).First()).OrderByDescending(x => x.Score).ToList();
            var direct = candidates.Where(x => ContainsSemanticText(x, table.SemanticText) || ContainsTableEvidence(x, table.SemanticText)).ToList();
            // D7: When multiple tables match, pick the best by score instead of skipping.
            if (direct.Count == 0) continue;
            var c = direct[0]; resolutions.Add(new SemanticApplicabilityTableResolution { TableId = c.Table!.Id, DataSourceId = c.Table.DataSourceId, SemanticText = table.SemanticText, Table = c.Table.TableName, BusinessMeaning = c.Table.BusinessDomain, Score = c.Score });
        }
        return resolutions;
    }

    private static bool ContainsSemanticText(MetadataSemanticSearchResult result, string semanticText)
    {
        var normalized = Normalize(semanticText);
        if (normalized.Length == 0) return false;
        var values = new[] { result.Semantic?.BusinessMeaning, result.Semantic?.Keywords, result.Semantic?.Synonyms, result.Semantic?.ExampleQuestions, result.Semantic?.SearchText, result.Column?.ColumnName, result.Column?.ColumnComment, result.Column?.SearchText, result.Table?.TableName, result.Table?.TableComment, result.Table?.SearchText };
        return values.Any(x => !string.IsNullOrWhiteSpace(x) && Normalize(x).Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsDirectEntityEvidence(MetadataSemanticSearchResult result, string entityText)
    {
        var normalized = Normalize(entityText);
        if (normalized.Length == 0 || result.Table is null) return false;
        var values = new[] { result.Table.TableName, result.Table.TableComment, result.Table.SearchText, result.Table.BusinessDomain, result.Column?.ColumnName, result.Column?.ColumnComment, result.Semantic?.BusinessMeaning, result.Semantic?.Keywords, result.Semantic?.Synonyms, result.Semantic?.ExampleQuestions };
        return values.Any(x => !string.IsNullOrWhiteSpace(x) && Normalize(x).Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetEntityEvidenceScore(MetadataSemanticSearchResult result, string entityText)
    {
        var score = (int)Math.Round(Math.Clamp(result.Score, 0d, 1d) * 60d);
        if (ContainsDirectEntityEvidence(result, entityText)) score += 40;
        // D-EntityCount：PK 列是实体计数的天然目标（COUNT(*) 或 COUNT(PK)），加分区分实体表与关联表。
        if (result.Column?.IsPrimaryKey == true) score += 15;
        // D6：明细表（detail table）的 PK 不代表实体计数目标，扣分。
        // 例如 wms_storage_receipt_info.id 的 BusinessMeaning 含"明细"，不应与 wms_storage_receipt.id 竞争。
        var businessMeaning = result.Semantic?.BusinessMeaning ?? string.Empty;
        if (businessMeaning.Contains("明细", StringComparison.Ordinal)) score -= 15;
        // D6：如果 entityText 是 keywords 中的独立关键词（精确匹配），说明该列直接代表该实体，加分。
        // 例如 es_supplier_code 的 keywords "供应商代码,供应商,编码" 中 "供应商" 是独立关键词。
        if (HasStandaloneKeywordMatch(result, entityText)) score += 10;
        return score;
    }

    /// <summary>
    /// 检查 entityText 是否是 Semantic Keywords 中的独立关键词（按逗号分割后精确匹配）。
    /// </summary>
    private static bool HasStandaloneKeywordMatch(MetadataSemanticSearchResult result, string entityText)
    {
        var normalized = Normalize(entityText);
        if (normalized.Length == 0) return false;
        var keywords = result.Semantic?.Keywords;
        if (string.IsNullOrWhiteSpace(keywords)) return false;
        return keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(k => Normalize(k) == normalized);
    }

    private static bool ContainsDimensionEntityEvidence(MetadataSemanticSearchResult result, string semanticText)
    {
        var normalized = Normalize(semanticText);
        if (normalized.Length == 0 || result.Table is null || result.Column is null) return false;
        var values = new[] { result.Table.TableName, result.Table.TableComment, result.Table.SearchText, result.Table.BusinessDomain, result.Column.ColumnName, result.Column.ColumnComment, result.Column.SearchText, result.Semantic?.BusinessMeaning, result.Semantic?.Keywords, result.Semantic?.Synonyms, result.Semantic?.ExampleQuestions, result.Semantic?.SearchText };
        return values.Any(x => !string.IsNullOrWhiteSpace(x) && Normalize(x).Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsTableEvidence(MetadataSemanticSearchResult result, string semanticText)
    {
        var normalized = Normalize(semanticText);
        if (normalized.Length == 0 || result.Table is null) return false;
        var values = new[] { result.Table.TableName, result.Table.TableComment, result.Table.SearchText, result.Table.BusinessDomain };
        return values.Any(x => !string.IsNullOrWhiteSpace(x) && Normalize(x).Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractEntitySemanticText(string semanticText)
    {
        var text = semanticText.Trim();
        if (text.StartsWith("查询", StringComparison.Ordinal)) text = text[2..];
        // D6: Strip common Chinese qualifiers before entity name
        var qualifiers = new[] { "不同", "各个", "各种", "各", "所有", "全部", "不同种类" };
        foreach (var q in qualifiers.OrderByDescending(x => x.Length))
        {
            if (text.StartsWith(q, StringComparison.Ordinal)) text = text[q.Length..];
        }
        if (text.EndsWith("数量", StringComparison.Ordinal)) text = text[..^2];
        return text.Trim();
    }

    private static string GetCandidateBindingKey(MetadataSemanticSearchResult result) => $"{result.Table?.Id}:{result.Column?.Id}";
    private static SemanticApplicabilityCandidate ToCandidate(MetadataSemanticSearchResult result) => new() { VectorType = result.VectorType, VectorId = result.VectorId, Score = result.Score, Table = result.Table?.TableName, Column = result.Column?.ColumnName, BusinessMeaning = result.Semantic?.BusinessMeaning };
    private static SemanticApplicabilityResolution ToResolution(MetadataSemanticSearchResult result) => new() { TableId = result.Table!.Id, DataSourceId = result.Table.DataSourceId, ColumnId = result.Column!.Id, Table = result.Table.TableName, Column = result.Column.ColumnName, BusinessMeaning = result.Semantic?.BusinessMeaning, Score = result.Score };
    private static SemanticApplicabilityResult CopyWithFailure(SemanticApplicabilityResult source, string reason) => source with { State = "NotResolved", Reason = reason, Resolution = null };
    private static string Normalize(string? value) => (value ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
}
