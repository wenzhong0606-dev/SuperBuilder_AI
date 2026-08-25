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

        var source = applicability.MetricResolutions.Count > 0 ? applicability.MetricResolutions : applicability.Resolution is null ? Array.Empty<SemanticApplicabilityMetricResolution>() : new[] { new SemanticApplicabilityMetricResolution { TableId = applicability.Resolution.TableId, DataSourceId = applicability.Resolution.DataSourceId, ColumnId = applicability.Resolution.ColumnId, SemanticText = applicability.MetricSemanticText, Table = applicability.Resolution.Table, Column = applicability.Resolution.Column, BusinessMeaning = applicability.Resolution.BusinessMeaning, Score = applicability.Resolution.Score } };
        if (source.Count == 0) throw new InvalidOperationException("Semantic Applicability 已标记为 Resolved，但没有 Metric Resolution。");

        var metrics = source.Select(x => new QueryPlanMetricResolution { TableId = x.TableId, DataSourceId = x.DataSourceId, ColumnId = x.ColumnId, SemanticText = x.SemanticText, Table = x.Table ?? string.Empty, Column = x.Column ?? string.Empty, BusinessMeaning = x.BusinessMeaning, Score = x.Score }).ToList();
        foreach (var metric in metrics)
            if (metric.TableId <= 0 || metric.DataSourceId <= 0 || metric.ColumnId <= 0 || string.IsNullOrWhiteSpace(metric.Table) || string.IsNullOrWhiteSpace(metric.Column)) throw new InvalidOperationException($"Metric Semantic Resolution 不完整：SemanticText={metric.SemanticText}。");

        var filters = applicability.FilterResolutions.Select(x => new QueryPlanFilterResolution { TableId = x.TableId, DataSourceId = x.DataSourceId, ColumnId = x.ColumnId, SemanticText = x.SemanticText, Table = x.Table ?? string.Empty, Column = x.Column ?? string.Empty, BusinessMeaning = x.BusinessMeaning, Score = x.Score }).ToList();

        var dimensions = applicability.DimensionResolutions.Select(x =>
        {
            if (string.IsNullOrWhiteSpace(x.ResolutionType) || string.Equals(x.ResolutionType, "NotResolved", StringComparison.OrdinalIgnoreCase) || !string.Equals(x.ExecutionCapability, "Executable", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Dimension Semantic Resolution 不可执行：SemanticText={x.SemanticText}，ResolutionType={x.ResolutionType}，ExecutionCapability={x.ExecutionCapability}。");
            if (x.TableId <= 0 || x.DataSourceId <= 0 || x.ColumnId <= 0 || string.IsNullOrWhiteSpace(x.Table) || string.IsNullOrWhiteSpace(x.Column)) throw new InvalidOperationException($"Dimension Semantic Resolution 物理绑定不完整：SemanticText={x.SemanticText}。");
            var isMasterJoin = string.Equals(x.ResolutionType, "MasterJoin", StringComparison.OrdinalIgnoreCase);
            var isDirectKey = string.Equals(x.ResolutionType, "DirectKey", StringComparison.OrdinalIgnoreCase);
            if (!isMasterJoin && !isDirectKey) throw new InvalidOperationException($"Dimension Semantic Resolution 类型非法：SemanticText={x.SemanticText}，ResolutionType={x.ResolutionType}。");
            if (!x.DimensionKeyColumnId.HasValue || string.IsNullOrWhiteSpace(x.DimensionKeyColumn)) throw new InvalidOperationException($"Dimension Semantic Resolution 缺少稳定 Dimension Key：SemanticText={x.SemanticText}。");
            if (isMasterJoin && (!x.DimensionLabelColumnId.HasValue || string.IsNullOrWhiteSpace(x.DimensionLabelColumn))) throw new InvalidOperationException($"MasterJoin Dimension Resolution 缺少 Master Label Column：SemanticText={x.SemanticText}。");
            if (isMasterJoin && (!x.MasterTableId.HasValue || !x.MasterDataSourceId.HasValue || !x.MasterKeyColumnId.HasValue || string.IsNullOrWhiteSpace(x.MasterTable) || string.IsNullOrWhiteSpace(x.MasterKeyColumn))) throw new InvalidOperationException($"MasterJoin Dimension Resolution 缺少完整 Master Binding：SemanticText={x.SemanticText}。");
            if (isDirectKey && (x.MasterTableId.HasValue || x.MasterKeyColumnId.HasValue || x.MasterDataSourceId.HasValue || !string.IsNullOrWhiteSpace(x.MasterTable) || !string.IsNullOrWhiteSpace(x.MasterKeyColumn) || x.DimensionLabelColumnId.HasValue || !string.IsNullOrWhiteSpace(x.DimensionLabelColumn))) throw new InvalidOperationException($"DirectKey Dimension Resolution 不应携带 Master Binding：SemanticText={x.SemanticText}。");

            return new QueryPlanDimensionResolution
            {
                TableId = x.TableId, DataSourceId = x.DataSourceId, ColumnId = x.ColumnId, SemanticText = x.SemanticText, Table = x.Table ?? string.Empty, Column = x.Column ?? string.Empty, BusinessMeaning = x.BusinessMeaning, Score = x.Score,
                ResolutionType = x.ResolutionType, ExecutionCapability = x.ExecutionCapability, DimensionKeyColumnId = x.DimensionKeyColumnId, DimensionKeyColumn = x.DimensionKeyColumn, DimensionLabelColumnId = x.DimensionLabelColumnId, DimensionLabelColumn = x.DimensionLabelColumn,
                MasterTableId = x.MasterTableId, MasterDataSourceId = x.MasterDataSourceId, MasterTable = x.MasterTable, MasterKeyColumnId = x.MasterKeyColumnId, MasterKeyColumn = x.MasterKeyColumn
            };
        }).ToList();

        var tables = applicability.TableResolutions.Select(x => new QueryPlanTableResolution { TableId = x.TableId, DataSourceId = x.DataSourceId, SemanticText = x.SemanticText, Table = x.Table ?? string.Empty, BusinessMeaning = x.BusinessMeaning, Score = x.Score }).ToList();
        return new QueryPlanSemanticResolution { Metrics = metrics, Filters = filters, Dimensions = dimensions, Tables = tables };
    }

    public static QueryPlanSemanticResolution From(SemanticApplicabilityResult applicability, QueryIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var resolution = From(applicability);
        if (!intent.IsRanking || string.IsNullOrWhiteSpace(intent.OrderBy)) return resolution;
        var candidates = resolution.Metrics.Where(x => string.Equals(x.SemanticText, intent.OrderBy, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Column, intent.OrderBy, StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidates.Count == 0) throw new InvalidOperationException($"Ranking Order 无法绑定到已确认的 Metric Resolution：OrderBy={intent.OrderBy}。");
        if (candidates.Count > 1) throw new InvalidOperationException($"Ranking Order 存在多个 Metric Resolution 候选，禁止猜测：OrderBy={intent.OrderBy}，Candidates={candidates.Count}。");
        var metric = candidates[0];
        return new QueryPlanSemanticResolution { Metrics = resolution.Metrics, Filters = resolution.Filters, Dimensions = resolution.Dimensions, Tables = resolution.Tables, Orders = new[] { new QueryPlanOrderResolution { TableId = metric.TableId, DataSourceId = metric.DataSourceId, ColumnId = metric.ColumnId, SemanticText = metric.SemanticText, Table = metric.Table, Column = metric.Column, BusinessMeaning = metric.BusinessMeaning, Score = metric.Score } } };
    }
}
