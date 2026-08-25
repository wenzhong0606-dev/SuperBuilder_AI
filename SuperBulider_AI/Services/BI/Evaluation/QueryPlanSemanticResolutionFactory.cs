using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

public static class QueryPlanSemanticResolutionFactory
{
    public static QueryPlanSemanticResolution From(SemanticApplicabilityResult applicability)
    {
        ArgumentNullException.ThrowIfNull(applicability);
        if (!string.Equals(applicability.State, "Resolved", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"只有 State=Resolved 的 Semantic Applicability 才允许创建 QueryPlanSemanticResolution，当前状态为“{applicability.State}”。");

        var source = applicability.MetricResolutions.Count > 0
            ? applicability.MetricResolutions
            : applicability.Resolution is null ? Array.Empty<SemanticApplicabilityMetricResolution>() : new[] { new SemanticApplicabilityMetricResolution { TableId = applicability.Resolution.TableId, DataSourceId = applicability.Resolution.DataSourceId, ColumnId = applicability.Resolution.ColumnId, SemanticText = applicability.MetricSemanticText, Table = applicability.Resolution.Table, Column = applicability.Resolution.Column, BusinessMeaning = applicability.Resolution.BusinessMeaning, Score = applicability.Resolution.Score } };
        if (source.Count == 0) throw new InvalidOperationException("Semantic Applicability 已标记为 Resolved，但没有 Metric Resolution。");

        var metrics = source.Select(x => new QueryPlanMetricResolution { TableId = x.TableId, DataSourceId = x.DataSourceId, ColumnId = x.ColumnId, SemanticText = x.SemanticText, Table = x.Table ?? string.Empty, Column = x.Column ?? string.Empty, BusinessMeaning = x.BusinessMeaning, Score = x.Score }).ToList();
        foreach (var metric in metrics)
            if (metric.TableId <= 0 || metric.DataSourceId <= 0 || metric.ColumnId <= 0 || string.IsNullOrWhiteSpace(metric.Table) || string.IsNullOrWhiteSpace(metric.Column))
                throw new InvalidOperationException($"Metric Semantic Resolution 不完整：SemanticText={metric.SemanticText}。");

        var filters = applicability.FilterResolutions.Select(x => new QueryPlanFilterResolution { TableId = x.TableId, DataSourceId = x.DataSourceId, ColumnId = x.ColumnId, SemanticText = x.SemanticText, Table = x.Table ?? string.Empty, Column = x.Column ?? string.Empty, BusinessMeaning = x.BusinessMeaning, Score = x.Score }).ToList();
        var dimensions = applicability.DimensionResolutions.Select(x => new QueryPlanDimensionResolution { TableId = x.TableId, DataSourceId = x.DataSourceId, ColumnId = x.ColumnId, SemanticText = x.SemanticText, Table = x.Table ?? string.Empty, Column = x.Column ?? string.Empty, BusinessMeaning = x.BusinessMeaning, Score = x.Score }).ToList();
        var tables = applicability.TableResolutions.Select(x => new QueryPlanTableResolution { TableId = x.TableId, DataSourceId = x.DataSourceId, SemanticText = x.SemanticText, Table = x.Table ?? string.Empty, BusinessMeaning = x.BusinessMeaning, Score = x.Score }).ToList();

        return new QueryPlanSemanticResolution
        {
            Metrics = metrics,
            Filters = filters,
            Dimensions = dimensions,
            Tables = tables
        };
    }

    /// <summary>
    /// Ranking-aware resolution：仅在 QueryIntent 明确声明 Ranking + OrderBy 时创建 Order Resolution。
    /// 物理列必须复用已经确认的 Metric Resolution，禁止重新进行 Semantic Search。
    /// </summary>
    public static QueryPlanSemanticResolution From(
        SemanticApplicabilityResult applicability,
        QueryIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var resolution = From(applicability);

        if (!intent.IsRanking || string.IsNullOrWhiteSpace(intent.OrderBy))
            return resolution;

        var candidates = resolution.Metrics
            .Where(x =>
                string.Equals(x.SemanticText, intent.OrderBy, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Column, intent.OrderBy, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException($"Ranking Order 无法绑定到已确认的 Metric Resolution：OrderBy={intent.OrderBy}。");

        if (candidates.Count > 1)
            throw new InvalidOperationException($"Ranking Order 存在多个 Metric Resolution 候选，禁止猜测：OrderBy={intent.OrderBy}，Candidates={candidates.Count}。");

        var metric = candidates[0];
        return new QueryPlanSemanticResolution
        {
            Metrics = resolution.Metrics,
            Filters = resolution.Filters,
            Dimensions = resolution.Dimensions,
            Tables = resolution.Tables,
            Orders = new[]
            {
                new QueryPlanOrderResolution
                {
                    TableId = metric.TableId,
                    DataSourceId = metric.DataSourceId,
                    ColumnId = metric.ColumnId,
                    SemanticText = metric.SemanticText,
                    Table = metric.Table,
                    Column = metric.Column,
                    BusinessMeaning = metric.BusinessMeaning,
                    Score = metric.Score
                }
            }
        };
    }
}
