using System.Text.RegularExpressions;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI.Planning;

/// <summary>
/// QueryIntent 确定性规范化器。
/// LLM负责理解自然语言；Normalizer负责修正明显的结构性错误，并保留业务 SemanticText。
/// </summary>
public sealed class QueryIntentNormalizer
{
    private static readonly string[] DescWords = { "最多", "最大", "最大的", "最高", "最高的", "数量最多", "金额最多", "销量最多", "库存最多", "库存数量最多", "Top", "top", "排名前" };
    private static readonly string[] AscWords = { "最少", "最小", "最小的", "最低", "最低的", "数量最少", "金额最少", "销量最少", "库存最少", "库存数量最少" };
    private static readonly string[] TimeDescWords = { "最近", "最新", "最近创建", "最新创建", "最近新增", "最新新增" };
    private static readonly string[] DetailWords = { "明细", "列表", "记录", "每条", "逐条", "原始数据", "详情" };

    public QueryIntent Normalize(QueryIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var question = intent.OriginalQuestion ?? string.Empty;
        NormalizeCollections(intent);
        NormalizeLimit(intent, question);
        NormalizeRanking(intent, question);
        NormalizeAggregation(intent);
        NormalizeMetricFields(intent);
        NormalizeQuantityAggregationSemantics(intent, question);
        NormalizeSemanticTexts(intent);
        NormalizeYearFilters(intent);
        return intent;
    }

    private static void NormalizeCollections(QueryIntent intent)
    {
        intent.Metrics ??= new List<QueryMetric>();
        intent.Filters ??= new List<QueryFilter>();
        intent.Dimensions ??= new List<string>();
        intent.Metrics = intent.Metrics.Where(x => x != null).ToList();
        intent.Filters = intent.Filters.Where(x => x != null).ToList();
        intent.Dimensions = intent.Dimensions.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void NormalizeSemanticTexts(QueryIntent intent)
    {
        foreach (var metric in intent.Metrics)
            metric.SemanticText = string.IsNullOrWhiteSpace(metric.SemanticText) ? metric.Name?.Trim() ?? string.Empty : metric.SemanticText.Trim();

        foreach (var filter in intent.Filters)
            filter.SemanticText = string.IsNullOrWhiteSpace(filter.SemanticText) ? filter.Field?.Trim() ?? string.Empty : filter.SemanticText.Trim();
    }

    private static void NormalizeLimit(QueryIntent intent, string question)
    {
        if (intent.Limit is > 0) return;
        var arabic = Regex.Match(question, @"(?i)(?:top\s*|前\s*|最近\s*|最后\s*)(\d+)");
        if (arabic.Success && int.TryParse(arabic.Groups[1].Value, out var arabicLimit) && arabicLimit > 0) { intent.Limit = arabicLimit; return; }
        var chinese = Regex.Match(question, @"([零一二两三四五六七八九十百千万]+)(?:条|个|项|笔|张|份|记录|凭证|单据|订单)");
        if (!chinese.Success) return;
        var value = ParseChineseNumber(chinese.Groups[1].Value);
        if (value > 0) intent.Limit = value;
    }

    private static void NormalizeRanking(QueryIntent intent, string question)
    {
        var isDesc = DescWords.Any(x => question.Contains(x, StringComparison.OrdinalIgnoreCase));
        var isAsc = AscWords.Any(x => question.Contains(x, StringComparison.OrdinalIgnoreCase));
        var isTimeDesc = TimeDescWords.Any(x => question.Contains(x, StringComparison.OrdinalIgnoreCase));
        var hasTopN = intent.Limit.HasValue && (isDesc || isAsc || isTimeDesc || Regex.IsMatch(question, @"(?i)\btop\s*\d+") || Regex.IsMatch(question, @"前[一二两三四五六七八九十百千万0-9]+"));
        if (!hasTopN) return;
        intent.IntentType = "Ranking";
        if (isAsc) intent.OrderDirection = "ASC"; else if (isDesc || isTimeDesc) intent.OrderDirection = "DESC";
        ResolveOrderingMetric(intent, question);
    }

    private static void ResolveOrderingMetric(QueryIntent intent, string question)
    {
        if (!string.IsNullOrWhiteSpace(intent.OrderBy)) return;
        var metric = FindMetricFromQuestion(intent, question);
        if (metric == null) return;
        metric.IsOrderingMetric = true;
        intent.OrderBy = string.IsNullOrWhiteSpace(metric.Field) ? metric.Name : metric.Field;
    }

    private static QueryMetric? FindMetricFromQuestion(QueryIntent intent, string question)
        => intent.Metrics.FirstOrDefault(x => x.IsOrderingMetric) ?? intent.Metrics.Where(x => !string.IsNullOrWhiteSpace(x.Name) || !string.IsNullOrWhiteSpace(x.Field)).OrderByDescending(x => ScoreMetric(x, question)).FirstOrDefault();

    private static int ScoreMetric(QueryMetric metric, string question)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(metric.Name) && question.Contains(metric.Name, StringComparison.OrdinalIgnoreCase)) score += 100;
        if (!string.IsNullOrWhiteSpace(metric.Field) && question.Contains(metric.Field, StringComparison.OrdinalIgnoreCase)) score += 80;
        if (string.Equals(metric.SemanticType, "Quantity", StringComparison.OrdinalIgnoreCase) && question.Contains("数量")) score += 60;
        if (string.Equals(metric.SemanticType, "Amount", StringComparison.OrdinalIgnoreCase) && question.Contains("金额")) score += 60;
        if (string.Equals(metric.SemanticType, "Count", StringComparison.OrdinalIgnoreCase) && (question.Contains("数量") || question.Contains("多少"))) score += 40;
        return score;
    }

    private static void NormalizeAggregation(QueryIntent intent)
    { foreach (var metric in intent.Metrics) metric.Aggregation = NormalizeAggregationValue(metric.Aggregation); }

    private static void NormalizeQuantityAggregationSemantics(QueryIntent intent, string question)
    {
        if (intent.Metrics.Count == 0 || string.IsNullOrWhiteSpace(question)) return;
        if (intent.IntentType.Equals("Ranking", StringComparison.OrdinalIgnoreCase)) return;
        if (DetailWords.Any(x => question.Contains(x, StringComparison.OrdinalIgnoreCase))) return;

        var hasQuantityMetric = false;
        foreach (var metric in intent.Metrics)
        {
            if (!IsQuantityMetric(metric)) continue;
            hasQuantityMetric = true;
            if (string.Equals(metric.Aggregation, "NONE", StringComparison.OrdinalIgnoreCase))
                metric.Aggregation = InferMetricAggregation(metric.Name);
        }

        if (hasQuantityMetric && intent.Metrics.Any(x => string.Equals(x.Aggregation, "SUM", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Aggregation, "COUNT", StringComparison.OrdinalIgnoreCase)))
        {
            intent.IntentType = "Aggregate";
        }
    }

    /// <summary>
    /// 根据指标名称推断聚合类型。
    /// "入库单数量"/"供应商数量"/"客户数量"等实体数量 → COUNT；
    /// "数量"/"金额"/"总额"/"销量"等业务度量 → SUM。
    /// （R3 修复：从死代码版本迁移，替代原来全部默认为 SUM 的逻辑）
    /// </summary>
    private static string InferMetricAggregation(string? metricName)
    {
        var text = metricName?.Trim() ?? string.Empty;

        // 实体数量 → COUNT
        if (text.Contains("单数量", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("单据数量", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("订单数量", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("供应商数量", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("客户数量", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("不同供应商", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("不同客户", StringComparison.OrdinalIgnoreCase))
        {
            return "COUNT";
        }

        // 普通业务数量/金额指标 → SUM
        return "SUM";
    }

    private static void NormalizeMetricFields(QueryIntent intent)
    {
        if (intent.Metrics.Count == 0) return;
        NormalizeExplicitMultiMetricSemantics(intent);
        foreach (var metric in intent.Metrics)
        {
            var name = metric.Name?.Trim() ?? string.Empty;
            var field = metric.Field?.Trim() ?? string.Empty;
            if (name.Contains("金额", StringComparison.OrdinalIgnoreCase) && !field.Equals("amount", StringComparison.OrdinalIgnoreCase) && !field.Contains("amount", StringComparison.OrdinalIgnoreCase) && !field.Contains("money", StringComparison.OrdinalIgnoreCase) && !field.Contains("price", StringComparison.OrdinalIgnoreCase)) { metric.Field = "amount"; metric.SemanticType = "Amount"; continue; }
            if (name.Contains("数量", StringComparison.OrdinalIgnoreCase) && !field.Equals("quantity", StringComparison.OrdinalIgnoreCase) && !field.Contains("quantity", StringComparison.OrdinalIgnoreCase) && !field.Equals("qty", StringComparison.OrdinalIgnoreCase)) { metric.Field = "quantity"; if (string.IsNullOrWhiteSpace(metric.SemanticType)) metric.SemanticType = "Quantity"; }
        }
    }

    private static void NormalizeExplicitMultiMetricSemantics(QueryIntent intent)
    {
        var question = intent.OriginalQuestion ?? string.Empty;
        if (intent.Metrics.Count < 2 || !question.Contains("数量", StringComparison.OrdinalIgnoreCase) || !question.Contains("金额", StringComparison.OrdinalIgnoreCase)) return;
        var quantityMetric = intent.Metrics.FirstOrDefault(IsQuantityMetric);
        var amountMetric = intent.Metrics.FirstOrDefault(IsAmountMetric);
        if (quantityMetric is not null && amountMetric is not null && !ReferenceEquals(quantityMetric, amountMetric)) { SetQuantity(quantityMetric); SetAmount(amountMetric); return; }
        if (quantityMetric is not null) { var target = intent.Metrics.FirstOrDefault(x => !ReferenceEquals(x, quantityMetric)); if (target is not null) { SetQuantity(quantityMetric); SetAmount(target); } return; }
        SetQuantity(intent.Metrics[0]); SetAmount(intent.Metrics[1]);
    }

    private static void SetQuantity(QueryMetric metric) { metric.Field = "quantity"; metric.SemanticType = "Quantity"; }
    private static void SetAmount(QueryMetric metric) { metric.Field = "amount"; metric.SemanticType = "Amount"; }
    private static bool IsQuantityMetric(QueryMetric metric) => string.Equals(metric.SemanticType, "Quantity", StringComparison.OrdinalIgnoreCase) || (metric.Name?.Contains("数量", StringComparison.OrdinalIgnoreCase) ?? false) || string.Equals(metric.Field, "quantity", StringComparison.OrdinalIgnoreCase) || string.Equals(metric.Field, "qty", StringComparison.OrdinalIgnoreCase);
    private static bool IsAmountMetric(QueryMetric metric) => string.Equals(metric.SemanticType, "Amount", StringComparison.OrdinalIgnoreCase) || (metric.Name?.Contains("金额", StringComparison.OrdinalIgnoreCase) ?? false) || string.Equals(metric.Field, "amount", StringComparison.OrdinalIgnoreCase) || (metric.Field?.Contains("money", StringComparison.OrdinalIgnoreCase) ?? false) || (metric.Field?.Contains("price", StringComparison.OrdinalIgnoreCase) ?? false);

    private static void NormalizeYearFilters(QueryIntent intent)
    {
        foreach (var filter in intent.Filters)
        {
            if (filter == null || string.IsNullOrWhiteSpace(filter.Value)) continue;
            var value = filter.Value.Trim();
            var operatorText = filter.Operator?.Trim() ?? string.Empty;

            // 模式一：Value 为纯年份（"2025" / "2025年"），Operator 为 "="
            var plain = Regex.Match(value, @"^(\d{4})年?$");
            if (plain.Success && int.TryParse(plain.Groups[1].Value, out var plainYear) && plainYear >= 1900 && plainYear <= 9999)
            {
                // Operator 为 "="（或缺省）时直接规范化；LIKE 等其余情况走模式二
                if (string.IsNullOrWhiteSpace(operatorText) || string.Equals(operatorText, "=", StringComparison.OrdinalIgnoreCase))
                {
                    filter.Operator = ">="; filter.Value = $"{plainYear:D4}-01-01";
                    continue;
                }
            }

            // 模式二：Value 被 LIKE 模糊包裹（"%2025%" / "2025%" / "%2025"），
            // 或 Operator 为 LIKE 且 Value 内含 4 位年份——一律规范化为日期范围过滤
            var embedded = Regex.Match(value, @"(\d{4})年?");
            if (!embedded.Success || !int.TryParse(embedded.Groups[1].Value, out var embeddedYear) || embeddedYear < 1900 || embeddedYear > 9999) continue;
            if (!string.Equals(operatorText, "LIKE", StringComparison.OrdinalIgnoreCase)) continue;
            filter.Operator = ">="; filter.Value = $"{embeddedYear:D4}-01-01";
        }
    }

    private static string NormalizeAggregationValue(string? value) => value?.Trim().ToUpperInvariant() switch { "SUM" => "SUM", "COUNT" => "COUNT", "AVG" => "AVG", "AVERAGE" => "AVG", "MAX" => "MAX", "MIN" => "MIN", "DISTINCTCOUNT" => "DISTINCTCOUNT", "DISTINCT_COUNT" => "DISTINCTCOUNT", _ => "NONE" };

    private static int ParseChineseNumber(string value)
    {
        var digits = new Dictionary<char, int> { ['零'] = 0, ['一'] = 1, ['二'] = 2, ['两'] = 2, ['三'] = 3, ['四'] = 4, ['五'] = 5, ['六'] = 6, ['七'] = 7, ['八'] = 8, ['九'] = 9 };
        var units = new Dictionary<char, int> { ['十'] = 10, ['百'] = 100, ['千'] = 1000, ['万'] = 10000 };
        var total = 0; var section = 0; var number = 0;
        foreach (var ch in value)
        {
            if (digits.TryGetValue(ch, out var digit)) { number = digit; continue; }
            if (!units.TryGetValue(ch, out var unit)) continue;
            if (unit == 10000) { section += number; total += section * unit; section = 0; number = 0; continue; }
            if (number == 0) number = 1; section += number * unit; number = 0;
        }
        return total + section + number;
    }
}
