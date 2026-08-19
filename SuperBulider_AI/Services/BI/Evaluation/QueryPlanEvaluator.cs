using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.4-D.2 QueryPlan Evaluator。
/// 第一版比较 Golden Contract 已明确表达的业务约束，并增加 Runtime QueryPlan
/// 的物理绑定一致性检查。
/// Golden 为 null 的字段表示“不断言”，而不是要求 Runtime 为 null。
/// </summary>
public sealed class QueryPlanEvaluator
{
    public QueryPlanEvaluationResult Evaluate(
        string caseId,
        GoldenQueryExpectation expected,
        QueryPlan runtime)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(runtime);

        var intent = EvaluateIntent(expected, runtime);
        var metrics = EvaluateMetrics(expected, runtime);
        var dimensions = EvaluateDimensions(expected, runtime);
        var filters = EvaluateFilters(expected, runtime);
        var shape = EvaluateShape(expected, runtime);
        var bindingConsistency = EvaluateBindingConsistency(expected, runtime);

        return new QueryPlanEvaluationResult
        {
            CaseId = caseId,
            Passed = intent.Passed
                      && metrics.Passed
                      && dimensions.Passed
                      && filters.Passed
                      && shape.Passed
                      && bindingConsistency.Passed,
            Intent = intent,
            Metrics = metrics,
            Dimensions = dimensions,
            Filters = filters,
            QueryShape = shape,
            BindingConsistency = bindingConsistency
        };
    }

    private static QueryPlanEvaluationSectionResult EvaluateIntent(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (string.IsNullOrWhiteSpace(expected.IntentType))
            return Pass("Golden 未指定 IntentType，不进行断言。");

        var actual = runtime.Intent?.IntentType.ToString();
        return string.Equals(expected.IntentType, actual, StringComparison.OrdinalIgnoreCase)
            ? Pass($"IntentType 匹配：{actual}。")
            : Fail($"期望 IntentType={expected.IntentType}，实际为 {actual ?? "null"}。");
    }

    private static QueryPlanEvaluationSectionResult EvaluateMetrics(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.Metrics is null)
            return Pass("Golden 未指定 Metrics，不进行断言。");

        var actual = runtime.Metrics ?? new List<QueryMetric>();
        if (actual.Count != expected.Metrics.Count)
            return Fail($"Metric 数量不匹配：期望 {expected.Metrics.Count}，实际 {actual.Count}。");

        for (var i = 0; i < expected.Metrics.Count; i++)
        {
            var golden = expected.Metrics[i];
            var metric = actual[i];

            if (!string.IsNullOrWhiteSpace(golden.SemanticText) && !MatchesSemantic(metric, golden.SemanticText))
                return Fail($"第 {i + 1} 个 Metric 语义不匹配：期望“{golden.SemanticText}”。");

            if (!string.IsNullOrWhiteSpace(golden.Field)
                && !string.Equals(metric.Field, golden.Field, StringComparison.OrdinalIgnoreCase))
                return Fail($"第 {i + 1} 个 Metric 字段不匹配：期望 {golden.Field}，实际 {metric.Field ?? "null"}。");

            if (metric.GetAggregation() != golden.Aggregation)
                return Fail($"第 {i + 1} 个 Metric 聚合不匹配：期望 {golden.Aggregation}，实际 {metric.GetAggregation()}。");
        }

        return Pass("Metrics 匹配。");
    }

    private static bool MatchesSemantic(QueryMetric metric, string semanticText)
    {
        return string.Equals(metric.Name, semanticText, StringComparison.OrdinalIgnoreCase)
               || string.Equals(metric.Field, semanticText, StringComparison.OrdinalIgnoreCase)
               || string.Equals(metric.SemanticType, semanticText, StringComparison.OrdinalIgnoreCase);
    }

    private static QueryPlanEvaluationSectionResult EvaluateDimensions(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.Dimensions is null)
            return Pass("Golden 未指定 Dimensions，不进行断言。");

        var actual = runtime.Dimensions ?? new List<QueryDimension>();
        return actual.Count == expected.Dimensions.Count
            ? Pass($"Dimensions 数量匹配：{actual.Count}。")
            : Fail($"Dimensions 数量不匹配：期望 {expected.Dimensions.Count}，实际 {actual.Count}。");
    }

    private static QueryPlanEvaluationSectionResult EvaluateFilters(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.Filters is null)
            return Pass("Golden 未指定 Filters，不进行断言。");

        var actual = runtime.Filters ?? new List<QueryFilter>();
        return actual.Count == expected.Filters.Count
            ? Pass($"Filters 数量匹配：{actual.Count}。")
            : Fail($"Filters 数量不匹配：期望 {expected.Filters.Count}，实际 {actual.Count}。");
    }

    private static QueryPlanEvaluationSectionResult EvaluateShape(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.IsAggregate.HasValue && runtime.IsAggregate != expected.IsAggregate.Value)
            return Fail($"IsAggregate 不匹配：期望 {expected.IsAggregate.Value}，实际 {runtime.IsAggregate}。");
        if (expected.Distinct.HasValue && runtime.Distinct != expected.Distinct.Value)
            return Fail($"Distinct 不匹配：期望 {expected.Distinct.Value}，实际 {runtime.Distinct}。");
        if (expected.Limit.HasValue && runtime.Limit != expected.Limit.Value)
            return Fail($"Limit 不匹配：期望 {expected.Limit.Value}，实际 {runtime.Limit}。");
        if (expected.IsRanking.HasValue && runtime.IsRanking != expected.IsRanking.Value)
            return Fail($"IsRanking 不匹配：期望 {expected.IsRanking.Value}，实际 {runtime.IsRanking}。");
        if (expected.IsDetailRanking.HasValue && runtime.IsDetailRanking != expected.IsDetailRanking.Value)
            return Fail($"IsDetailRanking 不匹配：期望 {expected.IsDetailRanking.Value}，实际 {runtime.IsDetailRanking}。");
        if (expected.IsAggregateRanking.HasValue && runtime.IsAggregateRanking != expected.IsAggregateRanking.Value)
            return Fail($"IsAggregateRanking 不匹配：期望 {expected.IsAggregateRanking.Value}，实际 {runtime.IsAggregateRanking}。");

        if (expected.Orders is not null)
        {
            var actual = runtime.Orders ?? new List<QueryOrder>();
            if (actual.Count != expected.Orders.Count)
                return Fail($"Orders 数量不匹配：期望 {expected.Orders.Count}，实际 {actual.Count}。");
        }

        if (expected.Joins is not null)
        {
            var actual = runtime.Joins ?? new List<QueryJoin>();
            if (actual.Count != expected.Joins.Count)
                return Fail($"Joins 数量不匹配：期望 {expected.Joins.Count}，实际 {actual.Count}。");
        }

        if (expected.Tables is not null)
        {
            var actual = runtime.Tables ?? new List<QueryTable>();
            if (actual.Count != expected.Tables.Count)
                return Fail($"Tables 数量不匹配：期望 {expected.Tables.Count}，实际 {actual.Count}。");
        }

        return Pass("Query Shape 匹配。");
    }

    /// <summary>
    /// 验证 Runtime QueryPlan 自身的物理绑定是否自洽。
    /// 该检查不猜业务语义，也不把 Golden 缺失的字段强行变成断言。
    /// </summary>
    private static QueryPlanEvaluationSectionResult EvaluateBindingConsistency(
        GoldenQueryExpectation expected,
        QueryPlan runtime)
    {
        var tables = runtime.Tables ?? new List<QueryTable>();
        var joins = runtime.Joins ?? new List<QueryJoin>();

        if (runtime.DataSourceId > 0
            && tables.Any(table => table.DataSourceId > 0 && table.DataSourceId != runtime.DataSourceId))
        {
            return Fail("Binding 一致性失败：QueryPlan.Tables 存在与 QueryPlan.DataSourceId 不一致的数据源。");
        }

        var duplicateTableIds = tables
            .Where(table => table.MetadataTableId > 0)
            .GroupBy(table => table.MetadataTableId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateTableIds.Count > 0)
        {
            return Fail(
                $"Binding 一致性失败：存在重复 MetadataTableId：{string.Join(", ", duplicateTableIds)}。");
        }

        foreach (var join in joins)
        {
            if (!tables.Any(table => table.MetadataTableId == join.LeftTableId)
                || !tables.Any(table => table.MetadataTableId == join.RightTableId))
            {
                return Fail(
                    $"Binding 一致性失败：Join 引用了 QueryPlan.Tables 中不存在的表，LeftTableId={join.LeftTableId}，RightTableId={join.RightTableId}。");
            }
        }

        if (expected.Tables is not null && tables.Count != expected.Tables.Count)
        {
            return Fail($"Binding 一致性失败：Golden Tables={expected.Tables.Count}，Runtime Tables={tables.Count}。");
        }

        if (expected.Joins is not null && joins.Count != expected.Joins.Count)
        {
            return Fail($"Binding 一致性失败：Golden Joins={expected.Joins.Count}，Runtime Joins={joins.Count}。");
        }

        return Pass(
            $"Binding 一致性通过：DataSource={runtime.DataSourceId}，Tables={tables.Count}，Joins={joins.Count}，无重复表且所有 Join 均引用已绑定表。");
    }

    private static QueryPlanEvaluationSectionResult Pass(string reason) => new() { Passed = true, Reason = reason };

    private static QueryPlanEvaluationSectionResult Fail(string reason) => new() { Passed = false, Reason = reason };
}
