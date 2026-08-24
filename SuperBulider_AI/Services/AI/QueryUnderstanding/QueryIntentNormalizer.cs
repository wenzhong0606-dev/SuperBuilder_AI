using SuperBuilder_AI.Models.BI;

namespace SuperBulider_AI.Services.AI.QueryUnderstanding;

/// <summary>
/// QueryIntent 确定性规范化器。
/// 负责将自然语言理解阶段产生的 QueryIntent 转换为后续 QueryPlan 可以稳定消费的结构。
/// </summary>
public class QueryIntentNormalizer
{
    public QueryIntent Normalize(QueryIntent intent)
    {
        if (intent == null)
            throw new ArgumentNullException(nameof(intent));

        NormalizeCollection(intent);
        NormalizeLimit(intent);
        NormalizeRanking(intent);
        NormalizeMetricOnlyAggregate(intent);
        NormalizeAggregation(intent);
        NormalizeYearFilters(intent);

        return intent;
    }

    private static void NormalizeCollection(QueryIntent intent)
    {
        if (intent.Filters == null)
            intent.Filters = new List<QueryFilter>();

        if (intent.Metrics == null)
            intent.Metrics = new List<QueryMetric>();

        if (intent.Dimensions == null)
            intent.Dimensions = new List<string>();
    }

    private static void NormalizeLimit(QueryIntent intent)
    {
        if (intent.Limit < 0)
            intent.Limit = 0;
    }

    private static void NormalizeRanking(QueryIntent intent)
    {
        if (intent.Limit <= 0)
            return;

        if (intent.IntentType != "Ranking")
            return;

        if (intent.Metrics != null && intent.Metrics.Count > 0)
        {
            foreach (var metric in intent.Metrics)
            {
                if (string.IsNullOrWhiteSpace(metric.Aggregation))
                    metric.Aggregation = "COUNT";
            }
        }
    }

    /// <summary>
    /// 对“查询入库数量”这一类无维度、无排序、无Limit的指标查询进行确定性归一化。
    /// 
    /// Qwen 是 QueryIntent 的语义理解器，但 IntentType / Aggregation 是后续
    /// QueryPlan Evaluation 的 Contract 字段，不能因为模型偶发输出 Detail/NONE
    /// 就让同一个 Golden Case 在 Runtime 中漂移。
    /// 
    /// 仅处理明显的指标查询：
    /// - 没有维度
    /// - 没有排序
    /// - 没有 Limit
    /// - 至少存在一个 Metric
    /// - 问题没有要求“明细/记录/列表/哪些”等实体明细结果
    /// 
    /// Ranking / Detail Ranking 不在本规则范围内。
    /// </summary>
    private static void NormalizeMetricOnlyAggregate(QueryIntent intent)
    {
        if (intent.Metrics == null || intent.Metrics.Count == 0)
            return;

        if (string.Equals(intent.IntentType, "Ranking", StringComparison.OrdinalIgnoreCase))
            return;

        if (intent.Dimensions != null && intent.Dimensions.Count > 0)
            return;

        if (!string.IsNullOrWhiteSpace(intent.OrderBy) ||
            !string.IsNullOrWhiteSpace(intent.OrderDirection) ||
            intent.Limit.GetValueOrDefault() > 0)
            return;

        var question = intent.OriginalQuestion?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(question))
            return;

        if (ContainsDetailResultMarker(question))
            return;

        if (!ContainsMetricQueryMarker(question))
            return;

        intent.IntentType = "Aggregate";

        foreach (var metric in intent.Metrics)
        {
            if (string.IsNullOrWhiteSpace(metric.Aggregation) ||
                string.Equals(metric.Aggregation, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                metric.Aggregation = InferMetricAggregation(metric.Name);
            }
        }
    }

    private static bool ContainsDetailResultMarker(string question)
    {
        string[] markers =
        {
            "明细", "记录", "列表", "哪些", "哪几条", "具体记录", "详情"
        };

        return markers.Any(question.Contains);
    }

    private static bool ContainsMetricQueryMarker(string question)
    {
        string[] markers =
        {
            "数量", "金额", "金额", "总额", "销量", "销售额", "总数", "平均", "平均值", "最大", "最小", "最高", "最低"
        };

        return markers.Any(question.Contains);
    }

    private static string InferMetricAggregation(string? metricName)
    {
        var text = metricName?.Trim() ?? string.Empty;

        // “入库单数量 / 订单数量 / 供应商数量”等是实体数量，默认 COUNT。
        if (text.Contains("单数量", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("单据数量", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("订单数量", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("供应商数量", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("客户数量", StringComparison.OrdinalIgnoreCase))
        {
            return "COUNT";
        }

        // 普通业务数量/金额指标默认 SUM。
        if (text.Contains("数量", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("金额", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("总额", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("销量", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("销售额", StringComparison.OrdinalIgnoreCase))
        {
            return "SUM";
        }

        return "SUM";
    }

    private static void NormalizeAggregation(QueryIntent intent)
    {
        if (intent.Metrics == null)
            return;

        foreach (var metric in intent.Metrics)
        {
            if (string.IsNullOrWhiteSpace(metric.Aggregation))
                metric.Aggregation = "COUNT";
        }
    }

    /// <summary>
    /// 将“2025年”这类确定性的年度时间语义规范化为日期下界过滤。
    /// 例如：2025年入库数量 -> 入库日期 >= 2025-01-01。
    /// 不覆盖已有明确日期范围、比较符或具体日期的 Filter。
    /// </summary>
    private static void NormalizeYearFilters(QueryIntent intent)
    {
        if (intent.Filters == null || intent.Filters.Count == 0)
            return;

        foreach (var filter in intent.Filters)
        {
            if (filter == null)
                continue;

            var value = filter.Value?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{4}年?$"))
                continue;

            var yearText = value.TrimEnd('年');
            if (!int.TryParse(yearText, out var year) || year < 1900 || year > 9999)
                continue;

            // 仅规范化“年度”表达，不改变已经明确表达的操作符。
            if (!string.IsNullOrWhiteSpace(filter.Operator) &&
                !string.Equals(filter.Operator, "=", StringComparison.OrdinalIgnoreCase))
                continue;

            filter.Operator = ">=";
            filter.Value = $"{year:D4}-01-01";
        }
    }
}
