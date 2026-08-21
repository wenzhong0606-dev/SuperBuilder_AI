using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

public partial class QueryPlanBuilder
{
    public async Task<QueryPlan> BuildAsync(
        QueryIntent intent,
        QueryPlanSemanticResolution? resolution)
    {
        QueryPlan plan;

        if (resolution is not null
            && resolution.Dimensions.Count > 0
            && resolution.Metrics.Count > 0
            && resolution.Dimensions.Any(x => x.TableId > 0 && !resolution.Metrics.Any(m => m.TableId == x.TableId)))
        {
            var originalDimensions = intent.Dimensions;
            try
            {
                intent.Dimensions = new List<string>();
                plan = await BuildAsync(intent);
            }
            finally
            {
                intent.Dimensions = originalDimensions;
            }
        }
        else
        {
            plan = await BuildAsync(intent);
        }

        if (resolution is null)
            return plan;

        NormalizeToResolvedTables(plan, resolution);
        ApplyMetricResolutions(plan, resolution);
        ApplyFilterResolutions(plan, resolution.Filters);
        EnsureResolvedDimensions(plan, resolution.Dimensions);
        ApplyDimensionResolutions(plan, resolution.Dimensions);
        ApplyOrderResolutions(plan, resolution.Orders);
        await ApplyResolvedJoinInferenceAsync(plan, resolution);
        return plan;
    }

    private static void EnsureResolvedDimensions(QueryPlan plan, IReadOnlyList<QueryPlanDimensionResolution> bindings)
    {
        if (bindings.Count == 0) return;
        while (plan.Dimensions.Count < bindings.Count) plan.Dimensions.Add(new QueryDimension());
    }

    private static void NormalizeToResolvedTables(QueryPlan plan, QueryPlanSemanticResolution resolution)
    {
        var resolvedTableIds = new HashSet<long>();
        foreach (var metric in resolution.Metrics) resolvedTableIds.Add(metric.TableId);
        if (resolution.Metrics.Count == 0 && resolution.Metric is not null) resolvedTableIds.Add(resolution.Metric.TableId);
        foreach (var binding in resolution.Filters) resolvedTableIds.Add(binding.TableId);
        foreach (var binding in resolution.Dimensions) resolvedTableIds.Add(binding.TableId);
        foreach (var binding in resolution.Orders) resolvedTableIds.Add(binding.TableId);
        if (resolvedTableIds.Count == 0) return;
        plan.Joins.RemoveAll(join => !resolvedTableIds.Contains(join.LeftTableId) || !resolvedTableIds.Contains(join.RightTableId));
        plan.Tables.RemoveAll(table => !resolvedTableIds.Contains(table.MetadataTableId));
    }

    /// <summary>
    /// C.13：消费 Semantic Applicability 已完成的全部 Metric 物理绑定。
    /// 不再对第二个及后续 Metric 进行二次 Semantic Search / 猜测。
    /// </summary>
    private static void ApplyMetricResolutions(QueryPlan plan, QueryPlanSemanticResolution resolution)
    {
        var bindings = resolution.Metrics.Count > 0
            ? resolution.Metrics
            : resolution.Metric is null
                ? Array.Empty<QueryPlanMetricResolution>()
                : new[] { resolution.Metric };

        if (bindings.Count == 0)
            throw new InvalidOperationException("QueryPlan Semantic Binding Drift：没有可消费的 Metric Resolution。");

        if (plan.Metrics.Count != bindings.Count)
            throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Metric 数量不一致，Resolution={bindings.Count}，Runtime={plan.Metrics.Count}。");

        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            ValidateResolutionColumn(binding.ColumnId, binding.Column, $"Metric[{i}]");
            EnsureResolutionTable(plan, binding.TableId, binding.DataSourceId, binding.Table, $"Metric[{i}]");

            var runtimeMetric = plan.Metrics[i];
            runtimeMetric.Field = binding.Column;
            EnsureResolutionField(
                plan,
                binding.ColumnId,
                binding.Column,
                runtimeMetric.GetAggregation().ToString());
        }
    }

    private async Task ApplyResolvedJoinInferenceAsync(QueryPlan plan, QueryPlanSemanticResolution resolution)
    {
        if (resolution.Metrics.Count == 0 && resolution.Metric is null) return;
        if (resolution.Dimensions.Count == 0) return;

        var metricTableId = resolution.Metrics.Count > 0
            ? resolution.Metrics[0].TableId
            : resolution.Metric!.TableId;
        if (metricTableId <= 0) return;

        foreach (var dimension in resolution.Dimensions)
        {
            if (dimension.TableId <= 0 || dimension.TableId == metricTableId) continue;
            var searchTexts = new List<string>();
            if (!string.IsNullOrWhiteSpace(dimension.SemanticText)) searchTexts.Add(dimension.SemanticText);
            if (!string.IsNullOrWhiteSpace(dimension.SemanticText)) searchTexts.Add($"{dimension.SemanticText}ID");
            var metricTable = resolution.Metrics.Count > 0 ? resolution.Metrics[0].Table : resolution.Metric?.Table;
            if (!string.IsNullOrWhiteSpace(metricTable)) searchTexts.Add(metricTable);

            var metadataResults = new List<SuperBuilder_AI.Models.AI.MetadataSemanticSearchResult>();
            foreach (var searchText in searchTexts.Distinct(StringComparer.OrdinalIgnoreCase))
                metadataResults.AddRange(await _metadataSearch.SearchAsync(searchText, 20));

            metadataResults = metadataResults
                .Where(x => x.Table is not null && x.Column is not null)
                .GroupBy(x => $"{x.Table!.Id}|{x.Column!.Id}|{x.Semantic?.Id ?? 0}", StringComparer.OrdinalIgnoreCase)
                .Select(x => x.OrderByDescending(item => item.Score).First())
                .ToList();
            if (metadataResults.Count == 0) continue;

            var candidates = await _joinInference.InferAsync(metadataResults);
            var candidate = candidates
                .Where(x => (x.LeftTableId == metricTableId && x.RightTableId == dimension.TableId) || (x.LeftTableId == dimension.TableId && x.RightTableId == metricTableId))
                .OrderByDescending(x => x.Confidence)
                .FirstOrDefault();
            if (candidate is null || candidate.LeftTableId == candidate.RightTableId) continue;

            if (!plan.Tables.Any(x => x.MetadataTableId == candidate.LeftTableId) || !plan.Tables.Any(x => x.MetadataTableId == candidate.RightTableId)) continue;

            var exists = plan.Joins.Any(x =>
                (x.LeftTableId == candidate.LeftTableId && x.LeftColumnId == candidate.LeftColumnId && x.RightTableId == candidate.RightTableId && x.RightColumnId == candidate.RightColumnId)
                || (x.LeftTableId == candidate.RightTableId && x.LeftColumnId == candidate.RightColumnId && x.RightTableId == candidate.LeftTableId && x.RightColumnId == candidate.LeftColumnId));
            if (exists) continue;

            plan.Joins.Add(new QueryJoin
            {
                LeftTableId = candidate.LeftTableId,
                LeftColumnId = candidate.LeftColumnId,
                RightTableId = candidate.RightTableId,
                RightColumnId = candidate.RightColumnId,
                LeftTableName = candidate.LeftTableName,
                LeftColumnName = candidate.LeftColumnName,
                RightTableName = candidate.RightTableName,
                RightColumnName = candidate.RightColumnName,
                JoinType = "INNER"
            });
        }
    }

    private static void ApplyFilterResolutions(QueryPlan plan, IReadOnlyList<QueryPlanFilterResolution> bindings)
    {
        if (bindings.Count == 0) return;
        if (plan.Filters.Count != bindings.Count)
            throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Filter 数量不一致，Resolution={bindings.Count}，Runtime={plan.Filters.Count}。");
        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            ValidateResolutionColumn(binding.ColumnId, binding.Column, $"Filter[{i}]");
            EnsureResolutionTable(plan, binding.TableId, binding.DataSourceId, binding.Table, $"Filter[{i}]");
            plan.Filters[i].Field = binding.Column;
        }
    }

    private static void ApplyDimensionResolutions(QueryPlan plan, IReadOnlyList<QueryPlanDimensionResolution> bindings)
    {
        if (bindings.Count == 0) return;
        if (plan.Dimensions.Count != bindings.Count)
            throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Dimension 数量不一致，Resolution={bindings.Count}，Runtime={plan.Dimensions.Count}。");
        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            ValidateResolutionColumn(binding.ColumnId, binding.Column, $"Dimension[{i}]");
            EnsureResolutionTable(plan, binding.TableId, binding.DataSourceId, binding.Table, $"Dimension[{i}]");
            plan.Dimensions[i].MetadataColumnId = binding.ColumnId;
            plan.Dimensions[i].ColumnName = binding.Column;
        }
    }

    private static void ApplyOrderResolutions(QueryPlan plan, IReadOnlyList<QueryPlanOrderResolution> bindings)
    {
        if (bindings.Count == 0) return;
        if (plan.Orders.Count != bindings.Count)
            throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Order 数量不一致，Resolution={bindings.Count}，Runtime={plan.Orders.Count}。");
        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            ValidateResolutionColumn(binding.ColumnId, binding.Column, $"Order[{i}]");
            EnsureResolutionTable(plan, binding.TableId, binding.DataSourceId, binding.Table, $"Order[{i}]");
            plan.Orders[i].MetadataColumnId = binding.ColumnId;
            plan.Orders[i].Field = binding.Column;
        }
    }

    private static void EnsureResolutionTable(QueryPlan plan, long tableId, long dataSourceId, string tableName, string bindingType)
    {
        if (tableId <= 0 || dataSourceId <= 0 || string.IsNullOrWhiteSpace(tableName))
            throw new InvalidOperationException($"Semantic Resolution 缺少有效的 {bindingType} Table Binding。");
        var existing = plan.Tables.FirstOrDefault(table => table.MetadataTableId == tableId);
        if (existing is not null)
        {
            if (existing.DataSourceId != dataSourceId)
                throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：{bindingType} TableId={tableId} 的 DataSourceId 不一致，Resolution={dataSourceId}，Runtime={existing.DataSourceId}。");
            return;
        }
        plan.Tables.Add(new QueryTable { MetadataTableId = tableId, DataSourceId = dataSourceId, TableName = tableName });
    }

    private static void EnsureResolutionField(QueryPlan plan, long columnId, string columnName, string aggregation)
    {
        var existing = plan.Fields.FirstOrDefault(field => field.MetadataColumnId == columnId);
        if (existing is not null)
        {
            existing.ColumnName = columnName;
            if (string.IsNullOrWhiteSpace(existing.Aggregation) || existing.Aggregation.Equals("NONE", StringComparison.OrdinalIgnoreCase)) existing.Aggregation = aggregation;
            return;
        }
        plan.Fields.Add(new QueryField { MetadataColumnId = columnId, ColumnName = columnName, Aggregation = aggregation });
    }

    private static void ValidateResolutionColumn(long columnId, string column, string bindingType)
    {
        if (columnId <= 0 || string.IsNullOrWhiteSpace(column))
            throw new InvalidOperationException($"Semantic Resolution 缺少有效的 {bindingType} Column Binding。");
    }
}
