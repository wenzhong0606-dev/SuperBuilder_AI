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

        ApplyMetricResolution(plan, resolution.Metric);
        ApplyFilterResolutions(plan, resolution.Filters);
        ApplyDimensionResolutions(plan, resolution.Dimensions);
        ApplyOrderResolutions(plan, resolution.Orders);

        return plan;
    }

    private static void ApplyMetricResolution(
        QueryPlan plan,
        QueryPlanMetricResolution? binding)
    {
        if (binding is null)
            return;

        ValidateResolutionTable(plan, binding.TableId, "Metric");
        ValidateResolutionColumn(binding.ColumnId, binding.Column, "Metric");

        var matchedMetric = false;
        var matchedField = false;

        foreach (var metric in plan.Metrics)
        {
            if (!MatchesMetric(metric, binding.SemanticText))
                continue;

            matchedMetric = true;
            var previousField = metric.Field;
            metric.Field = binding.Column;

            foreach (var field in plan.Fields.Where(field =>
                         string.Equals(field.ColumnName, previousField, StringComparison.OrdinalIgnoreCase)
                         || field.MetadataColumnId == binding.ColumnId))
            {
                matchedField = true;
                field.MetadataColumnId = binding.ColumnId;
                field.ColumnName = binding.Column;
            }
        }

        if (!matchedMetric)
        {
            throw new InvalidOperationException(
                $"QueryPlan Semantic Binding Drift：未找到与已解析 Metric“{binding.SemanticText}”对应的 Runtime Metric。");
        }

        if (!matchedField)
        {
            throw new InvalidOperationException(
                $"QueryPlan Semantic Binding Drift：Runtime QueryPlan 未找到 Metric Field Binding，期望 ColumnId={binding.ColumnId}。");
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
            ValidateResolutionTable(plan, binding.TableId, $"Filter[{i}]");
            ValidateResolutionColumn(binding.ColumnId, binding.Column, $"Filter[{i}]");

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
            ValidateResolutionTable(plan, binding.TableId, $"Dimension[{i}]");
            ValidateResolutionColumn(binding.ColumnId, binding.Column, $"Dimension[{i}]");

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
            ValidateResolutionTable(plan, binding.TableId, $"Order[{i}]");
            ValidateResolutionColumn(binding.ColumnId, binding.Column, $"Order[{i}]");

            plan.Orders[i].MetadataColumnId = binding.ColumnId;
            plan.Orders[i].Field = binding.Column;
        }
    }

    private static void ValidateResolutionTable(
        QueryPlan plan,
        long tableId,
        string bindingType)
    {
        if (tableId <= 0)
        {
            throw new InvalidOperationException(
                $"Semantic Resolution 缺少有效的 {bindingType} TableId。");
        }

        if (!plan.Tables.Any(table => table.MetadataTableId == tableId))
        {
            throw new InvalidOperationException(
                $"QueryPlan Semantic Binding Drift：{bindingType} 期望 TableId={tableId}，但 Runtime QueryPlan 未绑定该表。");
        }
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
