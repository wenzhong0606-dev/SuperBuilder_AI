using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Services.BI;

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

        if (string.IsNullOrWhiteSpace(binding.Column))
            throw new InvalidOperationException("Semantic Resolution 缺少 Metric Column。");

        if (binding.ColumnId <= 0)
            throw new InvalidOperationException("Semantic Resolution 缺少有效的 Metric ColumnId。");

        foreach (var metric in plan.Metrics)
        {
            if (!MatchesMetric(metric, binding.SemanticText))
                continue;

            metric.Field = binding.Column;
        }

        foreach (var field in plan.Fields)
        {
            if (field.MetadataColumnId == binding.ColumnId)
            {
                field.ColumnName = binding.Column;
                continue;
            }

            if (plan.Metrics.Any(metric =>
                    string.Equals(metric.Field, field.ColumnName, StringComparison.OrdinalIgnoreCase)))
            {
                field.MetadataColumnId = binding.ColumnId;
                field.ColumnName = binding.Column;
            }
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
