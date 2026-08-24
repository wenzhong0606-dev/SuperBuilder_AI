using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

public partial class QueryPlanBuilder
{
    /// <summary>
    /// Phase 2.6 C.13.2：只消费已确认的 Semantic Resolution。
    /// 本方法禁止对 Metric / Filter / Dimension 进行二次语义搜索。
    /// </summary>
    public Task<QueryPlan> BuildAsync(QueryIntent intent, QueryPlanSemanticResolution? resolution)
    {
        if (intent is null)
            throw new ArgumentNullException(nameof(intent));

        if (resolution is null)
            return BuildAsync(intent);

        // C.13.2 Contract：Resolution 是语义绑定的唯一真相源。
        // 这里仅根据 Intent 创建执行语义骨架，再由 Resolution 覆盖物理绑定；
        // 严禁调用 BuildAsync(intent)，避免再次执行 Metadata Semantic Search。
        var plan = BuildResolvedPlanSkeleton(intent, resolution);

        NormalizeToResolvedTables(plan, resolution);
        ApplyMetricResolutions(plan, resolution.Metrics);
        ApplyFilterResolutions(plan, resolution.Filters);
        ApplyDimensionResolutions(plan, resolution.Dimensions);
        ApplyTableResolutions(plan, resolution.Tables);
        ApplyOrderResolutions(plan, resolution.Orders);

        return Task.FromResult(plan);
    }

    private static QueryPlan BuildResolvedPlanSkeleton(
        QueryIntent intent,
        QueryPlanSemanticResolution resolution)
    {
        var plan = new QueryPlan
        {
            Intent = intent,
            IsAggregate = intent.IsAggregate,
            Limit = intent.Limit
        };

        foreach (var metric in intent.Metrics)
        {
            plan.Metrics.Add(new QueryMetric
            {
                Name = metric.Name,
                SemanticText = metric.SemanticText,
                Field = metric.Field,
                Aggregation = metric.Aggregation,
                Alias = metric.Alias,
                SemanticType = metric.SemanticType,
                IsOrderingMetric = metric.IsOrderingMetric
            });
        }

        foreach (var filter in intent.Filters)
        {
            plan.Filters.Add(new QueryFilter
            {
                SemanticText = filter.SemanticText,
                Field = filter.Field,
                Operator = filter.Operator,
                Value = filter.Value
            });
        }

        foreach (var dimension in intent.Dimensions)
        {
            plan.Dimensions.Add(new QueryDimension
            {
                SemanticText = dimension
            });
        }

        // Order 属于 Query Shape / Ranking Contract；这里仅在已有 Order Resolution
        // 时创建对应 Runtime 槽位，保持既有执行语义，不在 C.13.2 中重新推断 Ranking。
        foreach (var _ in resolution.Orders)
        {
            plan.Orders.Add(new QueryOrder
            {
                Direction = string.IsNullOrWhiteSpace(intent.OrderDirection)
                    ? "ASC"
                    : intent.OrderDirection!,
                IsMetric = true,
                MetricName = intent.OrderBy,
                Aggregation = ResolveOrderingAggregation(intent)
            });
        }

        return plan;
    }

    private static QueryAggregation ResolveOrderingAggregation(QueryIntent intent)
    {
        if (string.IsNullOrWhiteSpace(intent.OrderBy))
            return QueryAggregation.None;

        var metric = intent.Metrics.FirstOrDefault(x =>
            string.Equals(x.Name, intent.OrderBy, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.SemanticText, intent.OrderBy, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Field, intent.OrderBy, StringComparison.OrdinalIgnoreCase)
            || x.IsOrderingMetric);

        return metric?.GetAggregation() ?? QueryAggregation.None;
    }

    private static void NormalizeToResolvedTables(QueryPlan plan, QueryPlanSemanticResolution resolution)
    {
        var ids = new HashSet<long>(resolution.Tables.Select(x => x.TableId));
        foreach (var x in resolution.Metrics) ids.Add(x.TableId);
        foreach (var x in resolution.Filters) ids.Add(x.TableId);
        foreach (var x in resolution.Dimensions) ids.Add(x.TableId);
        foreach (var x in resolution.Orders) ids.Add(x.TableId);
        if (ids.Count == 0) return;
        plan.Tables.RemoveAll(x => x.MetadataTableId > 0 && !ids.Contains(x.MetadataTableId));
        plan.Joins.RemoveAll(x => !ids.Contains(x.LeftTableId) || !ids.Contains(x.RightTableId));
    }

    private static void ApplyMetricResolutions(QueryPlan plan, IReadOnlyList<QueryPlanMetricResolution> bindings)
    {
        if (bindings.Count == 0) return;
        if (plan.Metrics.Count != bindings.Count)
            throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Metric 数量不一致，Resolution={bindings.Count}，Runtime={plan.Metrics.Count}。");
        for (var i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            ValidateColumn(b.ColumnId, b.Column, $"Metric[{i}]");
            EnsureTable(plan, b.TableId, b.DataSourceId, b.Table, $"Metric[{i}]");
            plan.Metrics[i].SemanticText = b.SemanticText;
            plan.Metrics[i].Field = b.Column;
        }
    }

    private static void ApplyFilterResolutions(QueryPlan plan, IReadOnlyList<QueryPlanFilterResolution> bindings)
    {
        if (bindings.Count == 0) return;
        if (plan.Filters.Count != bindings.Count)
            throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Filter 数量不一致，Resolution={bindings.Count}，Runtime={plan.Filters.Count}。");
        for (var i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            ValidateColumn(b.ColumnId, b.Column, $"Filter[{i}]");
            EnsureTable(plan, b.TableId, b.DataSourceId, b.Table, $"Filter[{i}]");
            plan.Filters[i].SemanticText = b.SemanticText;
            plan.Filters[i].Field = b.Column;
        }
    }

    private static void ApplyDimensionResolutions(QueryPlan plan, IReadOnlyList<QueryPlanDimensionResolution> bindings)
    {
        if (bindings.Count == 0) return;
        if (plan.Dimensions.Count != bindings.Count)
            throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Dimension 数量不一致，Resolution={bindings.Count}，Runtime={plan.Dimensions.Count}。");
        for (var i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            ValidateColumn(b.ColumnId, b.Column, $"Dimension[{i}]");
            EnsureTable(plan, b.TableId, b.DataSourceId, b.Table, $"Dimension[{i}]");
            plan.Dimensions[i].SemanticText = b.SemanticText;
            plan.Dimensions[i].MetadataColumnId = b.ColumnId;
            plan.Dimensions[i].ColumnName = b.Column;
        }
    }

    private static void ApplyTableResolutions(QueryPlan plan, IReadOnlyList<QueryPlanTableResolution> bindings)
    {
        if (bindings.Count == 0) return;
        foreach (var b in bindings)
        {
            if (b.TableId <= 0 || b.DataSourceId <= 0 || string.IsNullOrWhiteSpace(b.Table))
                throw new InvalidOperationException($"Semantic Resolution 缺少有效的 Table Binding：{b.SemanticText}。");
            var table = plan.Tables.FirstOrDefault(x => x.MetadataTableId == b.TableId);
            if (table is null)
            {
                plan.Tables.Add(new QueryTable { MetadataTableId = b.TableId, DataSourceId = b.DataSourceId, SemanticText = b.SemanticText, TableName = b.Table });
                continue;
            }
            if (table.DataSourceId != b.DataSourceId) throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：TableId={b.TableId} DataSourceId 不一致。");
            table.SemanticText = b.SemanticText;
            table.TableName = b.Table;
        }
    }

    private static void ApplyOrderResolutions(QueryPlan plan, IReadOnlyList<QueryPlanOrderResolution> bindings)
    {
        if (bindings.Count == 0) return;
        if (plan.Orders.Count != bindings.Count) throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Order 数量不一致，Resolution={bindings.Count}，Runtime={plan.Orders.Count}。");
        for (var i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            ValidateColumn(b.ColumnId, b.Column, $"Order[{i}]");
            EnsureTable(plan, b.TableId, b.DataSourceId, b.Table, $"Order[{i}]");
            plan.Orders[i].MetadataColumnId = b.ColumnId;
            plan.Orders[i].Field = b.Column;
        }
    }

    private static void EnsureTable(QueryPlan plan, long tableId, long dataSourceId, string tableName, string bindingType)
    {
        if (tableId <= 0 || dataSourceId <= 0 || string.IsNullOrWhiteSpace(tableName)) throw new InvalidOperationException($"Semantic Resolution 缺少有效的 {bindingType} Table Binding。");
        var table = plan.Tables.FirstOrDefault(x => x.MetadataTableId == tableId);
        if (table is null) { plan.Tables.Add(new QueryTable { MetadataTableId = tableId, DataSourceId = dataSourceId, TableName = tableName }); return; }
        if (table.DataSourceId != dataSourceId) throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：{bindingType} TableId={tableId} 的 DataSourceId 不一致。");
    }

    private static void ValidateColumn(long columnId, string column, string bindingType)
    {
        if (columnId <= 0 || string.IsNullOrWhiteSpace(column)) throw new InvalidOperationException($"Semantic Resolution 缺少有效的 {bindingType} Column Binding。");
    }
}
