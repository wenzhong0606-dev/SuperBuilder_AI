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
        var metrics = new QueryPlanEvaluationSectionResult { Passed = metricEvaluation.Passed, Score = metricEvaluation.Score, Reason = metricEvaluation.Reason, Details = metricEvaluation.Items };
        var dimensionEvaluation = _dimensionScoringService.Evaluate(expected.Dimensions, runtime.Dimensions);
        var dimensions = new QueryPlanEvaluationSectionResult { Passed = dimensionEvaluation.Passed, Score = dimensionEvaluation.Score, Reason = dimensionEvaluation.Reason, Details = dimensionEvaluation.Items };
        var filterEvaluation = _filterScoringService.Evaluate(expected.Filters, runtime.Filters);
        var filters = new QueryPlanEvaluationSectionResult { Passed = filterEvaluation.Passed, Score = filterEvaluation.Score, Reason = filterEvaluation.Reason, Details = filterEvaluation.Items };
        var tables = EvaluateTables(expected, runtime);
        var joins = EvaluateJoins(expected, runtime);
        var queryShapeEvaluation = _queryShapeScoringService.Evaluate(expected, runtime);
        var shape = new QueryPlanEvaluationSectionResult { Passed = queryShapeEvaluation.Passed, Score = queryShapeEvaluation.Score, Reason = queryShapeEvaluation.Reason, Details = queryShapeEvaluation.Checks };
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

        return _evaluationScoringService.Score(raw);
    }

    /// <summary>
    /// C.13.2：独立评价 Semantic Applicability Resolution 与 Runtime QueryPlan 的物理绑定。
    /// 不修改 Structural OverallScore；Semantic Evidence 作为 Confidence / Decision Gate 的独立安全证据。
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

        var metricResolutions = applicability.Resolution is null
            ? Array.Empty<SemanticApplicabilityResolution>()
            : new[] { applicability.Resolution };
        var metricEvidence = EvaluateMetrics(expected, runtime, metricResolutions);
        var dimensionEvidence = EvaluateDimensions(expected, runtime, applicability.DimensionResolutions);
        var filterEvidence = EvaluateFilters(expected, runtime, applicability.FilterResolutions);
        var tableEvidence = EvaluateTablesSemantic(expected, runtime, applicability);
        var allEvidence = metricEvidence.Concat(dimensionEvidence).Concat(filterEvidence).Concat(tableEvidence).ToList();

        var hasSemanticExpectation = expected.Metrics?.Count > 0
                                      || expected.Dimensions?.Count > 0
                                      || expected.Filters?.Count > 0
                                      || expected.Tables is not null;

        if (!string.Equals(applicability.CaseId, caseId, StringComparison.OrdinalIgnoreCase))
            return SemanticEvidenceFailure(caseId, expected.Metrics?.FirstOrDefault()?.SemanticText ?? string.Empty, applicability, "Applicability CaseId 与当前 Evaluation CaseId 不一致。", metricEvidence, dimensionEvidence, filterEvidence, tableEvidence);

        if (!hasSemanticExpectation)
        {
            return new QueryPlanSemanticEvidenceResult
            {
                CaseId = caseId,
                Passed = true,
                Reason = "Golden 未定义需要 Semantic Resolution Evidence 的字段，不进行 Semantic Evidence 断言。",
                ApplicabilityState = applicability.State,
                ResolutionExists = applicability.Resolution is not null,
                Metrics = metricEvidence,
                Dimensions = dimensionEvidence,
                Filters = filterEvidence,
                Tables = tableEvidence
            };
        }

        if (!string.Equals(applicability.State, "Resolved", StringComparison.OrdinalIgnoreCase))
            return SemanticEvidenceFailure(caseId, expected.Metrics?.FirstOrDefault()?.SemanticText ?? applicability.MetricSemanticText, applicability, $"Golden Semantic Evidence 需要 Resolution，但 Applicability 当前状态为 {applicability.State}。", metricEvidence, dimensionEvidence, filterEvidence, tableEvidence);

        var failedEvidence = allEvidence.FirstOrDefault(x => !x.Passed);
        if (failedEvidence is not null)
            return SemanticEvidenceFailure(caseId, failedEvidence.SemanticText, applicability, failedEvidence.Reason, metricEvidence, dimensionEvidence, filterEvidence, tableEvidence);

        var scores = allEvidence.Where(x => x.ResolutionScore.HasValue).Select(x => x.ResolutionScore!.Value).ToList();
        var legacyMetric = metricEvidence.FirstOrDefault();
        return new QueryPlanSemanticEvidenceResult
        {
            CaseId = caseId,
            Passed = true,
            Reason = "Semantic Resolution 与 Runtime QueryPlan 的 Metric、Dimension、Filter、Table 物理绑定一致。",
            GoldenSemanticText = legacyMetric?.SemanticText ?? applicability.MetricSemanticText,
            ApplicabilityState = applicability.State,
            ResolutionExists = applicability.Resolution is not null,
            RuntimeMetricExists = runtime.Metrics?.Count > 0,
            MetricFieldMatchesResolution = legacyMetric?.BindingMatched ?? true,
            TableBindingMatchesResolution = tableEvidence.Count == 0 || tableEvidence.All(x => x.Passed),
            DataSourceBindingMatchesResolution = tableEvidence.Count == 0 || tableEvidence.All(x => x.RuntimeDataSourceId == x.ResolvedDataSourceId),
            ResolutionScore = scores.Count == 0 ? applicability.Resolution?.Score : scores.Average(),
            Metrics = metricEvidence,
            Dimensions = dimensionEvidence,
            Filters = filterEvidence,
            Tables = tableEvidence
        };
    }

    private static IReadOnlyList<QueryPlanSemanticBindingEvidence> EvaluateMetrics(GoldenQueryExpectation expected, QueryPlan runtime, IReadOnlyList<SemanticApplicabilityResolution> resolutions)
    {
        var goldenMetrics = expected.Metrics ?? new List<GoldenMetricExpectation>();
        var runtimeMetrics = runtime.Metrics ?? new List<QueryMetric>();
        var results = new List<QueryPlanSemanticBindingEvidence>();
        for (var i = 0; i < goldenMetrics.Count; i++)
        {
            var golden = goldenMetrics[i];
            var resolution = i < resolutions.Count ? resolutions[i] : null;
            var runtimeMetric = FindRuntimeMetric(golden, runtimeMetrics, i);
            if (resolution is null)
            {
                results.Add(FailEvidence(golden.SemanticText, "Golden Metric 没有对应的 Semantic Resolution。"));
                continue;
            }
            if (runtimeMetric is null)
            {
                results.Add(FailEvidence(golden.SemanticText, "Golden Metric 没有对应的 Runtime Metric.") with { ResolutionExists = true, ResolvedColumnId = resolution.ColumnId, ResolvedColumn = resolution.Column, ResolvedTableId = resolution.TableId, ResolvedTable = resolution.Table, ResolvedDataSourceId = resolution.DataSourceId, ResolutionScore = resolution.Score });
                continue;
            }
            var fieldMatches = string.IsNullOrWhiteSpace(resolution.Column)
                ? resolution.ColumnId <= 0 || string.IsNullOrWhiteSpace(runtimeMetric.Field)
                : string.Equals(runtimeMetric.Field, resolution.Column, StringComparison.OrdinalIgnoreCase);
            results.Add(new QueryPlanSemanticBindingEvidence
            {
                SemanticText = golden.SemanticText,
                ResolutionExists = true,
                ResolvedColumnId = resolution.ColumnId,
                ResolvedColumn = resolution.Column,
                ResolvedTableId = resolution.TableId,
                ResolvedTable = resolution.Table,
                ResolvedDataSourceId = resolution.DataSourceId,
                RuntimeColumn = runtimeMetric.Field,
                RuntimeTableId = FindRuntimeTable(runtime, resolution)?.MetadataTableId,
                RuntimeDataSourceId = runtime.DataSourceId,
                BindingMatched = fieldMatches,
                ResolutionScore = resolution.Score,
                Passed = fieldMatches,
                Reason = fieldMatches ? "Runtime Metric Field 与 Semantic Resolution.Column 一致。" : $"Runtime Metric Field={runtimeMetric.Field} 与 Resolution.Column={resolution.Column} 不一致。"
            });
        }
        return results;
    }

    private static IReadOnlyList<QueryPlanSemanticBindingEvidence> EvaluateDimensions(GoldenQueryExpectation expected, QueryPlan runtime, IReadOnlyList<SemanticApplicabilityDimensionResolution> resolutions)
    {
        var goldenDimensions = expected.Dimensions ?? new List<GoldenDimensionExpectation>();
        var runtimeDimensions = runtime.Dimensions ?? new List<QueryDimension>();
        var results = new List<QueryPlanSemanticBindingEvidence>();
        for (var i = 0; i < goldenDimensions.Count; i++)
        {
            var golden = goldenDimensions[i];
            var resolution = i < resolutions.Count ? resolutions[i] : null;
            var runtimeDimension = i < runtimeDimensions.Count ? runtimeDimensions[i] : null;
            if (resolution is null) { results.Add(FailEvidence(golden.SemanticText, "Golden Dimension 没有对应的 Semantic Resolution。")); continue; }
            if (runtimeDimension is null)
            {
                results.Add(FailEvidence(golden.SemanticText, "Golden Dimension 没有对应的 Runtime Dimension。") with { ResolutionExists = true, ResolvedColumnId = resolution.ColumnId, ResolvedColumn = resolution.Column, ResolvedTableId = resolution.TableId, ResolvedTable = resolution.Table, ResolvedDataSourceId = resolution.DataSourceId, ResolutionScore = resolution.Score });
                continue;
            }
            var columnMatches = runtimeDimension.MetadataColumnId > 0 && runtimeDimension.MetadataColumnId == resolution.ColumnId;
            var nameMatches = string.IsNullOrWhiteSpace(resolution.Column) || string.Equals(runtimeDimension.ColumnName, resolution.Column, StringComparison.OrdinalIgnoreCase);
            var binding = columnMatches && nameMatches;
            results.Add(new QueryPlanSemanticBindingEvidence
            {
                SemanticText = golden.SemanticText,
                ResolutionExists = true,
                ResolvedColumnId = resolution.ColumnId,
                ResolvedColumn = resolution.Column,
                ResolvedTableId = resolution.TableId,
                ResolvedTable = resolution.Table,
                ResolvedDataSourceId = resolution.DataSourceId,
                RuntimeColumnId = runtimeDimension.MetadataColumnId,
                RuntimeColumn = runtimeDimension.ColumnName,
                RuntimeTableId = (runtime.Tables ?? new List<QueryTable>()).FirstOrDefault(x => x.MetadataTableId == resolution.TableId && x.DataSourceId == resolution.DataSourceId)?.MetadataTableId,
                RuntimeDataSourceId = runtime.DataSourceId,
                BindingMatched = binding,
                ResolutionScore = resolution.Score,
                Passed = binding,
                Reason = binding ? "Runtime Dimension MetadataColumnId / ColumnName 与 Semantic Resolution 一致。" : "Runtime Dimension 的物理列绑定与 Semantic Resolution 不一致。"
            });
        }
        return results;
    }

    private static IReadOnlyList<QueryPlanSemanticBindingEvidence> EvaluateFilters(GoldenQueryExpectation expected, QueryPlan runtime, IReadOnlyList<SemanticApplicabilityFilterResolution> resolutions)
    {
        var goldenFilters = expected.Filters ?? new List<GoldenFilterExpectation>();
        var runtimeFilters = runtime.Filters ?? new List<QueryFilter>();
        var results = new List<QueryPlanSemanticBindingEvidence>();
        for (var i = 0; i < goldenFilters.Count; i++)
        {
            var golden = goldenFilters[i];
            var resolution = i < resolutions.Count ? resolutions[i] : null;
            var runtimeFilter = i < runtimeFilters.Count ? runtimeFilters[i] : null;
            if (resolution is null) { results.Add(FailEvidence(golden.SemanticText, "Golden Filter 没有对应的 Semantic Resolution。")); continue; }
            if (runtimeFilter is null)
            {
                results.Add(FailEvidence(golden.SemanticText, "Golden Filter 没有对应的 Runtime Filter。") with { ResolutionExists = true, ResolvedColumnId = resolution.ColumnId, ResolvedColumn = resolution.Column, ResolvedTableId = resolution.TableId, ResolvedTable = resolution.Table, ResolvedDataSourceId = resolution.DataSourceId, ResolutionScore = resolution.Score });
                continue;
            }
            var binding = !string.IsNullOrWhiteSpace(resolution.Column) && string.Equals(runtimeFilter.Field, resolution.Column, StringComparison.OrdinalIgnoreCase);
            results.Add(new QueryPlanSemanticBindingEvidence
            {
                SemanticText = golden.SemanticText,
                ResolutionExists = true,
                ResolvedColumnId = resolution.ColumnId,
                ResolvedColumn = resolution.Column,
                ResolvedTableId = resolution.TableId,
                ResolvedTable = resolution.Table,
                ResolvedDataSourceId = resolution.DataSourceId,
                RuntimeColumn = runtimeFilter.Field,
                RuntimeTableId = FindRuntimeTable(runtime, resolution)?.MetadataTableId,
                RuntimeDataSourceId = runtime.DataSourceId,
                BindingMatched = binding,
                ResolutionScore = resolution.Score,
                Passed = binding,
                Reason = binding ? "Runtime Filter Field 与 Filter Semantic Resolution.Column 一致。" : $"Runtime Filter Field={runtimeFilter.Field} 与 Resolution.Column={resolution.Column} 不一致。"
            });
        }
        return results;
    }

    private static IReadOnlyList<QueryPlanSemanticBindingEvidence> EvaluateTablesSemantic(GoldenQueryExpectation expected, QueryPlan runtime, SemanticApplicabilityResult applicability)
    {
        if (expected.Tables is null || expected.Tables.Count == 0) return Array.Empty<QueryPlanSemanticBindingEvidence>();
        var resolutions = new List<(long TableId, long DataSourceId, string? Table, double? Score)>();
        if (applicability.Resolution is not null) resolutions.Add((applicability.Resolution.TableId, applicability.Resolution.DataSourceId, applicability.Resolution.Table, applicability.Resolution.Score));
        resolutions.AddRange(applicability.FilterResolutions.Select(x => (x.TableId, x.DataSourceId, x.Table, x.Score)));
        resolutions.AddRange(applicability.DimensionResolutions.Select(x => (x.TableId, x.DataSourceId, x.Table, x.Score)));
        var distinct = resolutions.GroupBy(x => new { x.TableId, x.DataSourceId }).Select(x => x.First()).ToList();
        var results = new List<QueryPlanSemanticBindingEvidence>();
        var runtimeTables = runtime.Tables ?? new List<QueryTable>();
        foreach (var resolution in distinct)
        {
            var table = runtimeTables.FirstOrDefault(x => x.MetadataTableId == resolution.TableId && x.DataSourceId == resolution.DataSourceId);
            var matched = table is not null && (string.IsNullOrWhiteSpace(resolution.Table) || string.Equals(table.TableName, resolution.Table, StringComparison.OrdinalIgnoreCase));
            results.Add(new QueryPlanSemanticBindingEvidence
            {
                SemanticText = resolution.Table ?? string.Empty,
                ResolutionExists = true,
                ResolvedTableId = resolution.TableId,
                ResolvedTable = resolution.Table,
                ResolvedDataSourceId = resolution.DataSourceId,
                RuntimeTableId = table?.MetadataTableId,
                RuntimeDataSourceId = table?.DataSourceId,
                BindingMatched = matched,
                ResolutionScore = resolution.Score,
                Passed = matched,
                Reason = matched ? "Runtime Table 与 Semantic Resolution 的 TableId / DataSourceId / TableName 一致。" : "Runtime Table 与 Semantic Resolution 的物理绑定不一致。"
            });
        }
        if (distinct.Count == 0 && expected.Tables.Count > 0) results.Add(FailEvidence(string.Empty, "Golden 明确要求 Tables，但 Applicability 没有提供可验证的 Table Resolution。"));
        return results;
    }

    private static QueryMetric? FindRuntimeMetric(GoldenMetricExpectation golden, IReadOnlyList<QueryMetric> runtimeMetrics, int index)
    {
        if (index < runtimeMetrics.Count && string.Equals(runtimeMetrics[index].Name, golden.SemanticText, StringComparison.OrdinalIgnoreCase)) return runtimeMetrics[index];
        return runtimeMetrics.FirstOrDefault(x => string.Equals(x.Name, golden.SemanticText, StringComparison.OrdinalIgnoreCase));
    }

    private static QueryTable? FindRuntimeTable(QueryPlan runtime, SemanticApplicabilityResolution resolution)
        => (runtime.Tables ?? new List<QueryTable>()).FirstOrDefault(x => x.MetadataTableId == resolution.TableId && x.DataSourceId == resolution.DataSourceId);

    private static QueryPlanSemanticBindingEvidence FailEvidence(string semanticText, string reason) => new() { SemanticText = semanticText, Passed = false, BindingMatched = false, Reason = reason };

    private static QueryPlanSemanticEvidenceResult SemanticEvidenceFailure(string caseId, string semanticText, SemanticApplicabilityResult applicability, string reason, IReadOnlyList<QueryPlanSemanticBindingEvidence> metrics, IReadOnlyList<QueryPlanSemanticBindingEvidence> dimensions, IReadOnlyList<QueryPlanSemanticBindingEvidence> filters, IReadOnlyList<QueryPlanSemanticBindingEvidence> tables)
    {
        var first = metrics.Concat(dimensions).Concat(filters).Concat(tables).FirstOrDefault();
        return new QueryPlanSemanticEvidenceResult
        {
            CaseId = caseId,
            Passed = false,
            Reason = reason,
            GoldenSemanticText = semanticText,
            ApplicabilityState = applicability.State,
            ResolutionExists = applicability.Resolution is not null,
            RuntimeMetricExists = false,
            MetricFieldMatchesResolution = first?.BindingMatched ?? false,
            TableBindingMatchesResolution = tables.Count == 0 || tables.All(x => x.Passed),
            DataSourceBindingMatchesResolution = tables.Count == 0 || tables.All(x => x.RuntimeDataSourceId == x.ResolvedDataSourceId),
            ResolutionScore = applicability.Resolution?.Score,
            Metrics = metrics,
            Dimensions = dimensions,
            Filters = filters,
            Tables = tables
        };
    }

    private static QueryPlanEvaluationSectionResult EvaluateIntent(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        if (string.IsNullOrWhiteSpace(expected.IntentType)) return Pass("Golden 未指定 IntentType，不进行断言。");
        var actual = runtime.Intent?.IntentType.ToString();
        return string.Equals(expected.IntentType, actual, StringComparison.OrdinalIgnoreCase) ? Pass($"IntentType 匹配：{actual}。") : Fail($"期望 IntentType={expected.IntentType}，实际为 {actual ?? "null"}。");
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
        return new QueryPlanEvaluationSectionResult { Passed = result.Passed, Score = result.Score, Reason = result.Reason, Details = result.Items };
    }

    private static QueryPlanEvaluationSectionResult EvaluateBindingConsistency(GoldenQueryExpectation expected, QueryPlan runtime)
    {
        var tables = runtime.Tables ?? new List<QueryTable>();
        var joins = runtime.Joins ?? new List<QueryJoin>();
        if (runtime.DataSourceId > 0 && tables.Any(table => table.DataSourceId > 0 && table.DataSourceId != runtime.DataSourceId)) return Fail("Binding 一致性失败：QueryPlan.Tables 存在与 QueryPlan.DataSourceId 不一致的数据源。");
        var duplicateTableIds = tables.Where(table => table.MetadataTableId > 0).GroupBy(table => table.MetadataTableId).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        if (duplicateTableIds.Count > 0) return Fail($"Binding 一致性失败：存在重复 MetadataTableId：{string.Join(", ", duplicateTableIds)}。");
        foreach (var join in joins)
            if (!tables.Any(table => table.MetadataTableId == join.LeftTableId) || !tables.Any(table => table.MetadataTableId == join.RightTableId)) return Fail($"Binding 一致性失败：Join 引用了 QueryPlan.Tables 中不存在的表，LeftTableId={join.LeftTableId}，RightTableId={join.RightTableId}。");
        if (expected.Tables is not null && tables.Count != expected.Tables.Count) return Fail($"Binding 一致性失败：Golden Tables={expected.Tables.Count}，Runtime Tables={tables.Count}。");
        if (expected.Joins is not null && joins.Count != expected.Joins.Count) return Fail($"Binding 一致性失败：Golden Joins={expected.Joins.Count}，Runtime Joins={joins.Count}。");
        return Pass($"Binding 一致性通过：DataSource={runtime.DataSourceId}，Tables={tables.Count}，Joins={joins.Count}，无重复表且所有 Join 均引用已绑定表。");
    }

    private static QueryPlanEvaluationSectionResult Pass(string reason) => new() { Passed = true, Score = 1d, Reason = reason };
    private static QueryPlanEvaluationSectionResult Fail(string reason) => new() { Passed = false, Score = 0d, Reason = reason };
}
