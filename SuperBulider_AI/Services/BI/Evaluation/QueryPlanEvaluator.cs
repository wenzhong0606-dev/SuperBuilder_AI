using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

public sealed class QueryPlanEvaluator
{
    private readonly QueryPlanJoinScoringService _joinScoringService;
    private readonly QueryPlanMetricScoringService _metricScoringService;
    private readonly QueryPlanDimensionScoringService _dimensionScoringService;
    private readonly QueryPlanFilterScoringService _filterScoringService;
    private readonly QueryPlanQueryShapeScoringService _queryShapeScoringService;
    private readonly QueryPlanEvaluationScoringService _evaluationScoringService;

    public QueryPlanEvaluator(QueryPlanJoinScoringService joinScoringService, QueryPlanMetricScoringService metricScoringService, QueryPlanDimensionScoringService dimensionScoringService, QueryPlanFilterScoringService filterScoringService, QueryPlanQueryShapeScoringService queryShapeScoringService, QueryPlanEvaluationScoringService evaluationScoringService)
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
        ArgumentNullException.ThrowIfNull(expected); ArgumentNullException.ThrowIfNull(runtime);
        var intent = EvaluateIntent(expected, runtime);
        var m = _metricScoringService.Evaluate(expected.Metrics, runtime.Metrics);
        var d = _dimensionScoringService.Evaluate(expected.Dimensions, runtime.Dimensions);
        var f = _filterScoringService.Evaluate(expected.Filters, runtime.Filters);
        var q = _queryShapeScoringService.Evaluate(expected, runtime);
        var raw = new QueryPlanEvaluationResult
        {
            CaseId = caseId, Intent = intent,
            Metrics = new() { Passed = m.Passed, Score = m.Score, Reason = m.Reason, Details = m.Items },
            Dimensions = new() { Passed = d.Passed, Score = d.Score, Reason = d.Reason, Details = d.Items },
            Filters = new() { Passed = f.Passed, Score = f.Score, Reason = f.Reason, Details = f.Items },
            Tables = EvaluateTables(expected, runtime), Joins = EvaluateJoins(expected, runtime),
            QueryShape = new() { Passed = q.Passed, Score = q.Score, Reason = q.Reason, Details = q.Checks },
            BindingConsistency = EvaluateBindingConsistency(expected, runtime), MetricExpectations = expected.Metrics, ActualMetrics = runtime.Metrics
        };
        return _evaluationScoringService.Score(raw);
    }

    public QueryPlanSemanticEvidenceResult EvaluateSemanticEvidence(string caseId, GoldenQueryExpectation expected, QueryPlan runtime, SemanticApplicabilityResult applicability)
    {
        ArgumentNullException.ThrowIfNull(expected); ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(applicability);
        var metricResolutions = applicability.MetricResolutions.Count > 0
            ? applicability.MetricResolutions.Select(ToBaseResolution).ToList()
            : applicability.Resolution is null ? new List<SemanticApplicabilityResolution>() : new() { applicability.Resolution };
        var metricEvidence = EvaluateMetrics(expected, runtime, metricResolutions);
        var dimensionEvidence = EvaluateDimensions(expected, runtime, applicability.DimensionResolutions);
        var filterEvidence = EvaluateFilters(expected, runtime, applicability.FilterResolutions);
        var tableEvidence = EvaluateTablesSemantic(expected, runtime, applicability);
        var all = metricEvidence.Concat(dimensionEvidence).Concat(filterEvidence).Concat(tableEvidence).ToList();

        if (!string.Equals(applicability.CaseId, caseId, StringComparison.OrdinalIgnoreCase)) return Failure(caseId, expected, applicability, "Applicability CaseId 与当前 Evaluation CaseId 不一致。", metricEvidence, dimensionEvidence, filterEvidence, tableEvidence);
        if (!string.Equals(applicability.State, "Resolved", StringComparison.OrdinalIgnoreCase)) return Failure(caseId, expected, applicability, $"Semantic Applicability 当前状态为 {applicability.State}，禁止继续通过 Semantic Evidence。", metricEvidence, dimensionEvidence, filterEvidence, tableEvidence);
        var failed = all.FirstOrDefault(x => !x.Passed);
        if (failed is not null) return Failure(caseId, expected, applicability, failed.Reason, metricEvidence, dimensionEvidence, filterEvidence, tableEvidence);

        var scores = all.Where(x => x.ResolutionScore.HasValue).Select(x => x.ResolutionScore!.Value).ToList();
        return new QueryPlanSemanticEvidenceResult
        {
            CaseId = caseId, Passed = true, Reason = "Metric、Dimension、Filter、Table 的 Golden SemanticText 均通过统一 Resolution 与 Runtime Physical Binding 校验。",
            GoldenSemanticText = metricEvidence.FirstOrDefault()?.SemanticText ?? applicability.MetricSemanticText,
            ApplicabilityState = applicability.State, ResolutionExists = metricResolutions.Count > 0, RuntimeMetricExists = runtime.Metrics.Count > 0,
            MetricFieldMatchesResolution = metricEvidence.All(x => x.Passed), TableBindingMatchesResolution = tableEvidence.Count == 0 || tableEvidence.All(x => x.Passed),
            DataSourceBindingMatchesResolution = all.Where(x => x.RuntimeDataSourceId.HasValue && x.ResolvedDataSourceId.HasValue).All(x => x.RuntimeDataSourceId == x.ResolvedDataSourceId),
            ResolutionScore = scores.Count == 0 ? null : scores.Average(), Metrics = metricEvidence, Dimensions = dimensionEvidence, Filters = filterEvidence, Tables = tableEvidence
        };
    }

    private static IReadOnlyList<QueryPlanSemanticBindingEvidence> EvaluateMetrics(GoldenQueryExpectation expected, QueryPlan runtime, IReadOnlyList<SemanticApplicabilityResolution> resolutions)
    {
        var golden = expected.Metrics ?? new(); var actual = runtime.Metrics ?? new List<QueryMetric>(); var result = new List<QueryPlanSemanticBindingEvidence>();
        for (var i = 0; i < golden.Count; i++)
        {
            var g = golden[i]; var r = i < resolutions.Count ? resolutions[i] : null; var a = i < actual.Count ? actual[i] : actual.FirstOrDefault(x => string.Equals(x.SemanticText, g.SemanticText, StringComparison.OrdinalIgnoreCase));
            if (r is null) { result.Add(Fail(g.SemanticText, "Golden Metric 没有对应的 Semantic Resolution。")); continue; }
            if (a is null) { result.Add(Fail(g.SemanticText, "Golden Metric 没有对应的 Runtime Metric。") with { ResolutionExists = true, ResolvedColumnId = r.ColumnId, ResolvedColumn = r.Column, ResolvedTableId = r.TableId, ResolvedTable = r.Table, ResolvedDataSourceId = r.DataSourceId, ResolutionScore = r.Score }); continue; }
            var binding = string.Equals(a.Field, r.Column, StringComparison.OrdinalIgnoreCase) && a.SemanticText.Equals(g.SemanticText, StringComparison.OrdinalIgnoreCase);
            result.Add(new() { SemanticText = g.SemanticText, ResolutionExists = true, ResolvedColumnId = r.ColumnId, ResolvedColumn = r.Column, ResolvedTableId = r.TableId, ResolvedTable = r.Table, ResolvedDataSourceId = r.DataSourceId, RuntimeColumn = a.Field, RuntimeTableId = FindTable(runtime, r)?.MetadataTableId, RuntimeDataSourceId = runtime.DataSourceId, BindingMatched = binding, ResolutionScore = r.Score, Passed = binding, Reason = binding ? "Runtime Metric SemanticText / Field 与 Resolution 一致。" : "Runtime Metric 的 SemanticText 或物理 Field 与 Resolution 不一致。" });
        }
        return result;
    }

    private static IReadOnlyList<QueryPlanSemanticBindingEvidence> EvaluateDimensions(GoldenQueryExpectation expected, QueryPlan runtime, IReadOnlyList<SemanticApplicabilityDimensionResolution> resolutions)
    {
        var golden = expected.Dimensions ?? new(); var actual = runtime.Dimensions ?? new List<QueryDimension>(); var result = new List<QueryPlanSemanticBindingEvidence>();
        for (var i = 0; i < golden.Count; i++)
        {
            var g = golden[i]; var r = i < resolutions.Count ? resolutions[i] : null; var a = i < actual.Count ? actual[i] : null;
            if (r is null) { result.Add(Fail(g.SemanticText, "Golden Dimension 没有对应的 Semantic Resolution。")); continue; }
            if (a is null) { result.Add(Fail(g.SemanticText, "Golden Dimension 没有对应的 Runtime Dimension。") with { ResolutionExists = true, ResolvedColumnId = r.ColumnId, ResolvedColumn = r.Column, ResolvedTableId = r.TableId, ResolvedTable = r.Table, ResolvedDataSourceId = r.DataSourceId, ResolutionScore = r.Score }); continue; }
            var binding = a.MetadataColumnId == r.ColumnId && string.Equals(a.ColumnName, r.Column, StringComparison.OrdinalIgnoreCase) && string.Equals(a.SemanticText, g.SemanticText, StringComparison.OrdinalIgnoreCase);
            result.Add(new() { SemanticText = g.SemanticText, ResolutionExists = true, ResolvedColumnId = r.ColumnId, ResolvedColumn = r.Column, ResolvedTableId = r.TableId, ResolvedTable = r.Table, ResolvedDataSourceId = r.DataSourceId, RuntimeColumnId = a.MetadataColumnId, RuntimeColumn = a.ColumnName, RuntimeTableId = FindTable(runtime, r)?.MetadataTableId, RuntimeDataSourceId = runtime.DataSourceId, BindingMatched = binding, ResolutionScore = r.Score, Passed = binding, Reason = binding ? "Runtime Dimension SemanticText / MetadataColumnId / ColumnName 与 Resolution 一致。" : "Runtime Dimension 的语义或物理绑定与 Resolution 不一致。" });
        }
        return result;
    }

    private static IReadOnlyList<QueryPlanSemanticBindingEvidence> EvaluateFilters(GoldenQueryExpectation expected, QueryPlan runtime, IReadOnlyList<SemanticApplicabilityFilterResolution> resolutions)
    {
        var golden = expected.Filters ?? new(); var actual = runtime.Filters ?? new List<QueryFilter>(); var result = new List<QueryPlanSemanticBindingEvidence>();
        for (var i = 0; i < golden.Count; i++)
        {
            var g = golden[i]; var r = i < resolutions.Count ? resolutions[i] : null; var a = i < actual.Count ? actual[i] : null;
            if (r is null) { result.Add(Fail(g.SemanticText, "Golden Filter 没有对应的 Semantic Resolution。")); continue; }
            if (a is null) { result.Add(Fail(g.SemanticText, "Golden Filter 没有对应的 Runtime Filter。") with { ResolutionExists = true, ResolvedColumnId = r.ColumnId, ResolvedColumn = r.Column, ResolvedTableId = r.TableId, ResolvedTable = r.Table, ResolvedDataSourceId = r.DataSourceId, ResolutionScore = r.Score }); continue; }
            var binding = string.Equals(a.SemanticText, g.SemanticText, StringComparison.OrdinalIgnoreCase) && string.Equals(a.Field, r.Column, StringComparison.OrdinalIgnoreCase);
            result.Add(new() { SemanticText = g.SemanticText, ResolutionExists = true, ResolvedColumnId = r.ColumnId, ResolvedColumn = r.Column, ResolvedTableId = r.TableId, ResolvedTable = r.Table, ResolvedDataSourceId = r.DataSourceId, RuntimeColumn = a.Field, RuntimeTableId = FindTable(runtime, r)?.MetadataTableId, RuntimeDataSourceId = runtime.DataSourceId, BindingMatched = binding, ResolutionScore = r.Score, Passed = binding, Reason = binding ? "Runtime Filter SemanticText / Field 与 Resolution 一致。" : "Runtime Filter 的语义或物理 Field 与 Resolution 不一致。" });
        }
        return result;
    }

    private static IReadOnlyList<QueryPlanSemanticBindingEvidence> EvaluateTablesSemantic(GoldenQueryExpectation expected, QueryPlan runtime, SemanticApplicabilityResult applicability)
    {
        if (expected.Tables is null) return Array.Empty<QueryPlanSemanticBindingEvidence>();
        var resolutions = applicability.TableResolutions;
        if (resolutions.Count != expected.Tables.Count) return new[] { Fail(string.Empty, $"Golden Table Resolution 数量不一致：Expected={expected.Tables.Count}，Resolved={resolutions.Count}。") };
        var result = new List<QueryPlanSemanticBindingEvidence>();
        for (var i = 0; i < expected.Tables.Count; i++)
        {
            var g = expected.Tables[i]; var r = resolutions[i]; var a = (runtime.Tables ?? new List<QueryTable>()).FirstOrDefault(x => x.MetadataTableId == r.TableId && x.DataSourceId == r.DataSourceId);
            var binding = a is not null && string.Equals(a.SemanticText, g.SemanticText, StringComparison.OrdinalIgnoreCase) && string.Equals(a.TableName, r.Table, StringComparison.OrdinalIgnoreCase);
            result.Add(new() { SemanticText = g.SemanticText, ResolutionExists = true, ResolvedTableId = r.TableId, ResolvedTable = r.Table, ResolvedDataSourceId = r.DataSourceId, RuntimeTableId = a?.MetadataTableId, RuntimeDataSourceId = a?.DataSourceId, BindingMatched = binding, ResolutionScore = r.Score, Passed = binding, Reason = binding ? "Runtime Table SemanticText / TableName 与 Table Resolution 一致。" : "Runtime Table 的语义或物理表绑定与 Table Resolution 不一致。" });
        }
        return result;
    }

    private static SemanticApplicabilityResolution ToBaseResolution(SemanticApplicabilityMetricResolution x) => new() { TableId = x.TableId, DataSourceId = x.DataSourceId, ColumnId = x.ColumnId, Table = x.Table, Column = x.Column, BusinessMeaning = x.BusinessMeaning, Score = x.Score };
    private static QueryTable? FindTable(QueryPlan runtime, SemanticApplicabilityResolution r) => (runtime.Tables ?? new List<QueryTable>()).FirstOrDefault(x => x.MetadataTableId == r.TableId && x.DataSourceId == r.DataSourceId);
    private static QueryPlanSemanticBindingEvidence Fail(string text, string reason) => new() { SemanticText = text, Passed = false, BindingMatched = false, Reason = reason };

    private static QueryPlanSemanticEvidenceResult Failure(string caseId, GoldenQueryExpectation expected, SemanticApplicabilityResult applicability, string reason, IReadOnlyList<QueryPlanSemanticBindingEvidence> m, IReadOnlyList<QueryPlanSemanticBindingEvidence> d, IReadOnlyList<QueryPlanSemanticBindingEvidence> f, IReadOnlyList<QueryPlanSemanticBindingEvidence> t)
        => new() { CaseId = caseId, Passed = false, Reason = reason, GoldenSemanticText = expected.Metrics?.FirstOrDefault()?.SemanticText ?? applicability.MetricSemanticText, ApplicabilityState = applicability.State, ResolutionExists = applicability.MetricResolutions.Count > 0 || applicability.Resolution is not null, MetricFieldMatchesResolution = m.Count > 0 && m.All(x => x.Passed), TableBindingMatchesResolution = t.Count == 0 || t.All(x => x.Passed), DataSourceBindingMatchesResolution = t.Count == 0 || t.All(x => x.RuntimeDataSourceId == x.ResolvedDataSourceId), Metrics = m, Dimensions = d, Filters = f, Tables = t };

    private static QueryPlanEvaluationSectionResult EvaluateIntent(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (string.IsNullOrWhiteSpace(expected.IntentType)) return Pass("Golden 未指定 IntentType，不进行断言。");
        var actual = runtime.Intent?.IntentType?.ToString();
        return string.Equals(expected.IntentType, actual, StringComparison.OrdinalIgnoreCase) ? Pass($"IntentType 匹配：{actual}。") : Fail($"期望 IntentType={expected.IntentType}，实际为 {actual ?? "null"}。");
    }

    private static QueryPlanEvaluationSectionResult EvaluateTables(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.Tables is null) return Pass("Golden 未指定 Tables，不进行断言。");
        var actual = runtime.Tables ?? new List<QueryTable>();
        if (expected.Tables.Count == 0) return actual.Count == 0 ? Pass("Golden 明确要求无 Tables，Runtime 为空。") : Fail($"Golden 明确要求无 Tables，实际存在 {actual.Count} 个 Table。");
        if (actual.Count != expected.Tables.Count) return Fail($"Tables 数量不匹配：期望 {expected.Tables.Count}，实际 {actual.Count}。");
        if (actual.Any(x => x.MetadataTableId <= 0 || x.DataSourceId <= 0 || string.IsNullOrWhiteSpace(x.TableName))) return Fail("Runtime Table 存在无效物理绑定。");
        return Pass($"Tables 数量与 Runtime 物理绑定完整性匹配：{actual.Count} 个 Table。");
    }

    private QueryPlanEvaluationSectionResult EvaluateJoins(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (expected.Joins is null) return Pass("Golden 未指定 Joins，不进行断言。");
        var r = _joinScoringService.Evaluate(expected.Joins, runtime.Joins ?? new List<QueryJoin>());
        return new() { Passed = r.Passed, Score = r.Score, Reason = r.Reason, Details = r.Items };
    }

    private static QueryPlanEvaluationSectionResult EvaluateBindingConsistency(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        var tables = runtime.Tables ?? new List<QueryTable>(); var joins = runtime.Joins ?? new List<QueryJoin>();
        if (runtime.DataSourceId > 0 && tables.Any(x => x.DataSourceId > 0 && x.DataSourceId != runtime.DataSourceId)) return Fail("Binding 一致性失败：存在与 QueryPlan.DataSourceId 不一致的 Table。");
        if (tables.Where(x => x.MetadataTableId > 0).GroupBy(x => x.MetadataTableId).Any(x => x.Count() > 1)) return Fail("Binding 一致性失败：存在重复 MetadataTableId。");
        if (joins.Any(j => !tables.Any(t => t.MetadataTableId == j.LeftTableId) || !tables.Any(t => t.MetadataTableId == j.RightTableId))) return Fail("Binding 一致性失败：Join 引用了不存在的 Table。");
        if (expected.Tables is not null && tables.Count != expected.Tables.Count) return Fail($"Binding 一致性失败：Golden Tables={expected.Tables.Count}，Runtime Tables={tables.Count}。");
        if (expected.Joins is not null && joins.Count != expected.Joins.Count) return Fail($"Binding 一致性失败：Golden Joins={expected.Joins.Count}，Runtime Joins={joins.Count}。");
        return Pass($"Binding 一致性通过：DataSource={runtime.DataSourceId}，Tables={tables.Count}，Joins={joins.Count}。");
    }

    private static QueryPlanEvaluationSectionResult Pass(string reason) => new() { Passed = true, Score = 1d, Reason = reason };
    private static QueryPlanEvaluationSectionResult Fail(string reason) => new() { Passed = false, Score = 0d, Reason = reason };
}
