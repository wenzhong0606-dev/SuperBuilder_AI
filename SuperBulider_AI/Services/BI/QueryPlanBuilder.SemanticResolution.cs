using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

public partial class QueryPlanBuilder
{
    /// <summary>
    /// 使用已经完成 Semantic Applicability Resolution 的绑定构建 QueryPlan。
    /// Resolution 中已经确认的字段只允许直接绑定，不再通过 ResolveColumn 二次猜测。
    /// </summary>
    public async Task<QueryPlan> BuildAsync(
        QueryIntent intent,
        QueryPlanSemanticResolution? resolution)
    {
        QueryPlan plan;

        if (resolution is not null
            && resolution.Dimensions.Count > 0
            && resolution.Metric is not null
            && resolution.Dimensions.Any(x => x.TableId > 0 && x.TableId != resolution.Metric.TableId))
        {
            // Semantic Applicability 已经确认 Dimension 属于不同物理表时，
            // 不应让原始单表 ResolveColumn 再次尝试把 Dimension 映射到主表。
            // 原始 BuildAsync(intent) 的 Dimension 解析只适用于单表候选；
            // 跨表 Dimension 的最终物理绑定由 Resolution 负责。
            // 因此暂时移除 Dimension，仅完成 Metric / Filter / 主表 / 基础 Join 构建，
            // 随后由 ApplyDimensionResolutions 与 ApplyResolvedJoinInference 补齐已确认绑定。
            var originalDimensions = intent.Dimensions;
            try
            {
                intent.Dimensions = new List<string>();
                plan = await BuildAsync(intent);
            }
            finally
            {
                intent.Dimensions = originalDimensions;
            }
        }
        else
        {
            plan = await BuildAsync(intent);
        }

        if (resolution is null)
            return plan;

        NormalizeToResolvedTables(plan, resolution);

        ApplyMetricResolution(plan, resolution.Metric);
        ApplyFilterResolutions(plan, resolution.Filters);

        // 原始 BuildAsync 在跨表场景下被刻意跳过了 Dimension，因此这里必须先
        // 建立与 Resolution 一一对应的 Runtime Dimension 占位，再执行最终物理绑定。
        // 否则 ApplyDimensionResolutions 会把 Resolution=1 / Runtime=0 误判为 Binding Drift。
        EnsureResolvedDimensions(plan, resolution.Dimensions);
        ApplyDimensionResolutions(plan, resolution.Dimensions);

        ApplyOrderResolutions(plan, resolution.Orders);

        // Phase 2.6：当 Semantic Applicability 已经稳定解析出 Metric 与 Dimension
        // 分属不同物理表时，必须在最终 QueryPlan 中补齐两表之间的 Join。
        // 原有 BuildAsync(intent) 的 JoinInference 依赖初始 Metadata Search 结果，
        // 在 Resolution 已经补入第二张表的场景下可能没有足够的 Join 候选，因此这里
        // 基于已确认的物理表再次执行一次受限 JoinInference。该过程不会猜测新的业务表，
        // 只允许连接 Resolution 已确认的表。
        await ApplyResolvedJoinInferenceAsync(plan, resolution);

        return plan;
    }

    private static void EnsureResolvedDimensions(
        QueryPlan plan,
        IReadOnlyList<QueryPlanDimensionResolution> bindings)
    {
        if (bindings.Count == 0)
            return;

        // 跨表 Dimension 的原始解析被跳过，因此按 Resolution 顺序创建 Runtime Dimension。
        // 这里只创建结构占位，不进行任何新的语义猜测。
        while (plan.Dimensions.Count < bindings.Count)
        {
            plan.Dimensions.Add(new QueryDimension());
        }
    }

    /// <summary>
    /// Resolution 已经确认了本次查询需要的物理表。
    /// Builder 原始流程中的 JoinInference 属于候选推断，不能在已有稳定 Resolution 时
    /// 把未经语义绑定确认的表继续带入最终 QueryPlan。
    /// </summary>
    private static void NormalizeToResolvedTables(
        QueryPlan plan,
        QueryPlanSemanticResolution resolution)
    {
        var resolvedTableIds = new HashSet<long>();

        if (resolution.Metric is not null)
            resolvedTableIds.Add(resolution.Metric.TableId);

        foreach (var binding in resolution.Filters)
            resolvedTableIds.Add(binding.TableId);

        foreach (var binding in resolution.Dimensions)
            resolvedTableIds.Add(binding.TableId);

        foreach (var binding in resolution.Orders)
            resolvedTableIds.Add(binding.TableId);

        if (resolvedTableIds.Count == 0)
            return;

        plan.Joins.RemoveAll(join =>
            !resolvedTableIds.Contains(join.LeftTableId)
            || !resolvedTableIds.Contains(join.RightTableId));

        plan.Tables.RemoveAll(table =>
            !resolvedTableIds.Contains(table.MetadataTableId));
    }

    private async Task ApplyResolvedJoinInferenceAsync(
        QueryPlan plan,
        QueryPlanSemanticResolution resolution)
    {
        if (resolution.Metric is null || resolution.Dimensions.Count == 0)
            return;

        var metricTableId = resolution.Metric.TableId;
        if (metricTableId <= 0)
            return;

        foreach (var dimension in resolution.Dimensions)
        {
            if (dimension.TableId <= 0 || dimension.TableId == metricTableId)
                continue;

            var searchTexts = new List<string>();

            if (!string.IsNullOrWhiteSpace(dimension.SemanticText))
                searchTexts.Add(dimension.SemanticText);

            if (!string.IsNullOrWhiteSpace(dimension.SemanticText))
                searchTexts.Add($"{dimension.SemanticText}ID");

            if (!string.IsNullOrWhiteSpace(resolution.Metric.Table))
                searchTexts.Add(resolution.Metric.Table);

            var metadataResults = new List<SuperBuilder_AI.Models.AI.MetadataSemanticSearchResult>();

            foreach (var searchText in searchTexts.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var results = await _metadataSearch.SearchAsync(searchText, 20);
                metadataResults.AddRange(results);
            }

            metadataResults = metadataResults
                .Where(x => x.Table is not null && x.Column is not null)
                .GroupBy(x => $"{x.Table!.Id}|{x.Column!.Id}|{x.Semantic?.Id ?? 0}", StringComparer.OrdinalIgnoreCase)
                .Select(x => x.OrderByDescending(item => item.Score).First())
                .ToList();

            if (metadataResults.Count == 0)
                continue;

            var candidates = await _joinInference.InferAsync(metadataResults);

            var candidate = candidates
                .Where(x =>
                    (x.LeftTableId == metricTableId && x.RightTableId == dimension.TableId)
                    || (x.LeftTableId == dimension.TableId && x.RightTableId == metricTableId))
                .OrderByDescending(x => x.Confidence)
                .FirstOrDefault();

            if (candidate is null)
                continue;

            if (candidate.LeftTableId == candidate.RightTableId)
                continue;

            if (!plan.Tables.Any(x => x.MetadataTableId == candidate.LeftTableId)
                || !plan.Tables.Any(x => x.MetadataTableId == candidate.RightTableId))
            {
                continue;
            }

            var exists = plan.Joins.Any(x =>
                (x.LeftTableId == candidate.LeftTableId
                 && x.LeftColumnId == candidate.LeftColumnId
                 && x.RightTableId == candidate.RightTableId
                 && x.RightColumnId == candidate.RightColumnId)
                ||
                (x.LeftTableId == candidate.RightTableId
                 && x.LeftColumnId == candidate.RightColumnId
                 && x.RightTableId == candidate.LeftTableId
                 && x.RightColumnId == candidate.LeftColumnId));

            if (exists)
                continue;

            plan.Joins.Add(new QueryJoin
            {
                LeftTableId = candidate.LeftTableId,
                LeftColumnId = candidate.LeftColumnId,
                RightTableId = candidate.RightTableId,
                RightColumnId = candidate.RightColumnId,
                LeftTableName = candidate.LeftTableName,
                LeftColumnName = candidate.LeftColumnName,
                RightTableName = candidate.RightTableName,
                RightColumnName = candidate.RightColumnName,
                JoinType = "INNER"
            });
        }
    }

    private static void ApplyMetricResolution(QueryPlan plan, QueryPlanMetricResolution? binding)
    {
        if (binding is null)
            return;

        ValidateResolutionColumn(binding.ColumnId, binding.Column, "Metric");
        var matchedMetric = false;

        foreach (var metric in plan.Metrics)
        {
            if (!MatchesMetric(metric, binding.SemanticText))
                continue;

            matchedMetric = true;
            metric.Field = binding.Column;
            EnsureResolutionTable(plan, binding.TableId, binding.DataSourceId, binding.Table, "Metric");
            EnsureResolutionField(plan, binding.ColumnId, binding.Column, metric.GetAggregation().ToString());
        }

        if (!matchedMetric)
            throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：未找到与已解析 Metric“{binding.SemanticText}”对应的 Runtime Metric。");
    }

    private static void ApplyFilterResolutions(QueryPlan plan, IReadOnlyList<QueryPlanFilterResolution> bindings)
    {
        if (bindings.Count == 0)
            return;

        if (plan.Filters.Count != bindings.Count)
            throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Filter 数量不一致，Resolution={bindings.Count}，Runtime={plan.Filters.Count}。");

        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            ValidateResolutionColumn(binding.ColumnId, binding.Column, $"Filter[{i}]");
            EnsureResolutionTable(plan, binding.TableId, binding.DataSourceId, binding.Table, $"Filter[{i}]");
            plan.Filters[i].Field = binding.Column;
        }
    }

    private static void ApplyDimensionResolutions(QueryPlan plan, IReadOnlyList<QueryPlanDimensionResolution> bindings)
    {
        if (bindings.Count == 0)
            return;

        if (plan.Dimensions.Count != bindings.Count)
            throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Dimension 数量不一致，Resolution={bindings.Count}，Runtime={plan.Dimensions.Count}。");

        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            ValidateResolutionColumn(binding.ColumnId, binding.Column, $"Dimension[{i}]");
            EnsureResolutionTable(plan, binding.TableId, binding.DataSourceId, binding.Table, $"Dimension[{i}]");
            plan.Dimensions[i].MetadataColumnId = binding.ColumnId;
            plan.Dimensions[i].ColumnName = binding.Column;
        }
    }

    private static void ApplyOrderResolutions(QueryPlan plan, IReadOnlyList<QueryPlanOrderResolution> bindings)
    {
        if (bindings.Count == 0)
            return;

        if (plan.Orders.Count != bindings.Count)
            throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Order 数量不一致，Resolution={bindings.Count}，Runtime={plan.Orders.Count}。");

        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            ValidateResolutionColumn(binding.ColumnId, binding.Column, $"Order[{i}]");
            EnsureResolutionTable(plan, binding.TableId, binding.DataSourceId, binding.Table, $"Order[{i}]");
            plan.Orders[i].MetadataColumnId = binding.ColumnId;
            plan.Orders[i].Field = binding.Column;
        }
    }

    private static void EnsureResolutionTable(QueryPlan plan, long tableId, long dataSourceId, string tableName, string bindingType)
    {
        if (tableId <= 0 || dataSourceId <= 0 || string.IsNullOrWhiteSpace(tableName))
            throw new InvalidOperationException($"Semantic Resolution 缺少有效的 {bindingType} Table Binding。");

        var existing = plan.Tables.FirstOrDefault(table => table.MetadataTableId == tableId);
        if (existing is not null)
        {
            if (existing.DataSourceId != dataSourceId)
                throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：{bindingType} TableId={tableId} 的 DataSourceId 不一致，Resolution={dataSourceId}，Runtime={existing.DataSourceId}。");
            return;
        }

        plan.Tables.Add(new QueryTable
        {
            MetadataTableId = tableId,
            DataSourceId = dataSourceId,
            TableName = tableName
        });
    }

    private static void EnsureResolutionField(QueryPlan plan, long columnId, string columnName, string aggregation)
    {
        var existing = plan.Fields.FirstOrDefault(field => field.MetadataColumnId == columnId);
        if (existing is not null)
        {
            existing.ColumnName = columnName;
            if (string.IsNullOrWhiteSpace(existing.Aggregation) || existing.Aggregation.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                existing.Aggregation = aggregation;
            return;
        }

        plan.Fields.Add(new QueryField
        {
            MetadataColumnId = columnId,
            ColumnName = columnName,
            Aggregation = aggregation
        });
    }

    private static void ValidateResolutionColumn(long columnId, string column, string bindingType)
    {
        if (columnId <= 0 || string.IsNullOrWhiteSpace(column))
            throw new InvalidOperationException($"Semantic Resolution 缺少有效的 {bindingType} Column Binding。");
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
