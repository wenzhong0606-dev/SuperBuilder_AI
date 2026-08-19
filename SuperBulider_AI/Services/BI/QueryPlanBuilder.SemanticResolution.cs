using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

public partial class QueryPlanBuilder
{
    /// <summary>
    /// 使用已经完成 Semantic Applicability Resolution 的绑定构建 QueryPlan。
    /// Resolution 中已经确认的字段只允许直接绑定，不再通过 ResolveColumn 二次猜测。
    /// </summary>
    public async Task<QueryPlan> BuildAsync(
        QueryIntent intent,
        QueryPlanSemanticResolution? resolution)
    {
        var plan = await BuildAsync(intent);

        if (resolution is null)
            return plan;

        NormalizeToResolvedTables(plan, resolution);

        ApplyMetricResolution(plan, resolution.Metric);
        ApplyFilterResolutions(plan, resolution.Filters);
        ApplyDimensionResolutions(plan, resolution.Dimensions);
        ApplyOrderResolutions(plan, resolution.Orders);

        return plan;
    }

    /// <summary>
    /// Resolution 已经确认了本次查询需要的物理表。
    /// Builder 原始流程中的 JoinInference 属于候选推断，不能在已有稳定 Resolution 时
    /// 把未经语义绑定确认的表继续带入最终 QueryPlan。
    ///
    /// 当前阶段规则：
    /// 1. 收集 Metric / Filter / Dimension / Order 已确认的 TableId。
    /// 2. 删除未被 Resolution 引用的推测性 Table。
    /// 3. 删除任一端不在已确认 Table 集合中的推测性 Join。
    /// 4. 不主动创建 Join；合法多表 Join 应由后续 Join Resolution 明确提供。
    /// </summary>
    private static void NormalizeToResolvedTables(
        QueryPlan plan,
        QueryPlanSemanticResolution resolution)
    {
        var resolvedTableIds = new HashSet<long>();

        if (resolution.Metric is not null)
            resolvedTableIds.Add(resolution.Metric.TableId);

        foreach (var binding in resolution.Filters)
            resolvedTableIds.Add(binding.TableId);

        foreach (var binding in resolution.Dimensions)
            resolvedTableIds.Add(binding.TableId);

        foreach (var binding in resolution.Orders)
            resolvedTableIds.Add(binding.TableId);

        if (resolvedTableIds.Count == 0)
            return;

        plan.Joins.RemoveAll(join =>
            !resolvedTableIds.Contains(join.LeftTableId)
            || !resolvedTableIds.Contains(join.RightTableId));

        plan.Tables.RemoveAll(table =>
            !resolvedTableIds.Contains(table.MetadataTableId));
    }

    private static void ApplyMetricResolution(
        QueryPlan plan,
        QueryPlanMetricResolution? binding)
    {
        if (binding is null)
            return;

        ValidateResolutionColumn(binding.ColumnId, binding.Column, "Metric");

        var matchedMetric = false;

        foreach (var metric in plan.Metrics)
        {
            if (!MatchesMetric(metric, binding.SemanticText))
                continue;

            matchedMetric = true;
            metric.Field = binding.Column;

            EnsureResolutionTable(plan, binding.TableId, binding.DataSourceId, binding.Table, "Metric");
            EnsureResolutionField(plan, binding.ColumnId, binding.Column, metric.GetAggregation().ToString());
        }

        if (!matchedMetric)
        {
            throw new InvalidOperationException(
                $"QueryPlan Semantic Binding Drift：未找到与已解析 Metric“{binding.SemanticText}”对应的 Runtime Metric。");
        }
    }

    private static void ApplyFilterResolutions(
        QueryPlan plan,
        IReadOnlyList<QueryPlanFilterResolution> bindings)
    {
        if (bindings.Count == 0)
            return;

        if (plan.Filters.Count != bindings.Count)
        {
            throw new InvalidOperationException(
                $"QueryPlan Semantic Binding Drift：Filter 数量不一致，Resolution={bindings.Count}，Runtime={plan.Filters.Count}。");
        }

        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            ValidateResolutionColumn(binding.ColumnId, binding.Column, $"Filter[{i}]");
            EnsureResolutionTable(plan, binding.TableId, binding.DataSourceId, binding.Table, $"Filter[{i}]");
            plan.Filters[i].Field = binding.Column;
        }
    }

    private static void ApplyDimensionResolutions(
        QueryPlan plan,
        IReadOnlyList<QueryPlanDimensionResolution> bindings)
    {
        if (bindings.Count == 0)
            return;

        if (plan.Dimensions.Count != bindings.Count)
        {
            throw new InvalidOperationException(
                $"QueryPlan Semantic Binding Drift：Dimension 数量不一致，Resolution={bindings.Count}，Runtime={plan.Dimensions.Count}。");
        }

        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            ValidateResolutionColumn(binding.ColumnId, binding.Column, $"Dimension[{i}]");
            EnsureResolutionTable(plan, binding.TableId, binding.DataSourceId, binding.Table, $"Dimension[{i}]");

            plan.Dimensions[i].MetadataColumnId = binding.ColumnId;
            plan.Dimensions[i].ColumnName = binding.Column;
        }
    }

    private static void ApplyOrderResolutions(
        QueryPlan plan,
        IReadOnlyList<QueryPlanOrderResolution> bindings)
    {
        if (bindings.Count == 0)
            return;

        if (plan.Orders.Count != bindings.Count)
        {
            throw new InvalidOperationException(
                $"QueryPlan Semantic Binding Drift：Order 数量不一致，Resolution={bindings.Count}，Runtime={plan.Orders.Count}。");
        }

        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            ValidateResolutionColumn(binding.ColumnId, binding.Column, $"Order[{i}]");
            EnsureResolutionTable(plan, binding.TableId, binding.DataSourceId, binding.Table, $"Order[{i}]");

            plan.Orders[i].MetadataColumnId = binding.ColumnId;
            plan.Orders[i].Field = binding.Column;
        }
    }

    /// <summary>
    /// Resolution 是语义层已经确认的物理绑定。
    /// Runtime QueryPlan 如果尚未包含该表，则补入该解析表，而不是再次猜测或错误阻断。
    /// 如果已存在同名 TableId，则验证数据源一致性。
    /// </summary>
    private static void EnsureResolutionTable(
        QueryPlan plan,
        long tableId,
        long dataSourceId,
        string tableName,
        string bindingType)
    {
        if (tableId <= 0 || dataSourceId <= 0 || string.IsNullOrWhiteSpace(tableName))
        {
            throw new InvalidOperationException(
                $"Semantic Resolution 缺少有效的 {bindingType} Table Binding。");
        }

        var existing = plan.Tables.FirstOrDefault(table => table.MetadataTableId == tableId);
        if (existing is not null)
        {
            if (existing.DataSourceId != dataSourceId)
            {
                throw new InvalidOperationException(
                    $"QueryPlan Semantic Binding Drift：{bindingType} TableId={tableId} 的 DataSourceId 不一致，Resolution={dataSourceId}，Runtime={existing.DataSourceId}。");
            }

            return;
        }

        plan.Tables.Add(new QueryTable
        {
            MetadataTableId = tableId,
            DataSourceId = dataSourceId,
            TableName = tableName
        });
    }

    private static void EnsureResolutionField(
        QueryPlan plan,
        long columnId,
        string columnName,
        string aggregation)
    {
        var existing = plan.Fields.FirstOrDefault(field => field.MetadataColumnId == columnId);
        if (existing is not null)
        {
            existing.ColumnName = columnName;
            if (string.IsNullOrWhiteSpace(existing.Aggregation)
                || existing.Aggregation.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            {
                existing.Aggregation = aggregation;
            }

            return;
        }

        plan.Fields.Add(new QueryField
        {
            MetadataColumnId = columnId,
            ColumnName = columnName,
            Aggregation = aggregation
        });
    }

    private static void ValidateResolutionColumn(
        long columnId,
        string column,
        string bindingType)
    {
        if (columnId <= 0 || string.IsNullOrWhiteSpace(column))
        {
            throw new InvalidOperationException(
                $"Semantic Resolution 缺少有效的 {bindingType} Column Binding。");
        }
    }

    private static bool MatchesMetric(QueryMetric metric, string semanticText)
    {
        if (string.IsNullOrWhiteSpace(semanticText))
            return true;

        return string.Equals(metric.Name, semanticText, StringComparison.OrdinalIgnoreCase)
               || string.Equals(metric.Field, semanticText, StringComparison.OrdinalIgnoreCase)
               || string.Equals(metric.SemanticType, semanticText, StringComparison.OrdinalIgnoreCase);
    }
}
