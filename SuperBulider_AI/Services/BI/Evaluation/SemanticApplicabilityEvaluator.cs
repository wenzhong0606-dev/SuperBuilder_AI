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
            .GroupBy(GetCandidateBindingKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Score).First())
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

        var metricApplicability = metricType == "EntityCount"
            ? EvaluateEntityCount(goldenCase, metric.SemanticText, metrics.Count, semanticCandidates, entityCandidates, topSemantic, secondSemantic, scoreGap)
            : EvaluateColumnMetric(goldenCase, metric.SemanticText, metrics.Count, semanticCandidates, entityCandidates, topSemantic, secondSemantic, scoreGap);

        if (!string.Equals(metricApplicability.State, "Resolved", StringComparison.OrdinalIgnoreCase))
            return metricApplicability;

        var filters = goldenCase.Expected?.Filters;
        var filterResolutions = new List<SemanticApplicabilityFilterResolution>();
        if (filters is not null)
        {
            foreach (var filter in filters)
            {
                var filterResolution = await ResolveFilterAsync(filter, topK);
                if (filterResolution is null)
                {
                    return CopyWithFailure(
                        metricApplicability,
                        $"Filter 语义“{filter.SemanticText}”无法解析为稳定的 Metadata 物理绑定。");
                }

                filterResolutions.Add(filterResolution);
            }
        }

        var dimensions = goldenCase.Expected?.Dimensions;
        var dimensionResolutions = new List<SemanticApplicabilityDimensionResolution>();
        if (dimensions is not null)
        {
            foreach (var dimension in dimensions)
            {
                var dimensionResolution = await ResolveDimensionAsync(dimension, topK);
                if (dimensionResolution is null)
                {
                    return CopyWithFailure(
                        metricApplicability,
                        $"Dimension 语义“{dimension.SemanticText}”无法解析为稳定的 Metadata 物理绑定。");
                }

                dimensionResolutions.Add(dimensionResolution);
            }
        }

        return new SemanticApplicabilityResult
        {
            CaseId = metricApplicability.CaseId,
            Question = metricApplicability.Question,
            MetricSemanticText = metricApplicability.MetricSemanticText,
            MetricType = metricApplicability.MetricType,
            State = metricApplicability.State,
            Reason = metricApplicability.Reason,
            SearchCandidate = metricApplicability.SearchCandidate,
            Resolution = metricApplicability.Resolution,
            FilterResolutions = filterResolutions,
            DimensionResolutions = dimensionResolutions,
            Evidence = metricApplicability.Evidence
        };
    }

    private async Task<SemanticApplicabilityFilterResolution?> ResolveFilterAsync(GoldenFilterExpectation filter, int topK)
    {
        if (string.IsNullOrWhiteSpace(filter.SemanticText))
            return null;

        var results = await _semanticSearchService.SearchAsync(filter.SemanticText, topK);
        var candidates = results
            .Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null)
            .GroupBy(GetCandidateBindingKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score)
            .ToList();

        var candidate = candidates.FirstOrDefault(x => ContainsSemanticText(x, filter.SemanticText));
        if (candidate?.Table is null || candidate.Column is null)
            return null;

        return new SemanticApplicabilityFilterResolution
        {
            TableId = candidate.Table.Id,
            DataSourceId = candidate.Table.DataSourceId,
            ColumnId = candidate.Column.Id,
            SemanticText = filter.SemanticText,
            Table = candidate.Table.TableName,
            Column = candidate.Column.ColumnName,
            BusinessMeaning = candidate.Semantic?.BusinessMeaning,
            Score = candidate.Score
        };
    }

    private async Task<SemanticApplicabilityDimensionResolution?> ResolveDimensionAsync(GoldenDimensionExpectation dimension, int topK)
    {
        if (string.IsNullOrWhiteSpace(dimension.SemanticText))
            return null;

        var results = await _semanticSearchService.SearchAsync(dimension.SemanticText, topK);
        var candidates = results
            .Where(x => x.IsSemanticVector && x.Table is not null && x.Column is not null)
            .GroupBy(GetCandidateBindingKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score)
            .ToList();

        var candidate = candidates.FirstOrDefault(x => ContainsSemanticText(x, dimension.SemanticText));
        if (candidate?.Table is null || candidate.Column is null)
            return null;

        return new SemanticApplicabilityDimensionResolution
        {
            TableId = candidate.Table.Id,
            DataSourceId = candidate.Table.DataSourceId,
            ColumnId = candidate.Column.Id,
            SemanticText = dimension.SemanticText,
            Table = candidate.Table.TableName,
            Column = candidate.Column.ColumnName,
            BusinessMeaning = candidate.Semantic?.BusinessMeaning,
            Score = candidate.Score
        };
    }

    private static SemanticApplicabilityResult CopyWithFailure(SemanticApplicabilityResult source, string reason)
    {
        return new SemanticApplicabilityResult
        {
            CaseId = source.CaseId,
            Question = source.Question,
            MetricSemanticText = source.MetricSemanticText,
            MetricType = source.MetricType,
            State = "NotResolved",
            Reason = reason,
            SearchCandidate = source.SearchCandidate,
            Resolution = source.Resolution,
            Evidence = source.Evidence
        };
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
        var competingCandidates = semanticCandidates.Skip(1).Any(x => ContainsSemanticText(x, semanticText));
        var state = topLexicalMatch ? competingCandidates ? "Ambiguous" : "Resolved" : "NotResolved";

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

        var topLexicalMatch = ContainsSemanticText(topSemantic, semanticText);
        var entitySemanticText = ExtractEntitySemanticText(semanticText);

        // EntityCount 首先解析“实体”本身，而不是把包含“入库单”的数量字段、外键字段当作实体候选。
        // 只有表级语义能够证明实体时，才允许进入 Resolved/Ambiguous。
        var tableEntityCandidates = !string.IsNullOrWhiteSpace(entitySemanticText)
            ? semanticCandidates
                .Where(x => x.Table is not null && ContainsTableEntitySemanticText(x, entitySemanticText))
                .GroupBy(x => x.Table!.Id)
                .Select(g => g.OrderByDescending(x => x.Score).First())
                .OrderByDescending(x => x.Score)
                .ToList()
            : new List<MetadataSemanticSearchResult>();

        // 精确实体匹配只允许 TableName / TableComment。
        // SearchText 是拼接后的表+字段全文，不能作为“精确实体”的依据，否则诸如“入库单明细”
        // 或包含“入库单ID”的字段描述会被错误提升为同级实体候选。
        var exactEntityCandidates = !string.IsNullOrWhiteSpace(entitySemanticText)
            ? tableEntityCandidates
                .Where(x => MatchesExactEntityTableSemanticText(x, entitySemanticText))
                .GroupBy(x => x.Table!.Id)
                .Select(g => g.OrderByDescending(x => x.Score).First())
                .OrderByDescending(x => x.Score)
                .ToList()
            : new List<MetadataSemanticSearchResult>();

        // 精确 TableName/TableComment 唯一时直接解析；只有多个真正的精确实体表才保持 Ambiguous。
        // 没有精确实体时，才退回表级包含匹配。
        var entityCandidatesForResolution = exactEntityCandidates.Count > 0
            ? exactEntityCandidates
            : tableEntityCandidates;

        var entityCandidate = entityCandidatesForResolution.FirstOrDefault();
        var competingCandidates = entityCandidatesForResolution.Count > 1;

        // EntityCount 不允许仅凭 metric field 的 lexical match 建立实体证据。
        // “入库单数量”中的“数量”可能命中 quantity 字段，但真正需要解析的是“入库单”这个实体。
        var directEvidence = entityCandidate is not null;
        if (directEvidence)
            topSemantic = entityCandidate;

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
            Reason = BuildEntityCountReason(state, metricCount, topLexicalMatch, entitySemanticText),
            SearchCandidate = ToCandidate(topSemantic),
            Resolution = state == "Resolved" ? ToResolution(topSemantic) : null,
            Evidence = new SemanticApplicabilityEvidence
            {
                SemanticCandidateExists = semanticCandidates.Count > 0,
                EntityCandidateExists = entityCandidates.Count > 0,
                DirectEntityCountEvidence = directEvidence,
                LexicalMatch = topLexicalMatch || !string.IsNullOrWhiteSpace(entityCandidate?.Table?.TableComment),
                CompetingCandidates = competingCandidates,
                TopScore = topSemantic.Score,
                SecondScore = secondSemantic?.Score,
                ScoreGap = scoreGap
            }
        };
    }

    private static string ExtractEntitySemanticText(string semanticText)
    {
        var normalized = NormalizeSemanticText(semanticText);
        foreach (var suffix in new[] { "数量", "个数", "总数", "数目" })
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return normalized[..^suffix.Length];
        }

        return string.Empty;
    }

    private static bool MatchesExactEntityTableSemanticText(MetadataSemanticSearchResult candidate, string entitySemanticText)
    {
        if (candidate.Table is null)
            return false;

        var normalizedEntity = NormalizeSemanticText(entitySemanticText);
        if (normalizedEntity.Length == 0)
            return false;

        // 精确实体只认可稳定的表名/表注释。
        // Table.SearchText 是全文索引文本，不具备实体边界，不能用于 exact match。
        var values = new[]
        {
            candidate.Table.TableComment,
            candidate.Table.TableName
        };

        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value)
            && NormalizeSemanticText(value).Equals(normalizedEntity, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsTableEntitySemanticText(MetadataSemanticSearchResult candidate, string entitySemanticText)
    {
        if (candidate.Table is null)
            return false;

        var normalizedEntity = NormalizeSemanticText(entitySemanticText);
        if (normalizedEntity.Length == 0)
            return false;

        var values = new[]
        {
            candidate.Table.TableComment,
            candidate.Table.TableName,
            candidate.Table.SearchText
        };

        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value)
            && NormalizeSemanticText(value).Contains(normalizedEntity, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsEntitySemanticText(MetadataSemanticSearchResult candidate, string entitySemanticText)
    {
        if (string.IsNullOrWhiteSpace(entitySemanticText))
            return false;

        var normalizedEntity = NormalizeSemanticText(entitySemanticText);
        if (normalizedEntity.Length == 0)
            return false;

        var values = new[]
        {
            candidate.Table?.TableName,
            candidate.Table?.TableComment,
            candidate.Table?.SearchText,
            candidate.Semantic?.BusinessMeaning,
            candidate.Semantic?.Keywords,
            candidate.Semantic?.Synonyms,
            candidate.Semantic?.ExampleQuestions,
            candidate.Semantic?.SearchText,
            candidate.Column?.ColumnName,
            candidate.Column?.ColumnComment
        };

        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value)
            && NormalizeSemanticText(value).Contains(normalizedEntity, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsSemanticText(MetadataSemanticSearchResult candidate, string semanticText)
    {
        var normalized = NormalizeSemanticText(semanticText);
        if (normalized.Length == 0)
            return false;

        var values = new[]
        {
            candidate.Table?.TableName,
            candidate.Table?.TableComment,
            candidate.Table?.SearchText,
            candidate.Column?.ColumnName,
            candidate.Column?.ColumnComment,
            candidate.Semantic?.BusinessMeaning,
            candidate.Semantic?.Keywords,
            candidate.Semantic?.Synonyms,
            candidate.Semantic?.ExampleQuestions,
            candidate.Semantic?.SearchText
        };

        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value)
            && NormalizeSemanticText(value).Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeSemanticText(string value)
    {
        return new string(value.Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-').ToArray());
    }

    private static string GetCandidateBindingKey(MetadataSemanticSearchResult candidate)
    {
        return $"{candidate.Table?.Id ?? 0}:{candidate.Column?.Id ?? 0}";
    }

    private static SemanticApplicabilityCandidate? ToCandidate(MetadataSemanticSearchResult? candidate)
    {
        if (candidate?.Table is null || candidate.Column is null)
            return null;

        return new SemanticApplicabilityCandidate
        {
            VectorType = candidate.VectorType,
            VectorId = candidate.VectorId,
            Score = candidate.Score,
            Table = candidate.Table.TableName,
            Column = candidate.Column.ColumnName,
            BusinessMeaning = candidate.Semantic?.BusinessMeaning
        };
    }

    private static SemanticApplicabilityResolution? ToResolution(MetadataSemanticSearchResult? candidate)
    {
        if (candidate?.Table is null || candidate.Column is null)
            return null;

        return new SemanticApplicabilityResolution
        {
            TableId = candidate.Table.Id,
            DataSourceId = candidate.Table.DataSourceId,
            ColumnId = candidate.Column.Id,
            Table = candidate.Table.TableName,
            Column = candidate.Column.ColumnName,
            BusinessMeaning = candidate.Semantic?.BusinessMeaning,
            Score = candidate.Score
        };
    }

    private static string BuildColumnReason(string state, bool lexicalMatch, int metricCount)
    {
        if (metricCount > 1)
            return $"Primary Metric semantic applicability={state}; Golden Case contains {metricCount} metrics and the primary metric was evaluated first.";

        return state switch
        {
            "Resolved" => "Direct semantic candidate matched the requested metric semantic text.",
            "Ambiguous" => "Multiple semantic candidates contain direct evidence for the Golden metric; Applicability remains Ambiguous.",
            _ when !lexicalMatch => "Top semantic candidate does not provide sufficient lexical evidence for the Golden metric.",
            _ => "No stable semantic applicability could be resolved."
        };
    }

    private static string BuildEntityCountReason(string state, int metricCount, bool topLexicalMatch, string entitySemanticText)
    {
        if (metricCount > 1)
            return $"Primary EntityCount semantic applicability={state}; Golden Case contains {metricCount} metrics and the primary metric was evaluated first.";

        return state switch
        {
            "Resolved" => string.IsNullOrWhiteSpace(entitySemanticText)
                ? "Direct EntityCount semantic evidence matched an existing table/column binding."
                : "Direct EntityCount semantic evidence matched a stable business-entity table binding.",
            "Ambiguous" => "Multiple table-level semantic candidates contain direct EntityCount evidence; Applicability remains Ambiguous.",
            _ when !topLexicalMatch => "Top semantic candidate does not provide sufficient evidence for the Golden metric.",
            _ => "No direct EntityCount semantic evidence could be resolved from the current semantic candidates."
        };
    }
}