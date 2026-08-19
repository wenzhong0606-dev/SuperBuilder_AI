using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

public partial class QueryPlanBuilder
{
    /// <summary>
    /// 使用已经完成 Semantic Applicability Resolution 的 Metric Binding 构建 QueryPlan。
    /// 第一版只处理 Metric，后续扩展 Filter / Dimension / Order。
    /// </summary>
    public async Task<QueryPlan> BuildAsync(
        QueryIntent intent,
        QueryPlanSemanticResolution? resolution)
    {
        var plan = await BuildAsync(intent);

        var binding = resolution?.Metric;
        if (binding is null)
            return plan;

        if (binding.TableId <= 0)
            throw new InvalidOperationException("Semantic Resolution 缺少有效的 Metric TableId。");

        if (binding.ColumnId <= 0 || string.IsNullOrWhiteSpace(binding.Column))
            throw new InvalidOperationException("Semantic Resolution 缺少有效的 Metric Column Binding。");

        if (!plan.Tables.Any(table => table.MetadataTableId == binding.TableId))
        {
            throw new InvalidOperationException(
                $"QueryPlan Semantic Binding Drift：期望 TableId={binding.TableId}，但 Runtime QueryPlan 未绑定该表。");
        }

        var matchedMetric = false;
        var matchedField = false;

        foreach (var metric in plan.Metrics)
        {
            if (!MatchesMetric(metric, binding.SemanticText))
                continue;

            matchedMetric = true;
            var previousField = metric.Field;
            metric.Field = binding.Column;

            var matchingFields = plan.Fields
                .Where(field =>
                    string.Equals(field.ColumnName, previousField, StringComparison.OrdinalIgnoreCase)
                    || field.MetadataColumnId == binding.ColumnId)
                .ToList();

            foreach (var field in matchingFields)
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

        return plan;
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
