using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

public partial class QueryPlanBuilder
{
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
        await ApplySecondaryMetricResolutionsAsync(plan, intent, resolution);
        ApplyFilterResolutions(plan, resolution.Filters);
        EnsureResolvedDimensions(plan, resolution.Dimensions);
        ApplyDimensionResolutions(plan, resolution.Dimensions);
        ApplyOrderResolutions(plan, resolution.Orders);
        await ApplyResolvedJoinInferenceAsync(plan, resolution);
        return plan;
    }

    private static void EnsureResolvedDimensions(QueryPlan plan, IReadOnlyList<QueryPlanDimensionResolution> bindings)
    {
        if (bindings.Count == 0) return;
        while (plan.Dimensions.Count < bindings.Count) plan.Dimensions.Add(new QueryDimension());
    }

    private static void NormalizeToResolvedTables(QueryPlan plan, QueryPlanSemanticResolution resolution)
    {
        var resolvedTableIds = new HashSet<long>();
        if (resolution.Metric is not null) resolvedTableIds.Add(resolution.Metric.TableId);
        foreach (var binding in resolution.Filters) resolvedTableIds.Add(binding.TableId);
        foreach (var binding in resolution.Dimensions) resolvedTableIds.Add(binding.TableId);
        foreach (var binding in resolution.Orders) resolvedTableIds.Add(binding.TableId);
        if (resolvedTableIds.Count == 0) return;
        plan.Joins.RemoveAll(join => !resolvedTableIds.Contains(join.LeftTableId) || !resolvedTableIds.Contains(join.RightTableId));
        plan.Tables.RemoveAll(table => !resolvedTableIds.Contains(table.MetadataTableId));
    }

    private async Task ApplySecondaryMetricResolutionsAsync(QueryPlan plan, QueryIntent intent, QueryPlanSemanticResolution resolution)
    {
        if (resolution.Metric is null || intent.Metrics.Count <= 1 || plan.Metrics.Count <= 1) return;

        for (var i = 0; i < intent.Metrics.Count && i < plan.Metrics.Count; i++)
        {
            var intentMetric = intent.Metrics[i];
            var runtimeMetric = plan.Metrics[i];

            if (MatchesMetric(runtimeMetric, resolution.Metric.SemanticText)
                || string.Equals(intentMetric.Name, resolution.Metric.SemanticText, StringComparison.OrdinalIgnoreCase))
                continue;

            if (resolution.Metric.TableId <= 0) continue;

            // C.13：若 QueryIntent 已经给出物理 Field，则先进行同表精确列绑定。
            // 例如 amount / quantity，不再让全局最高相似语义列决定第二指标。
            var explicitField = intentMetric.Field?.Trim();
            if (!string.IsNullOrWhiteSpace(explicitField))
            {
                var exactCandidates = await _metadataSearch.SearchAsync(explicitField, 20);
                var exactColumn = exactCandidates
                    .Where(x => x.Table is not null
                        && x.Table.Id == resolution.Metric.TableId
                        && x.Column is not null
                        && string.Equals(x.Column.ColumnName, explicitField, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Score)
                    .Select(x => x.Column!)
                    .FirstOrDefault();

                if (exactColumn is not null)
                {
                    runtimeMetric.Field = exactColumn.ColumnName ?? runtimeMetric.Field;
                    EnsureResolutionField(plan, exactColumn.Id, exactColumn.ColumnName ?? runtimeMetric.Field, runtimeMetric.GetAggregation().ToString());
                    continue;
                }
            }

            var searchText = !string.IsNullOrWhiteSpace(intentMetric.Name) ? intentMetric.Name : intentMetric.Field;
            if (string.IsNullOrWhiteSpace(searchText)) continue;

            var candidates = await _metadataSearch.SearchAsync(searchText, 20);
            var tableCandidates = candidates
                .Where(x => x.Table is not null && x.Table.Id == resolution.Metric.TableId && x.Column is not null)
                .GroupBy(x => x.Column!.Id)
                .Select(g => g.OrderByDescending(x => x.Score).First())
                .ToList();

            if (tableCandidates.Count == 0) continue;

            var ranked = tableCandidates
                .Select(x => new
                {
                    Column = x.Column!,
                    LexicalScore = CalculateLexicalScore(searchText, x.Column!),
                    SemanticScore = x.Score,
                    SemanticTypeScore = CalculateSemanticTypeCompatibility(intentMetric, x.Column!)
                })
                .Select(x => new
                {
                    x.Column,
                    FinalScore = x.LexicalScore * 100 + x.SemanticTypeScore * 20 + x.SemanticScore * 10
                })
                .OrderByDescending(x => x.FinalScore)
                .ToList();

            var best = ranked.FirstOrDefault();
            if (best is null || best.FinalScore < 3) continue;

            runtimeMetric.Field = best.Column.ColumnName ?? runtimeMetric.Field;
            EnsureResolutionField(plan, best.Column.Id, best.Column.ColumnName ?? runtimeMetric.Field, runtimeMetric.GetAggregation().ToString());
        }
    }

    private static double CalculateSemanticTypeCompatibility(QueryMetric metric, SuperBuilder_AI.Models.Metadata.MetadataColumn column)
    {
        var type = metric.SemanticType?.Trim().ToLowerInvariant();
        var name = column.ColumnName?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(type)) return 0;
        return type switch
        {
            "amount" when name == "amount" || name.Contains("amount") || name.Contains("total_amount") || name.Contains("money") || name.Contains("price") => 1.0,
            "quantity" when name == "quantity" || name.Contains("quantity") || name.Contains("qty") => 1.0,
            "count" when name == "id" || name.EndsWith("_id") => 0.8,
            "ratio" when name.Contains("ratio") || name.Contains("rate") => 1.0,
            _ => 0
        };
    }

    private async Task ApplyResolvedJoinInferenceAsync(QueryPlan plan, QueryPlanSemanticResolution resolution)
    {
        if (resolution.Metric is null || resolution.Dimensions.Count == 0) return;
        var metricTableId = resolution.Metric.TableId;
        if (metricTableId <= 0) return;

        foreach (var dimension in resolution.Dimensions)
        {
            if (dimension.TableId <= 0 || dimension.TableId == metricTableId) continue;
            var searchTexts = new List<string>();
            if (!string.IsNullOrWhiteSpace(dimension.SemanticText)) searchTexts.Add(dimension.SemanticText);
            if (!string.IsNullOrWhiteSpace(dimension.SemanticText)) searchTexts.Add($"{dimension.SemanticText}ID");
            if (!string.IsNullOrWhiteSpace(resolution.Metric.Table)) searchTexts.Add(resolution.Metric.Table);

            var metadataResults = new List<SuperBuilder_AI.Models.AI.MetadataSemanticSearchResult>();
            foreach (var searchText in searchTexts.Distinct(StringComparer.OrdinalIgnoreCase))
                metadataResults.AddRange(await _metadataSearch.SearchAsync(searchText, 20));

            metadataResults = metadataResults
                .Where(x => x.Table is not null && x.Column is not null)
                .GroupBy(x => $"{x.Table!.Id}|{x.Column!.Id}|{x.Semantic?.Id ?? 0}", StringComparer.OrdinalIgnoreCase)
                .Select(x => x.OrderByDescending(item => item.Score).First())
                .ToList();
            if (metadataResults.Count == 0) continue;

            var candidates = await _joinInference.InferAsync(metadataResults);
            var candidate = candidates
                .Where(x => (x.LeftTableId == metricTableId && x.RightTableId == dimension.TableId) || (x.LeftTableId == dimension.TableId && x.RightTableId == metricTableId))
                .OrderByDescending(x => x.Confidence)
                .FirstOrDefault();
            if (candidate is null || candidate.LeftTableId == candidate.RightTableId) continue;

            if (!plan.Tables.Any(x => x.MetadataTableId == candidate.LeftTableId) || !plan.Tables.Any(x => x.MetadataTableId == candidate.RightTableId)) continue;

            var exists = plan.Joins.Any(x =>
                (x.LeftTableId == candidate.LeftTableId && x.LeftColumnId == candidate.LeftColumnId && x.RightTableId == candidate.RightTableId && x.RightColumnId == candidate.RightColumnId)
                || (x.LeftTableId == candidate.RightTableId && x.LeftColumnId == candidate.RightColumnId && x.RightTableId == candidate.LeftTableId && x.RightColumnId == candidate.LeftColumnId));
            if (exists) continue;

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
        if (binding is null) return;
        ValidateResolutionColumn(binding.ColumnId, binding.Column, "Metric");
        var matchedMetric = false;
        foreach (var metric in plan.Metrics)
        {
            if (!MatchesMetric(metric, binding.SemanticText)) continue;
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
        if (bindings.Count == 0) return;
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
        if (bindings.Count == 0) return;
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
        if (bindings.Count == 0) return;
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
        plan.Tables.Add(new QueryTable { MetadataTableId = tableId, DataSourceId = dataSourceId, TableName = tableName });
    }

    private static void EnsureResolutionField(QueryPlan plan, long columnId, string columnName, string aggregation)
    {
        var existing = plan.Fields.FirstOrDefault(field => field.MetadataColumnId == columnId);
        if (existing is not null)
        {
            existing.ColumnName = columnName;
            if (string.IsNullOrWhiteSpace(existing.Aggregation) || existing.Aggregation.Equals("NONE", StringComparison.OrdinalIgnoreCase)) existing.Aggregation = aggregation;
            return;
        }
        plan.Fields.Add(new QueryField { MetadataColumnId = columnId, ColumnName = columnName, Aggregation = aggregation });
    }

    private static void ValidateResolutionColumn(long columnId, string column, string bindingType)
    {
        if (columnId <= 0 || string.IsNullOrWhiteSpace(column))
            throw new InvalidOperationException($"Semantic Resolution 缺少有效的 {bindingType} Column Binding。");
    }

    private static bool MatchesMetric(QueryMetric metric, string semanticText)
    {
        if (string.IsNullOrWhiteSpace(semanticText)) return true;
        return string.Equals(metric.Name, semanticText, StringComparison.OrdinalIgnoreCase)
               || string.Equals(metric.Field, semanticText, StringComparison.OrdinalIgnoreCase)
               || string.Equals(metric.SemanticType, semanticText, StringComparison.OrdinalIgnoreCase);
    }
}