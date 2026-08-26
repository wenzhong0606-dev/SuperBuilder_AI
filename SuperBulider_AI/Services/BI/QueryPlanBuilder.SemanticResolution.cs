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
        if (intent is null) throw new ArgumentNullException(nameof(intent));
        if (resolution is null) return BuildAsync(intent);
        var plan = BuildResolvedPlanSkeleton(intent, resolution);
        NormalizeToResolvedTables(plan, resolution);
        ApplyMetricResolutions(plan, resolution.Metrics);
        ApplyFilterResolutions(plan, resolution.Filters);
        ApplyDimensionResolutions(plan, resolution.Dimensions);
        ApplyTableResolutions(plan, resolution.Tables);
        ApplyOrderResolutions(plan, resolution.Orders);
        return Task.FromResult(plan);
    }

    private static QueryPlan BuildResolvedPlanSkeleton(QueryIntent intent, QueryPlanSemanticResolution resolution)
    {
        var plan = new QueryPlan { Intent = intent, IsAggregate = intent.IsAggregate, Limit = intent.Limit };
        foreach (var metric in intent.Metrics)
            plan.Metrics.Add(new QueryMetric { Name = metric.Name, SemanticText = metric.SemanticText, Field = metric.Field, Aggregation = metric.Aggregation, Alias = metric.Alias, SemanticType = metric.SemanticType, IsOrderingMetric = metric.IsOrderingMetric });
        foreach (var filter in intent.Filters)
            plan.Filters.Add(new QueryFilter { SemanticText = filter.SemanticText, Field = filter.Field, Operator = filter.Operator, Value = filter.Value });
        foreach (var dimension in intent.Dimensions)
            plan.Dimensions.Add(new QueryDimension { SemanticText = dimension });
        var orderingMetric = ResolveOrderingMetric(intent);
        foreach (var _ in resolution.Orders)
            plan.Orders.Add(new QueryOrder
            {
                Direction = string.IsNullOrWhiteSpace(intent.OrderDirection) ? "ASC" : intent.OrderDirection,
                IsMetric = true,
                // OrderBy may be a physical Field (for example "quantity").
                // QueryOrder.MetricName is the runtime semantic contract consumed by Evaluation.
                // Preserve the metric SemanticText here instead of leaking the physical OrderBy token.
                MetricName = orderingMetric?.SemanticText ?? orderingMetric?.Name ?? intent.OrderBy,
                Aggregation = ResolveOrderingAggregation(intent)
            });
        return plan;
    }

    private static QueryMetric? ResolveOrderingMetric(QueryIntent intent)
    {
        if (string.IsNullOrWhiteSpace(intent.OrderBy))
            return intent.Metrics.FirstOrDefault(x => x.IsOrderingMetric);

        return intent.Metrics.FirstOrDefault(x =>
            string.Equals(x.Name, intent.OrderBy, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.SemanticText, intent.OrderBy, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Field, intent.OrderBy, StringComparison.OrdinalIgnoreCase) ||
            x.IsOrderingMetric);
    }

    private static QueryAggregation ResolveOrderingAggregation(QueryIntent intent)
    {
        var metric = ResolveOrderingMetric(intent);
        return metric?.GetAggregation() ?? QueryAggregation.None;
    }

    private static void NormalizeToResolvedTables(QueryPlan plan, QueryPlanSemanticResolution resolution)
    {
        var ids = new HashSet<long>(resolution.Tables.Select(x => x.TableId));
        foreach (var x in resolution.Metrics) ids.Add(x.TableId);
        foreach (var x in resolution.Filters) ids.Add(x.TableId);
        foreach (var x in resolution.Dimensions) ids.Add(x.TableId);
        foreach (var x in resolution.Orders) ids.Add(x.TableId);
        foreach (var x in resolution.Dimensions.Where(x => x.MasterTableId.HasValue)) ids.Add(x.MasterTableId!.Value);
        if (ids.Count == 0) return;
        plan.Tables.RemoveAll(x => x.MetadataTableId > 0 && !ids.Contains(x.MetadataTableId));
        plan.Joins.RemoveAll(x => !ids.Contains(x.LeftTableId) || !ids.Contains(x.RightTableId));
    }

    private static void ApplyMetricResolutions(QueryPlan plan, IReadOnlyList<QueryPlanMetricResolution> bindings)
    {
        if (bindings.Count == 0) return;
        if (plan.Metrics.Count != bindings.Count) throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Metric 数量不一致，Resolution={bindings.Count}，Runtime={plan.Metrics.Count}。");
        for (var i = 0; i < bindings.Count; i++) { var b = bindings[i]; ValidateColumn(b.ColumnId, b.Column, $"Metric[{i}]"); EnsureTable(plan, b.TableId, b.DataSourceId, b.Table, $"Metric[{i}]"); plan.Metrics[i].SemanticText = b.SemanticText; plan.Metrics[i].Field = b.Column; }
    }

    private static void ApplyFilterResolutions(QueryPlan plan, IReadOnlyList<QueryPlanFilterResolution> bindings)
    {
        if (bindings.Count == 0) return;
        if (plan.Filters.Count != bindings.Count) throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Filter 数量不一致，Resolution={bindings.Count}，Runtime={plan.Filters.Count}。");
        for (var i = 0; i < bindings.Count; i++) { var b = bindings[i]; ValidateColumn(b.ColumnId, b.Column, $"Filter[{i}]"); EnsureTable(plan, b.TableId, b.DataSourceId, b.Table, $"Filter[{i}]"); plan.Filters[i].SemanticText = b.SemanticText; plan.Filters[i].Field = b.Column; }
    }

    private static void ApplyDimensionResolutions(QueryPlan plan, IReadOnlyList<QueryPlanDimensionResolution> bindings)
    {
        if (bindings.Count == 0) return;
        if (plan.Dimensions.Count != bindings.Count) throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Dimension 数量不一致，Resolution={bindings.Count}，Runtime={plan.Dimensions.Count}。");
        for (var i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            ValidateColumn(b.ColumnId, b.Column, $"Dimension[{i}]");
            EnsureTable(plan, b.TableId, b.DataSourceId, b.Table, $"Dimension[{i}]");
            if (!string.Equals(b.ExecutionCapability, "Executable", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Dimension[{i}] 不可执行：ExecutionCapability={b.ExecutionCapability}。");
            var isMasterJoin = string.Equals(b.ResolutionType, "MasterJoin", StringComparison.OrdinalIgnoreCase);
            var isDirectKey = string.Equals(b.ResolutionType, "DirectKey", StringComparison.OrdinalIgnoreCase);
            if (!isMasterJoin && !isDirectKey) throw new InvalidOperationException($"Dimension[{i}] ResolutionType 非法：{b.ResolutionType}。");
            if (!b.DimensionKeyColumnId.HasValue || string.IsNullOrWhiteSpace(b.DimensionKeyColumn)) throw new InvalidOperationException($"Dimension[{i}] 缺少 Fact DimensionKey。");

            var runtime = plan.Dimensions[i];
            runtime.SemanticText = b.SemanticText;
            runtime.ResolutionType = b.ResolutionType;
            runtime.ResolutionState = "Resolved";
            runtime.ExecutionCapability = b.ExecutionCapability;
            runtime.DimensionKeyColumnId = b.DimensionKeyColumnId;
            runtime.DimensionKeyColumnName = b.DimensionKeyColumn;

            if (isDirectKey)
            {
                if (b.MasterTableId.HasValue || b.MasterKeyColumnId.HasValue || b.DimensionLabelColumnId.HasValue && !string.IsNullOrWhiteSpace(b.DimensionLabelColumn))
                    throw new InvalidOperationException($"DirectKey Dimension 不应携带 Master Binding：SemanticText={b.SemanticText}。");
                runtime.MetadataColumnId = b.DimensionKeyColumnId.Value;
                runtime.ColumnName = b.DimensionKeyColumn!;
                runtime.DimensionLabelColumnId = null;
                runtime.DimensionLabelColumnName = null;
                continue;
            }

            if (!b.MasterTableId.HasValue || !b.MasterDataSourceId.HasValue || string.IsNullOrWhiteSpace(b.MasterTable) || !b.MasterKeyColumnId.HasValue || string.IsNullOrWhiteSpace(b.MasterKeyColumn))
                throw new InvalidOperationException($"MasterJoin Dimension 缺少完整 Master Binding：SemanticText={b.SemanticText}。");
            if (!b.DimensionLabelColumnId.HasValue || string.IsNullOrWhiteSpace(b.DimensionLabelColumn))
                throw new InvalidOperationException($"MasterJoin Dimension 缺少 Label Column：SemanticText={b.SemanticText}。");
            if (b.MasterDataSourceId.Value != b.DataSourceId)
                throw new InvalidOperationException($"MasterJoin Dimension 跨 DataSource，当前 QueryPlan 不允许执行：SemanticText={b.SemanticText}。");

            EnsureTable(plan, b.MasterTableId.Value, b.MasterDataSourceId.Value, b.MasterTable!, $"Dimension[{i}].Master");
            runtime.MetadataColumnId = b.DimensionLabelColumnId.Value;
            runtime.ColumnName = b.DimensionLabelColumn!;
            runtime.DimensionLabelColumnId = b.DimensionLabelColumnId;
            runtime.DimensionLabelColumnName = b.DimensionLabelColumn;

            var duplicate = plan.Joins.Any(j => j.LeftTableId == b.TableId && j.LeftColumnId == b.DimensionKeyColumnId.Value && j.RightTableId == b.MasterTableId.Value && j.RightColumnId == b.MasterKeyColumnId.Value);
            if (!duplicate)
                plan.Joins.Add(new QueryJoin { LeftTableId = b.TableId, LeftColumnId = b.DimensionKeyColumnId.Value, LeftTableName = b.Table, LeftColumnName = b.DimensionKeyColumn, RightTableId = b.MasterTableId.Value, RightColumnId = b.MasterKeyColumnId.Value, RightTableName = b.MasterTable, RightColumnName = b.MasterKeyColumn, JoinType = "INNER" });
        }
    }

    private static void ApplyTableResolutions(QueryPlan plan, IReadOnlyList<QueryPlanTableResolution> bindings)
    {
        if (bindings.Count == 0) return;
        foreach (var b in bindings)
        {
            if (b.TableId <= 0 || b.DataSourceId <= 0 || string.IsNullOrWhiteSpace(b.Table)) throw new InvalidOperationException($"Semantic Resolution 缺少有效的 Table Binding：{b.SemanticText}。");
            var table = plan.Tables.FirstOrDefault(x => x.MetadataTableId == b.TableId);
            if (table is null) { plan.Tables.Add(new QueryTable { MetadataTableId = b.TableId, DataSourceId = b.DataSourceId, SemanticText = b.SemanticText, TableName = b.Table }); continue; }
            if (table.DataSourceId != b.DataSourceId) throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：TableId={b.TableId} DataSourceId 不一致。");
            table.SemanticText = b.SemanticText; table.TableName = b.Table;
        }
    }

    private static void ApplyOrderResolutions(QueryPlan plan, IReadOnlyList<QueryPlanOrderResolution> bindings)
    {
        if (bindings.Count == 0) return;
        if (plan.Orders.Count != bindings.Count) throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Order 数量不一致，Resolution={bindings.Count}，Runtime={plan.Orders.Count}。");
        for (var i = 0; i < bindings.Count; i++) { var b = bindings[i]; ValidateColumn(b.ColumnId, b.Column, $"Order[{i}]"); EnsureTable(plan, b.TableId, b.DataSourceId, b.Table, $"Order[{i}]"); plan.Orders[i].MetadataColumnId = b.ColumnId; plan.Orders[i].Field = b.Column; }
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
