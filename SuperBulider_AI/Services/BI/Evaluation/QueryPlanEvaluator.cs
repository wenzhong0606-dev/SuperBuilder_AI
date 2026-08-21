using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 QueryPlan Evaluator。
/// 负责组织各 Evaluation Section，并将最终结果交给统一 Scoring Contract。
/// Golden Contract 与 Runtime QueryPlan 解耦；Golden 为 null 的字段表示不断言。
/// </summary>
public sealed class QueryPlanEvaluator
{
    private readonly QueryPlanJoinScoringService _joinScoringService;
    private readonly QueryPlanMetricScoringService _metricScoringService;
    private readonly QueryPlanDimensionScoringService _dimensionScoringService;
    private readonly QueryPlanFilterScoringService _filterScoringService;
    private readonly QueryPlanQueryShapeScoringService _queryShapeScoringService;
    private readonly QueryPlanEvaluationScoringService _evaluationScoringService;

    public QueryPlanEvaluator(
        QueryPlanJoinScoringService joinScoringService,
        QueryPlanMetricScoringService metricScoringService,
        QueryPlanDimensionScoringService dimensionScoringService,
        QueryPlanFilterScoringService filterScoringService,
        QueryPlanQueryShapeScoringService queryShapeScoringService,
        QueryPlanEvaluationScoringService evaluationScoringService)
    {
        _joinScoringService = joinScoringService;
        _metricScoringService = metricScoringService;
        _dimensionScoringService = dimensionScoringService;
        _filterScoringService = filterScoringService;
        _queryShapeScoringService = queryShapeScoringService;
        _evaluationScoringService = evaluationScoringService;
    }

    public QueryPlanEvaluationResult Evaluate(string caseId, GoldenQueryExpectation expected, QueryPlan runtime)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(runtime);

        var intent = EvaluateIntent(expected, runtime);

        var metricEvaluation = _metricScoringService.Evaluate(expected.Metrics, runtime.Metrics);
        var metrics = new QueryPlanEvaluationSectionResult
        {
            Passed = metricEvaluation.Passed,
            Score = metricEvaluation.Score,
            Reason = metricEvaluation.Reason,
            Details = metricEvaluation.Items
        };

        var dimensionEvaluation = _dimensionScoringService.Evaluate(expected.Dimensions, runtime.Dimensions);
        var dimensions = new QueryPlanEvaluationSectionResult
        {
            Passed = dimensionEvaluation.Passed,
            Score = dimensionEvaluation.Score,
            Reason = dimensionEvaluation.Reason,
            Details = dimensionEvaluation.Items
        };

        var filterEvaluation = _filterScoringService.Evaluate(expected.Filters, runtime.Filters);
        var filters = new QueryPlanEvaluationSectionResult
        {
            Passed = filterEvaluation.Passed,
            Score = filterEvaluation.Score,
            Reason = filterEvaluation.Reason,
            Details = filterEvaluation.Items
        };

        var tables = EvaluateTables(expected, runtime);
        var joins = EvaluateJoins(expected, runtime);

        var queryShapeEvaluation = _queryShapeScoringService.Evaluate(expected, runtime);
        var shape = new QueryPlanEvaluationSectionResult
        {
            Passed = queryShapeEvaluation.Passed,
            Score = queryShapeEvaluation.Score,
            Reason = queryShapeEvaluation.Reason,
            Details = queryShapeEvaluation.Checks
        };

        var bindingConsistency = EvaluateBindingConsistency(expected, runtime);

        var raw = new QueryPlanEvaluationResult
        {
            CaseId = caseId,
            Intent = intent,
            Metrics = metrics,
            Dimensions = dimensions,
            Filters = filters,
            Tables = tables,
            Joins = joins,
            QueryShape = shape,
            BindingConsistency = bindingConsistency,
            MetricExpectations = expected.Metrics,
            ActualMetrics = runtime.Metrics
        };

        // C.13.1：最终 PASS / PARTIAL / FAIL / OverallScore 只能由统一 Scoring Contract 产生。
        return _evaluationScoringService.Score(raw);
    }

    /// <summary>
    /// C.4.8：独立评价 Semantic Applicability Resolution 与 Runtime QueryPlan 的物理绑定。
    /// 当前仍以 Golden Metrics[0] 作为主 Semantic Evidence；多 Metric 的完整闭环由 Metrics Scoring 独立保证。
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

        var goldenMetric = expected.Metrics?.FirstOrDefault();
        var runtimeMetric = runtime.Metrics?.FirstOrDefault();
        var resolution = applicability.Resolution;
        var resolutionExists = resolution is not null;
        var runtimeMetricExists = runtimeMetric is not null;

        if (goldenMetric is null)
        {
            return new QueryPlanSemanticEvidenceResult
            {
                CaseId = caseId,
                Passed = true,
                Reason = "Golden 未定义 Metric，不进行 Semantic Resolution Evidence 断言。",
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
            return SemanticEvidenceFail(caseId, goldenMetric.SemanticText, applicability, $"Golden Metric 需要 Semantic Resolution，但 Applicability 当前状态为 {applicability.State}。");

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
            Reason = "Semantic Resolution 与 Runtime QueryPlan 的主 Metric Field、Table、DataSource 物理绑定一致；其余 Metrics 由 Metrics Scoring 独立验证。",
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
        => new()
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

    private static QueryPlanEvaluationSectionResult EvaluateIntent(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (string.IsNullOrWhiteSpace(expected.IntentType)) return Pass("Golden 未指定 IntentType，不进行断言。");
        var actual = runtime.Intent?.IntentType.ToString();
        return string.Equals(expected.IntentType, actual, StringComparison.OrdinalIgnoreCase)
            ? Pass($"IntentType 匹配：{actual}。")
            : Fail($"期望 IntentType={expected.IntentType}，实际为 {actual ?? "null"}。");
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

    private QueryPlanEvaluationSectionResult EvaluateJoins(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.Joins is null) return Pass("Golden 未指定 Joins，不进行断言。");
        var actual = runtime.Joins ?? new List<QueryJoin>();
        var result = _joinScoringService.Evaluate(expected.Joins, actual);
        return new QueryPlanEvaluationSectionResult
        {
            Passed = result.Passed,
            Score = result.Score,
            Reason = result.Reason,
            Details = result.Items
        };
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

    private static QueryPlanEvaluationSectionResult Pass(string reason) => new() { Passed = true, Score = 1d, Reason = reason };
    private static QueryPlanEvaluationSectionResult Fail(string reason) => new() { Passed = false, Score = 0d, Reason = reason };
}
