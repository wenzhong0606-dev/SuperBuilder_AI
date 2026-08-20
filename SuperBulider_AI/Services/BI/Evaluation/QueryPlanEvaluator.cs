using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 QueryPlan Evaluator。
/// Golden Contract 与 Runtime QueryPlan 解耦；Golden 为 null 的字段表示不断言。
/// </summary>
public sealed class QueryPlanEvaluator
{
    public QueryPlanEvaluationResult Evaluate(string caseId, GoldenQueryExpectation expected, QueryPlan runtime)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(runtime);

        var intent = EvaluateIntent(expected, runtime);
        var metrics = EvaluateMetrics(expected, runtime);
        var dimensions = EvaluateDimensions(expected, runtime);
        var filters = EvaluateFilters(expected, runtime);
        var tables = EvaluateTables(expected, runtime);
        var joins = EvaluateJoins(expected, runtime);
        var shape = EvaluateShape(expected, runtime);
        var bindingConsistency = EvaluateBindingConsistency(expected, runtime);

        return new QueryPlanEvaluationResult
        {
            CaseId = caseId,
            Passed = intent.Passed && metrics.Passed && dimensions.Passed && filters.Passed
                      && tables.Passed && joins.Passed && shape.Passed && bindingConsistency.Passed,
            Intent = intent,
            Metrics = metrics,
            Dimensions = dimensions,
            Filters = filters,
            Tables = tables,
            Joins = joins,
            QueryShape = shape,
            BindingConsistency = bindingConsistency
        };
    }

    /// <summary>
    /// C.4.8：独立评价 Semantic Applicability Resolution 与 Runtime QueryPlan 的物理绑定。
    /// 该方法不改变原有 Evaluate Gate，调用方可在拥有 Applicability 结果时追加语义证据。
    /// </summary>
    public QueryPlanSemanticEvidenceResult EvaluateSemanticEvidence(
        string caseId,
        GoldenQueryExpectation expected,
        QueryPlan runtime,
        SemanticApplicabilityResult applicability)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(applicability);

        var goldenMetric = expected.Metrics?.SingleOrDefault();
        var runtimeMetric = runtime.Metrics?.SingleOrDefault();
        var resolution = applicability.Resolution;
        var resolutionExists = resolution is not null;
        var runtimeMetricExists = runtimeMetric is not null;

        if (goldenMetric is null)
        {
            return new QueryPlanSemanticEvidenceResult
            {
                CaseId = caseId,
                Passed = true,
                Reason = "Golden 未定义单一 Metric，不进行 Semantic Resolution Evidence 断言。",
                ApplicabilityState = applicability.State,
                ResolutionExists = resolutionExists,
                RuntimeMetricExists = runtimeMetricExists
            };
        }

        if (!string.Equals(applicability.CaseId, caseId, StringComparison.OrdinalIgnoreCase))
            return SemanticEvidenceFail(caseId, goldenMetric.SemanticText, applicability, "Applicability CaseId 与当前 Evaluation CaseId 不一致。");

        if (!string.Equals(applicability.MetricSemanticText, goldenMetric.SemanticText, StringComparison.OrdinalIgnoreCase))
            return SemanticEvidenceFail(caseId, goldenMetric.SemanticText, applicability, "Applicability MetricSemanticText 与 Golden Metric.SemanticText 不一致。");

        if (!string.Equals(applicability.State, "Resolved", StringComparison.OrdinalIgnoreCase))
        {
            return SemanticEvidenceFail(
                caseId,
                goldenMetric.SemanticText,
                applicability,
                $"Golden Metric 需要 Semantic Resolution，但 Applicability 当前状态为 {applicability.State}。");
        }

        if (resolution is null)
            return SemanticEvidenceFail(caseId, goldenMetric.SemanticText, applicability, "Applicability 为 Resolved，但 Resolution 为空。");

        if (runtimeMetric is null)
            return SemanticEvidenceFail(caseId, goldenMetric.SemanticText, applicability, "Applicability 已 Resolved，但 Runtime QueryPlan 没有 Metric。");

        var fieldMatches = string.Equals(runtimeMetric.Field, resolution.Column, StringComparison.OrdinalIgnoreCase);
        var table = runtime.Tables?.FirstOrDefault(t => t.MetadataTableId == resolution.TableId);
        var tableMatches = table is not null
                           && table.DataSourceId == resolution.DataSourceId
                           && string.Equals(table.TableName, resolution.Table, StringComparison.OrdinalIgnoreCase);
        var dataSourceMatches = runtime.DataSourceId == resolution.DataSourceId;

        if (!fieldMatches)
            return SemanticEvidenceFail(caseId, goldenMetric.SemanticText, applicability, $"Runtime Metric Field={runtimeMetric.Field} 与 Resolution.Column={resolution.Column} 不一致。", resolution.Score, runtimeMetricExists, false, tableMatches, dataSourceMatches);
        if (!tableMatches)
            return SemanticEvidenceFail(caseId, goldenMetric.SemanticText, applicability, $"Runtime QueryPlan 未找到与 Resolution.TableId={resolution.TableId}、Table={resolution.Table}、DataSourceId={resolution.DataSourceId} 一致的物理表。", resolution.Score, runtimeMetricExists, true, false, dataSourceMatches);
        if (!dataSourceMatches)
            return SemanticEvidenceFail(caseId, goldenMetric.SemanticText, applicability, $"Runtime DataSourceId={runtime.DataSourceId} 与 Resolution.DataSourceId={resolution.DataSourceId} 不一致。", resolution.Score, runtimeMetricExists, true, true, false);

        return new QueryPlanSemanticEvidenceResult
        {
            CaseId = caseId,
            Passed = true,
            Reason = "Semantic Resolution 与 Runtime QueryPlan 的 Metric Field、Table、DataSource 物理绑定一致。",
            GoldenSemanticText = goldenMetric.SemanticText,
            ApplicabilityState = applicability.State,
            ResolutionExists = true,
            RuntimeMetricExists = true,
            MetricFieldMatchesResolution = true,
            TableBindingMatchesResolution = true,
            DataSourceBindingMatchesResolution = true,
            ResolutionScore = resolution.Score
        };
    }

    private static QueryPlanSemanticEvidenceResult SemanticEvidenceFail(
        string caseId,
        string semanticText,
        SemanticApplicabilityResult applicability,
        string reason,
        double? score = null,
        bool runtimeMetricExists = false,
        bool fieldMatches = false,
        bool tableMatches = false,
        bool dataSourceMatches = false)
    {
        return new QueryPlanSemanticEvidenceResult
        {
            CaseId = caseId,
            Passed = false,
            Reason = reason,
            GoldenSemanticText = semanticText,
            ApplicabilityState = applicability.State,
            ResolutionExists = applicability.Resolution is not null,
            RuntimeMetricExists = runtimeMetricExists,
            MetricFieldMatchesResolution = fieldMatches,
            TableBindingMatchesResolution = tableMatches,
            DataSourceBindingMatchesResolution = dataSourceMatches,
            ResolutionScore = score ?? applicability.Resolution?.Score
        };
    }

    private static QueryPlanEvaluationSectionResult EvaluateIntent(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (string.IsNullOrWhiteSpace(expected.IntentType)) return Pass("Golden 未指定 IntentType，不进行断言。");
        var actual = runtime.Intent?.IntentType.ToString();
        return string.Equals(expected.IntentType, actual, StringComparison.OrdinalIgnoreCase)
            ? Pass($"IntentType 匹配：{actual}。")
            : Fail($"期望 IntentType={expected.IntentType}，实际为 {actual ?? "null"}。");
    }

    private static QueryPlanEvaluationSectionResult EvaluateMetrics(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.Metrics is null) return Pass("Golden 未指定 Metrics，不进行断言。");
        var actual = runtime.Metrics ?? new List<QueryMetric>();
        if (actual.Count != expected.Metrics.Count) return Fail($"Metric 数量不匹配：期望 {expected.Metrics.Count}，实际 {actual.Count}。");
        for (var i = 0; i < expected.Metrics.Count; i++)
        {
            var golden = expected.Metrics[i];
            var metric = actual[i];
            if (!string.IsNullOrWhiteSpace(golden.SemanticText) && !MatchesSemantic(metric, golden.SemanticText))
                return Fail($"第 {i + 1} 个 Metric 语义不匹配：期望“{golden.SemanticText}”。");
            if (!string.IsNullOrWhiteSpace(golden.Field) && !string.Equals(metric.Field, golden.Field, StringComparison.OrdinalIgnoreCase))
                return Fail($"第 {i + 1} 个 Metric 字段不匹配：期望 {golden.Field}，实际 {metric.Field ?? "null"}。");
            if (metric.GetAggregation() != golden.Aggregation)
                return Fail($"第 {i + 1} 个 Metric 聚合不匹配：期望 {golden.Aggregation}，实际 {metric.GetAggregation()}。");
        }
        return Pass("Metrics 匹配。");
    }

    private static bool MatchesSemantic(QueryMetric metric, string semanticText) =>
        string.Equals(metric.Name, semanticText, StringComparison.OrdinalIgnoreCase)
        || string.Equals(metric.Field, semanticText, StringComparison.OrdinalIgnoreCase)
        || string.Equals(metric.SemanticType, semanticText, StringComparison.OrdinalIgnoreCase);

    private static QueryPlanEvaluationSectionResult EvaluateDimensions(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.Dimensions is null) return Pass("Golden 未指定 Dimensions，不进行断言。");
        var actual = runtime.Dimensions ?? new List<QueryDimension>();
        if (expected.Dimensions.Count == 0)
            return actual.Count == 0 ? Pass("Golden 明确要求无 Dimensions，Runtime 为空。") : Fail($"Golden 明确要求无 Dimensions，实际存在 {actual.Count} 个 Dimension。");
        if (actual.Count != expected.Dimensions.Count) return Fail($"Dimensions 数量不匹配：期望 {expected.Dimensions.Count}，实际 {actual.Count}。");
        for (var i = 0; i < actual.Count; i++)
        {
            if (actual[i].MetadataColumnId <= 0) return Fail($"第 {i + 1} 个 Dimension 物理绑定无效：MetadataColumnId={actual[i].MetadataColumnId}。");
            if (string.IsNullOrWhiteSpace(actual[i].ColumnName)) return Fail($"第 {i + 1} 个 Dimension 物理绑定无效：ColumnName 为空。");
        }
        return Pass($"Dimensions 数量与 Runtime 物理绑定完整性匹配：{actual.Count} 个 Dimension。Golden SemanticText 当前不直接与 ColumnName 等值比较。");
    }

    private static QueryPlanEvaluationSectionResult EvaluateFilters(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.Filters is null) return Pass("Golden 未指定 Filters，不进行断言。");
        var actual = runtime.Filters ?? new List<QueryFilter>();
        if (expected.Filters.Count == 0)
            return actual.Count == 0 ? Pass("Golden 明确要求无 Filters，Runtime 为空。") : Fail($"Golden 明确要求无 Filters，实际存在 {actual.Count} 个 Filter。");
        if (actual.Count != expected.Filters.Count) return Fail($"Filters 数量不匹配：期望 {expected.Filters.Count}，实际 {actual.Count}。");
        for (var i = 0; i < expected.Filters.Count; i++)
        {
            var golden = expected.Filters[i];
            var filter = actual[i];
            if (string.IsNullOrWhiteSpace(filter.Field)) return Fail($"第 {i + 1} 个 Filter 物理绑定无效：Field 为空。");
            if (!string.IsNullOrWhiteSpace(golden.Operator) && !string.Equals(filter.Operator, golden.Operator, StringComparison.OrdinalIgnoreCase)) return Fail($"第 {i + 1} 个 Filter 操作符不匹配：期望 {golden.Operator}，实际 {filter.Operator}。");
            if (golden.Value is not null && !string.Equals(filter.Value, golden.Value, StringComparison.Ordinal)) return Fail($"第 {i + 1} 个 Filter 值不匹配：期望 {golden.Value}，实际 {filter.Value}。");
        }
        return Pass($"Filters 数量、Operator、Golden 已声明的 Value 约束匹配；{actual.Count} 个 Runtime Filter 均具有有效 Field。Golden SemanticText 当前不直接与 Runtime Field 等值比较。");
    }

    private static QueryPlanEvaluationSectionResult EvaluateTables(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.Tables is null) return Pass("Golden 未指定 Tables，不进行断言。");
        var actual = runtime.Tables ?? new List<QueryTable>();
        if (expected.Tables.Count == 0) return actual.Count == 0 ? Pass("Golden 明确要求无 Tables，Runtime 为空。") : Fail($"Golden 明确要求无 Tables，实际存在 {actual.Count} 个 Table。");
        if (actual.Count != expected.Tables.Count) return Fail($"Tables 数量不匹配：期望 {expected.Tables.Count}，实际 {actual.Count}。");
        for (var i = 0; i < actual.Count; i++)
        {
            var table = actual[i];
            if (table.MetadataTableId <= 0) return Fail($"第 {i + 1} 个 Table 物理绑定无效：MetadataTableId={table.MetadataTableId}。");
            if (table.DataSourceId <= 0) return Fail($"第 {i + 1} 个 Table 物理绑定无效：DataSourceId={table.DataSourceId}。");
            if (string.IsNullOrWhiteSpace(table.TableName)) return Fail($"第 {i + 1} 个 Table 物理绑定无效：TableName 为空。");
        }
        return Pass($"Tables 数量与 Runtime 物理绑定完整性匹配：{actual.Count} 个 Table，均具有有效 MetadataTableId、DataSourceId 和 TableName。Golden SemanticText 当前不直接与 TableName/TableComment 等值比较。");
    }

    private static QueryPlanEvaluationSectionResult EvaluateJoins(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.Joins is null) return Pass("Golden 未指定 Joins，不进行断言。");
        var actual = runtime.Joins ?? new List<QueryJoin>();
        if (expected.Joins.Count == 0) return actual.Count == 0 ? Pass("Golden 明确要求无 Joins，Runtime 为空。") : Fail($"Golden 明确要求无 Joins，实际存在 {actual.Count} 个 Join。");
        if (actual.Count != expected.Joins.Count) return Fail($"Joins 数量不匹配：期望 {expected.Joins.Count}，实际 {actual.Count}。");
        var tables = runtime.Tables ?? new List<QueryTable>();
        var tableIds = tables.Select(t => t.MetadataTableId).Where(id => id > 0).ToHashSet();
        for (var i = 0; i < expected.Joins.Count; i++)
        {
            var golden = expected.Joins[i];
            var join = actual[i];
            if (join.LeftTableId <= 0 || !tableIds.Contains(join.LeftTableId)) return Fail($"第 {i + 1} 个 Join 左表绑定无效：LeftTableId={join.LeftTableId}。");
            if (join.RightTableId <= 0 || !tableIds.Contains(join.RightTableId)) return Fail($"第 {i + 1} 个 Join 右表绑定无效：RightTableId={join.RightTableId}。");
            if (join.LeftTableId == join.RightTableId) return Fail($"第 {i + 1} 个 Join 非法：LeftTableId 与 RightTableId 相同（{join.LeftTableId}）。");
            if (join.LeftColumnId <= 0) return Fail($"第 {i + 1} 个 Join 左字段绑定无效：LeftColumnId={join.LeftColumnId}。");
            if (join.RightColumnId <= 0) return Fail($"第 {i + 1} 个 Join 右字段绑定无效：RightColumnId={join.RightColumnId}。");
            if (string.IsNullOrWhiteSpace(join.LeftColumnName) || string.IsNullOrWhiteSpace(join.RightColumnName)) return Fail($"第 {i + 1} 个 Join 字段名称绑定无效：LeftColumnName/RightColumnName 不得为空。");
            var expectedJoinType = string.IsNullOrWhiteSpace(golden.JoinType) ? "INNER" : golden.JoinType;
            if (!string.Equals(join.JoinType, expectedJoinType, StringComparison.OrdinalIgnoreCase)) return Fail($"第 {i + 1} 个 Join 类型不匹配：期望 {expectedJoinType}，实际 {join.JoinType}。");
        }
        return Pass($"Joins 数量、JoinType 与 Runtime 物理引用完整性匹配：{actual.Count} 个 Join。Golden 左右表/字段 SemanticText 当前不直接与物理名称等值比较。");
    }

    private static QueryPlanEvaluationSectionResult EvaluateShape(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.IsAggregate.HasValue && runtime.IsAggregate != expected.IsAggregate.Value) return Fail($"IsAggregate 不匹配：期望 {expected.IsAggregate.Value}，实际 {runtime.IsAggregate}。");
        if (expected.Distinct.HasValue && runtime.Distinct != expected.Distinct.Value) return Fail($"Distinct 不匹配：期望 {expected.Distinct.Value}，实际 {runtime.Distinct}。");
        if (expected.Limit.HasValue && runtime.Limit != expected.Limit.Value) return Fail($"Limit 不匹配：期望 {expected.Limit.Value}，实际 {runtime.Limit}。");
        if (expected.IsRanking.HasValue && runtime.IsRanking != expected.IsRanking.Value) return Fail($"IsRanking 不匹配：期望 {expected.IsRanking.Value}，实际 {runtime.IsRanking}。");
        if (expected.IsDetailRanking.HasValue && runtime.IsDetailRanking != expected.IsDetailRanking.Value) return Fail($"IsDetailRanking 不匹配：期望 {expected.IsDetailRanking.Value}，实际 {runtime.IsDetailRanking}。");
        if (expected.IsAggregateRanking.HasValue && runtime.IsAggregateRanking != expected.IsAggregateRanking.Value) return Fail($"IsAggregateRanking 不匹配：期望 {expected.IsAggregateRanking.Value}，实际 {runtime.IsAggregateRanking}。");
        if (expected.Orders is not null && (runtime.Orders?.Count ?? 0) != expected.Orders.Count) return Fail($"Orders 数量不匹配：期望 {expected.Orders.Count}，实际 {runtime.Orders?.Count ?? 0}。");
        if (expected.Joins is not null && (runtime.Joins?.Count ?? 0) != expected.Joins.Count) return Fail($"Joins 数量不匹配：期望 {expected.Joins.Count}，实际 {runtime.Joins?.Count ?? 0}。");
        if (expected.Tables is not null && (runtime.Tables?.Count ?? 0) != expected.Tables.Count) return Fail($"Tables 数量不匹配：期望 {expected.Tables.Count}，实际 {runtime.Tables?.Count ?? 0}。");
        return Pass("Query Shape 匹配。");
    }

    private static QueryPlanEvaluationSectionResult EvaluateBindingConsistency(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        var tables = runtime.Tables ?? new List<QueryTable>();
        var joins = runtime.Joins ?? new List<QueryJoin>();
        if (runtime.DataSourceId > 0 && tables.Any(table => table.DataSourceId > 0 && table.DataSourceId != runtime.DataSourceId)) return Fail("Binding 一致性失败：QueryPlan.Tables 存在与 QueryPlan.DataSourceId 不一致的数据源。");
        var duplicateTableIds = tables.Where(table => table.MetadataTableId > 0).GroupBy(table => table.MetadataTableId).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        if (duplicateTableIds.Count > 0) return Fail($"Binding 一致性失败：存在重复 MetadataTableId：{string.Join(", ", duplicateTableIds)}。");
        foreach (var join in joins)
            if (!tables.Any(table => table.MetadataTableId == join.LeftTableId) || !tables.Any(table => table.MetadataTableId == join.RightTableId))
                return Fail($"Binding 一致性失败：Join 引用了 QueryPlan.Tables 中不存在的表，LeftTableId={join.LeftTableId}，RightTableId={join.RightTableId}。");
        if (expected.Tables is not null && tables.Count != expected.Tables.Count) return Fail($"Binding 一致性失败：Golden Tables={expected.Tables.Count}，Runtime Tables={tables.Count}。");
        if (expected.Joins is not null && joins.Count != expected.Joins.Count) return Fail($"Binding 一致性失败：Golden Joins={expected.Joins.Count}，Runtime Joins={joins.Count}。");
        return Pass($"Binding 一致性通过：DataSource={runtime.DataSourceId}，Tables={tables.Count}，Joins={joins.Count}，无重复表且所有 Join 均引用已绑定表。");
    }

    private static QueryPlanEvaluationSectionResult Pass(string reason) => new() { Passed = true, Reason = reason };
    private static QueryPlanEvaluationSectionResult Fail(string reason) => new() { Passed = false, Reason = reason };
}
