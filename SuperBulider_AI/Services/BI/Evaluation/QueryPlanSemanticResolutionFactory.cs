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
        return new QueryPlanSemanticResolution { Metrics = metrics, Filters = filters, Dimensions = dimensions, Tables = tables };
    }
}
