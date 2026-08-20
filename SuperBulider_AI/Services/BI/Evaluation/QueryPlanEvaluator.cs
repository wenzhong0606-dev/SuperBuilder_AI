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

    /// <summary>
    /// C.4.2 Dimension Evaluation。
    /// Golden Dimension 目前只表达数据库无关的 SemanticText，因此本阶段不伪造
    /// SemanticText 与 ColumnName 的直接等值关系；先验证三态集合语义、数量以及
    /// Runtime Dimension 的最基本物理绑定完整性。真正的语义解析由 Semantic
    /// Applicability / Resolution 层负责，后续 C.4 Evaluation Evidence 再接入。
    /// </summary>
    private static QueryPlanEvaluationSectionResult EvaluateDimensions(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.Dimensions is null)
            return Pass("Golden 未指定 Dimensions，不进行断言。");

        var actual = runtime.Dimensions ?? new List<QueryDimension>();

        if (expected.Dimensions.Count == 0)
        {
            return actual.Count == 0
                ? Pass("Golden 明确要求无 Dimensions，Runtime 为空。")
                : Fail($"Golden 明确要求无 Dimensions，实际存在 {actual.Count} 个 Dimension。");
        }

        if (actual.Count != expected.Dimensions.Count)
            return Fail($"Dimensions 数量不匹配：期望 {expected.Dimensions.Count}，实际 {actual.Count}。");

        for (var i = 0; i < actual.Count; i++)
        {
            var dimension = actual[i];

            if (dimension.MetadataColumnId <= 0)
                return Fail($"第 {i + 1} 个 Dimension 物理绑定无效：MetadataColumnId={dimension.MetadataColumnId}。");

            if (string.IsNullOrWhiteSpace(dimension.ColumnName))
                return Fail($"第 {i + 1} 个 Dimension 物理绑定无效：ColumnName 为空。");
        }

        return Pass(
            $"Dimensions 数量与 Runtime 物理绑定完整性匹配：{actual.Count} 个 Dimension，均具有有效 MetadataColumnId 和 ColumnName。" +
            " Golden SemanticText 当前不直接与 ColumnName 等值比较。");
    }

    /// <summary>
    /// C.4.3 Filter Evaluation。
    /// Golden Filter 是数据库无关的业务语义 Contract：SemanticText 不直接与 Runtime
    /// Field 做字符串等值判断；Operator 与 Value 则属于 Golden 已明确表达的约束，
    /// 因此在本阶段直接进行精确比较。Runtime Field 必须存在，作为最基本的物理绑定证据。
    /// </summary>
    private static QueryPlanEvaluationSectionResult EvaluateFilters(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.Filters is null)
            return Pass("Golden 未指定 Filters，不进行断言。");

        var actual = runtime.Filters ?? new List<QueryFilter>();

        // Golden 明确指定空集合：要求 Runtime 也没有 Filter。
        if (expected.Filters.Count == 0)
        {
            return actual.Count == 0
                ? Pass("Golden 明确要求无 Filters，Runtime 为空。")
                : Fail($"Golden 明确要求无 Filters，实际存在 {actual.Count} 个 Filter。");
        }

        if (actual.Count != expected.Filters.Count)
            return Fail($"Filters 数量不匹配：期望 {expected.Filters.Count}，实际 {actual.Count}。");

        for (var i = 0; i < expected.Filters.Count; i++)
        {
            var golden = expected.Filters[i];
            var filter = actual[i];

            if (string.IsNullOrWhiteSpace(filter.Field))
                return Fail($"第 {i + 1} 个 Filter 物理绑定无效：Field 为空。");

            if (!string.IsNullOrWhiteSpace(golden.Operator)
                && !string.Equals(filter.Operator, golden.Operator, StringComparison.OrdinalIgnoreCase))
            {
                return Fail(
                    $"第 {i + 1} 个 Filter 操作符不匹配：期望 {golden.Operator}，实际 {filter.Operator}。");
            }

            if (golden.Value is not null
                && !string.Equals(filter.Value, golden.Value, StringComparison.Ordinal))
            {
                return Fail(
                    $"第 {i + 1} 个 Filter 值不匹配：期望 {golden.Value}，实际 {filter.Value}。");
            }
        }

        return Pass(
            $"Filters 数量、Operator、Golden 已声明的 Value 约束匹配；{actual.Count} 个 Runtime Filter 均具有有效 Field。" +
            " Golden SemanticText 当前不直接与 Runtime Field 等值比较。");
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
            return Fail($"Binding 一致性失败：Golden Tables={expected.Tables.Count}，Runtime Tables={tables.Count}。");

        if (expected.Joins is not null && joins.Count != expected.Joins.Count)
            return Fail($"Binding 一致性失败：Golden Joins={expected.Joins.Count}，Runtime Joins={joins.Count}。");

        return Pass(
            $"Binding 一致性通过：DataSource={runtime.DataSourceId}，Tables={tables.Count}，Joins={joins.Count}，无重复表且所有 Join 均引用已绑定表。");
    }

    private static QueryPlanEvaluationSectionResult Pass(string reason) => new() { Passed = true, Reason = reason };

    private static QueryPlanEvaluationSectionResult Fail(string reason) => new() { Passed = false, Reason = reason };
}
