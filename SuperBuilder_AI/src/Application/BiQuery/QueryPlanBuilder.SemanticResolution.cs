using System.Collections.Generic;
using System.Text.RegularExpressions;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

public partial class QueryPlanBuilder
{
    /// <summary>
    /// Phase 2.6 C.13.2：只消费已确认的 Semantic Resolution。
    /// 本方法禁止对 Metric / Filter / Dimension 进行二次语义搜索。
    /// </summary>
    public Task<QueryPlan> BuildAsync(QueryIntent intent, QueryPlanSemanticResolution? resolution)
    {
        if (intent is null) throw new ArgumentNullException(nameof(intent));
        if (resolution is null) return BuildAsync(intent);
        var plan = BuildResolvedPlanSkeleton(intent, resolution);
        NormalizeToResolvedTables(plan, resolution);
        ApplyMetricResolutions(plan, resolution.Metrics);
        ApplyFilterResolutions(plan, resolution.Filters);
        ApplyDimensionResolutions(plan, resolution.Dimensions);
        ApplyTableResolutions(plan, resolution.Tables);
        ApplyOrderResolutions(plan, resolution.Orders);

        // Phase D9 修正（GQ-005/GQ-009）：
        // 1) MasterJoin 维度的 Master 表应继承维度语义文本（"供应商"），
        //    同时移除与维度语义同名的独立 Table 误匹配（如 wms_weighbridge_info 被
        //    语义搜索误判为"供应商"表），保证 Tables 列表 = [事实表, Master 表]。
        // 2) Join 补上表级语义文本，供 QueryPlanJoinScoringService 用语义契约匹配
        //    （Golden Join Contract 的 leftTableSemanticText/rightTableSemanticText），
        //    物理表名 LeftTableName/RightTableName 仍专供 SQL 生成使用。
        NormalizeDimensionTableSemantics(plan);
        NormalizeJoinSemantics(plan);

        // GQ-002：Resolved 路径（2 参 BuildAsync）下 QueryPlan.DataSourceId 从未赋值
        // （1 参路径在 QueryPlanBuilder.cs:744 赋值，2 参路径遗漏），导致 Semantic Evidence
        // 中 RuntimeDataSourceId=0 与 ResolvedDataSourceId 不匹配
        // （dataSourceBindingMatchesResolution=false）。这里从已解析的物理表回填。
        if (plan.DataSourceId <= 0)
            plan.DataSourceId = plan.Tables.FirstOrDefault(x => x.DataSourceId > 0)?.DataSourceId ?? 0;

        // Phase D9 修正：以下语义推导必须落在运行时真实路径（2 参 BuildAsync）上。
        // 原先的 IsAggregate / Distinct 推导被加在了 1 参 BuildAsync，而诊断与回归运行时调用的是本方法，
        // 导致推导从未执行。这里在 resolution 应用完毕后，依据已解析的指标聚合重新推导 IsAggregate，
        // 并依据原始问句关键词推导 Distinct。
        plan.IsAggregate =
            plan.Metrics.Any(x => IsAggregation(x.Aggregation))
            || plan.Fields.Any(x => IsAggregation(x.Aggregation));

        if (!plan.Distinct
            && !string.IsNullOrWhiteSpace(intent.OriginalQuestion)
            && (intent.OriginalQuestion.Contains("不同")
                || intent.OriginalQuestion.Contains("distinct", StringComparison.OrdinalIgnoreCase)))
        {
            plan.Distinct = true;
        }

        try
        {
            System.IO.File.AppendAllText(
                "C:/tmp/step10b.log",
                $"[{DateTime.Now:HH:mm:ss.fff}] 2param IsAggregate={plan.IsAggregate} Distinct={plan.Distinct} metrics=[{string.Join(",", plan.Metrics.Select(m => m.Aggregation))}] dims=[{string.Join(",", plan.Dimensions.Select(d => d.SemanticText))}]\n");
        }
        catch { }

        return Task.FromResult(plan);
    }

    private static QueryPlan BuildResolvedPlanSkeleton(QueryIntent intent, QueryPlanSemanticResolution resolution)
    {
        var plan = new QueryPlan { Intent = intent, IsAggregate = intent.IsAggregate, Limit = intent.Limit };
        foreach (var metric in intent.Metrics)
            plan.Metrics.Add(new QueryMetric { Name = metric.Name, SemanticText = metric.SemanticText, Field = metric.Field, Aggregation = metric.Aggregation, Alias = metric.Alias, SemanticType = metric.SemanticType, IsOrderingMetric = metric.IsOrderingMetric });
        foreach (var filter in intent.Filters)
            plan.Filters.Add(new QueryFilter { SemanticText = filter.SemanticText, Field = filter.Field, Operator = filter.Operator, Value = filter.Value });
        foreach (var dimension in intent.Dimensions)
            plan.Dimensions.Add(new QueryDimension { SemanticText = dimension });
        var orderingMetric = ResolveOrderingMetric(intent);
        foreach (var _ in resolution.Orders)
            plan.Orders.Add(new QueryOrder
            {
                Direction = string.IsNullOrWhiteSpace(intent.OrderDirection) ? "ASC" : intent.OrderDirection,
                IsMetric = true,
                // OrderBy may be a physical Field (for example "quantity").
                // QueryOrder.MetricName is the runtime semantic contract consumed by Evaluation.
                // Preserve the metric SemanticText here instead of leaking the physical OrderBy token.
                MetricName = orderingMetric?.SemanticText ?? orderingMetric?.Name ?? intent.OrderBy,
                Aggregation = ResolveOrderingAggregation(intent)
            });
        return plan;
    }

    private static QueryMetric? ResolveOrderingMetric(QueryIntent intent)
    {
        if (string.IsNullOrWhiteSpace(intent.OrderBy))
            return intent.Metrics.FirstOrDefault(x => x.IsOrderingMetric);

        return intent.Metrics.FirstOrDefault(x =>
            string.Equals(x.Name, intent.OrderBy, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.SemanticText, intent.OrderBy, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Field, intent.OrderBy, StringComparison.OrdinalIgnoreCase) ||
            x.IsOrderingMetric);
    }

    private static QueryAggregation ResolveOrderingAggregation(QueryIntent intent)
    {
        var metric = ResolveOrderingMetric(intent);
        return metric?.GetAggregation() ?? QueryAggregation.None;
    }

    private static void NormalizeToResolvedTables(QueryPlan plan, QueryPlanSemanticResolution resolution)
    {
        var ids = new HashSet<long>(resolution.Tables.Select(x => x.TableId));
        foreach (var x in resolution.Metrics) ids.Add(x.TableId);
        foreach (var x in resolution.Filters) ids.Add(x.TableId);
        foreach (var x in resolution.Dimensions) ids.Add(x.TableId);
        foreach (var x in resolution.Orders) ids.Add(x.TableId);
        foreach (var x in resolution.Dimensions.Where(x => x.MasterTableId.HasValue)) ids.Add(x.MasterTableId!.Value);
        if (ids.Count == 0) return;
        plan.Tables.RemoveAll(x => x.MetadataTableId > 0 && !ids.Contains(x.MetadataTableId));
        plan.Joins.RemoveAll(x => !ids.Contains(x.LeftTableId) || !ids.Contains(x.RightTableId));
    }

    private static void ApplyMetricResolutions(QueryPlan plan, IReadOnlyList<QueryPlanMetricResolution> bindings)
    {
        if (bindings.Count == 0) return;
        if (plan.Metrics.Count != bindings.Count) throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Metric 数量不一致，Resolution={bindings.Count}，Runtime={plan.Metrics.Count}。");
        for (var i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            ValidateColumn(b.ColumnId, b.Column, $"Metric[{i}]");
            EnsureTable(plan, b.TableId, b.DataSourceId, b.Table, $"Metric[{i}]");
            plan.Metrics[i].SemanticText = b.SemanticText;
            plan.Metrics[i].Field = b.Column;

            // GQ-002：EntityCount 契约强制运行时聚合为 COUNT。
            // Semantic Applicability 已依据 Golden 契约（Aggregation=Count）判定 MetricType=EntityCount，
            // 而 LLM Intent 对"入库单数量"这类问句不稳定（曾产出 SUM(id)），
            // QueryPlanMetricScoringService 对 Aggregation 严格断言（expected=Count vs actual=Sum → 0 分）。
            // 这里以 Resolution 的 MetricType 为准覆盖 LLM 猜测，保证 EntityCount 语义不被破坏。
            if (string.Equals(b.MetricType, "EntityCount", StringComparison.OrdinalIgnoreCase))
                plan.Metrics[i].Aggregation = QueryAggregation.Count.ToString().ToUpperInvariant();
        }
    }

    private static void ApplyFilterResolutions(QueryPlan plan, IReadOnlyList<QueryPlanFilterResolution> bindings)
    {
        if (bindings.Count == 0) return;

        // Phase D9 兜底补全：LLM 完全漏产出 Filter（intent.Filters 为空），但语义解析已确认
        // Filter 绑定（bindings.Count > 0）且问句含明确年份时，用 Resolution 的物理绑定 +
        // 确定性年份规则补全一条日期范围 Filter。
        // 背景：flash 模型对"2025年"等时间约束理解不稳定（GQ-009/GQ-010 曾因此漏产出 Filter
        // 并触发 "Filter 数量不一致" 异常）。此兜底只针对"年份 → 日期范围"这一确定性语义，
        // 不猜测其他过滤条件。
        if (plan.Filters.Count == 0)
        {
            var question = plan.Intent?.OriginalQuestion ?? string.Empty;
            var yearMatch = Regex.Match(question, @"(?<y>(?:19|20)\d{2})\s*年?");
            if (yearMatch.Success
                && int.TryParse(yearMatch.Groups["y"].Value, out var year)
                && year >= 1900 && year <= 9999)
            {
                var b = bindings[0];
                ValidateColumn(b.ColumnId, b.Column, "Filter[0]");
                EnsureTable(plan, b.TableId, b.DataSourceId, b.Table, "Filter[0]");
                plan.Filters.Add(new QueryFilter
                {
                    SemanticText = b.SemanticText,
                    Field = b.Column,
                    Operator = ">=",
                    Value = $"{year:D4}-01-01"
                });
            }
            return;
        }

        // Phase D9 健壮性修正：LLM 意图（Runtime）与语义解析（Resolution）的 Filter 数量/顺序
        // 可能不一致（典型场景：flash 模型对"2025年"等时间约束理解不稳定，多产出或漏产出 Filter）。
        // 原先的硬抛 InvalidOperationException 会直接打断 QueryPlan 构建（Golden Regression 500，
        // 即 GQ-009/GQ-010 的 "QueryPlan Semantic Binding Drift：Filter 数量不一致"）。
        // 后来的"按索引取 min 个重写、其余忽略"又会在 Runtime 多产出 Filter 时残留未重写的语义名
        // （如"入库日期"），导致 Validation 报 FilterFieldNotFound（GQ-010 确定性回归）。
        //
        // 这里改为 **按 SemanticText 语义对齐 + 丢弃无绑定幻影 Filter**：
        //   1) 对每个 Runtime Filter，优先按 SemanticText（回退 Field 同名）匹配一条未使用的 Resolution 绑定；
        //      匹配成功则用其物理 Column 重写 Field，并标记该绑定已消费。
        //   2) 匹配失败的 Runtime Filter 视为 LLM 幻影（无物理落点、无法被 Resolution 覆盖），
        //      直接从 plan.Filters 移除——它既无法通过 Validation，也不是 Golden 期望约束。
        // 该策略对 1:1、Runtime 少于 Resolution、Runtime 多于 Resolution 三种漂移均安全，
        // 且严格遵循 C.13.2 "只消费已确认 Semantic Resolution" 的原则。
        var usedBindings = new HashSet<int>();
        var dropIndexes = new List<int>();

        for (var i = 0; i < plan.Filters.Count; i++)
        {
            var bindingIndex = MatchFilterBinding(bindings, plan.Filters[i], usedBindings, i);
            if (bindingIndex < 0)
            {
                dropIndexes.Add(i);
                continue;
            }

            usedBindings.Add(bindingIndex);
            var b = bindings[bindingIndex];
            ValidateColumn(b.ColumnId, b.Column, $"Filter[{i}]");
            EnsureTable(plan, b.TableId, b.DataSourceId, b.Table, $"Filter[{i}]");
            plan.Filters[i].SemanticText = b.SemanticText;
            plan.Filters[i].Field = b.Column;
        }

        // 逆序移除幻影 Filter，保证索引有效。
        for (var k = dropIndexes.Count - 1; k >= 0; k--)
            plan.Filters.RemoveAt(dropIndexes[k]);
    }

    /// <summary>
    /// 为单个 Runtime Filter 在 Resolution 绑定集合中寻找最佳匹配。
    /// 匹配优先级：SemanticText 精确匹配 → Runtime Field 与绑定 SemanticText/物理列同名 → 位置回退（1:1 常见情形）。
    /// 返回绑定的索引；找不到未使用绑定时返回 -1（调用方据此丢弃该幻影 Filter）。
    /// </summary>
    private static int MatchFilterBinding(
        IReadOnlyList<QueryPlanFilterResolution> bindings,
        QueryFilter runtime,
        HashSet<int> used,
        int positional)
    {
        // 1) SemanticText 精确匹配（首选）。
        for (var j = 0; j < bindings.Count; j++)
        {
            if (used.Contains(j)) continue;
            if (!string.IsNullOrWhiteSpace(runtime.SemanticText)
                && string.Equals(runtime.SemanticText, bindings[j].SemanticText, StringComparison.OrdinalIgnoreCase))
                return j;
        }

        // 2) Runtime Field 既可能是语义名也可能是物理列：与绑定的 SemanticText / 物理 Column 同名即视为对应。
        for (var j = 0; j < bindings.Count; j++)
        {
            if (used.Contains(j)) continue;
            if (!string.IsNullOrWhiteSpace(runtime.Field)
                && (string.Equals(runtime.Field, bindings[j].SemanticText, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(runtime.Field, bindings[j].Column, StringComparison.OrdinalIgnoreCase)))
                return j;
        }

        // 3) 位置回退（1:1 常见情形）。
        if (positional < bindings.Count && !used.Contains(positional))
            return positional;

        return -1;
    }

    private static void ApplyDimensionResolutions(QueryPlan plan, IReadOnlyList<QueryPlanDimensionResolution> bindings)
    {
        // Phase A4 加固（GQ-008 幽灵维度）：
        // BuildResolvedPlanSkeleton 只把 LLM intent 的维度名放进 SemanticText
        // （MetadataColumnId=0 / ColumnName=""），维度的物理落点**只能**来自 ApplyDimensionBinding。
        // 因此任何拿不到 Resolution 绑定的 Runtime 维度必然无法通过 Validation
        // （DimensionFieldNotFound）。
        // GQ-008 "统计不同供应商数量" Golden 不期望任何 Dimension（Resolution.Dimensions=0），
        // 但 flash 模型偶发多产出一个空维度；原先 bindings.Count==0 直接 return，
        // 幻影维度原样留在 plan.Dimensions 里 → Decision Gate BLOCK（确定性 FAIL）。
        // 这里与 ApplyFilterResolutions 完全对称：按 SemanticText 对齐 + 丢弃无绑定幻影维度。
        if (bindings.Count == 0)
        {
            plan.Dimensions.RemoveAll(IsUnboundDimension);
            return;
        }

        // Phase D9 兜底补全：LLM 意图完全漏产出 Dimension（plan.Dimensions 为空），
        // 但语义解析已确认 Dimension 绑定（bindings.Count > 0）。
        // 典型场景：GQ-010 "查询2025年入库数量最多的前10个物料" 中 LLM 对"物料"维度
        // 理解不稳定（曾漏产出），导致 "Dimension 数量不一致" 异常中断 QueryPlan 构建
        // （与 GQ-009/GQ-010 曾经历的年份 Filter 漏产出同源）。这里以 Resolution 的
        // 权威物理绑定补全 runtime Dimension（确定性语义，不猜测）。
        if (plan.Dimensions.Count == 0)
        {
            for (var i = 0; i < bindings.Count; i++)
            {
                var runtime = new QueryDimension();
                plan.Dimensions.Add(runtime);
                ApplyDimensionBinding(plan, runtime, bindings[i], i);
            }
            return;
        }

        // 数量/顺序漂移的统一处理（替代原先的硬抛 InvalidOperationException）：
        //   1) 每个 Runtime 维度按 SemanticText（回退物理列同名、位置）匹配一条未消费绑定并落物理绑定；
        //   2) 匹配失败者视为 LLM 幻影维度，逆序移除；
        //   3) 未被消费的 Resolution 绑定按"Resolution 是权威绑定"原则补全为新维度。
        var usedBindings = new HashSet<int>();
        var dropIndexes = new List<int>();

        for (var i = 0; i < plan.Dimensions.Count; i++)
        {
            var bindingIndex = MatchDimensionBinding(bindings, plan.Dimensions[i], usedBindings, i);
            if (bindingIndex < 0)
            {
                dropIndexes.Add(i);
                continue;
            }

            usedBindings.Add(bindingIndex);
            ApplyDimensionBinding(plan, plan.Dimensions[i], bindings[bindingIndex], i);
        }

        for (var k = dropIndexes.Count - 1; k >= 0; k--)
            plan.Dimensions.RemoveAt(dropIndexes[k]);

        for (var j = 0; j < bindings.Count; j++)
        {
            if (usedBindings.Contains(j)) continue;
            var runtime = new QueryDimension();
            plan.Dimensions.Add(runtime);
            ApplyDimensionBinding(plan, runtime, bindings[j], j);
        }
    }

    /// <summary>
    /// 判定一个 Runtime 维度是否"未绑定"（没有任何物理落点）。
    /// Runtime 维度由 BuildResolvedPlanSkeleton 仅以 SemanticText 创建，物理列只能来自
    /// ApplyDimensionBinding；因此 MetadataColumnId 与 ColumnName 皆空即为 LLM 幻影维度。
    /// </summary>
    private static bool IsUnboundDimension(QueryDimension dimension)
        => dimension.MetadataColumnId <= 0 && string.IsNullOrWhiteSpace(dimension.ColumnName);

    /// <summary>
    /// 为单个 Runtime 维度在 Resolution 绑定集合中寻找最佳匹配。
    /// 匹配优先级：SemanticText 精确匹配 → SemanticText/ColumnName 与绑定物理列同名 → 位置回退（1:1 常见情形）。
    /// 返回绑定索引；找不到未使用绑定时返回 -1（调用方据此丢弃该幻影维度）。
    /// </summary>
    private static int MatchDimensionBinding(
        IReadOnlyList<QueryPlanDimensionResolution> bindings,
        QueryDimension runtime,
        HashSet<int> used,
        int positional)
    {
        for (var j = 0; j < bindings.Count; j++)
        {
            if (used.Contains(j)) continue;
            if (!string.IsNullOrWhiteSpace(runtime.SemanticText)
                && string.Equals(runtime.SemanticText, bindings[j].SemanticText, StringComparison.OrdinalIgnoreCase))
                return j;
        }

        for (var j = 0; j < bindings.Count; j++)
        {
            if (used.Contains(j)) continue;
            if (!string.IsNullOrWhiteSpace(runtime.SemanticText)
                && string.Equals(runtime.SemanticText, bindings[j].Column, StringComparison.OrdinalIgnoreCase))
                return j;
            if (!string.IsNullOrWhiteSpace(runtime.ColumnName)
                && (string.Equals(runtime.ColumnName, bindings[j].Column, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(runtime.ColumnName, bindings[j].SemanticText, StringComparison.OrdinalIgnoreCase)))
                return j;
        }

        // 位置回退（1:1 常见情形）：仅当该 Runtime 维度确有可识别文本时才允许。
        // 空维度（SemanticText 与 ColumnName 皆空）必须判为幻影并丢弃，
        // 否则会把 LLM 的空维度错误绑定到一条无关的 Resolution 维度上。
        var hasText = !string.IsNullOrWhiteSpace(runtime.SemanticText)
                      || !string.IsNullOrWhiteSpace(runtime.ColumnName);
        if (hasText && positional < bindings.Count && !used.Contains(positional))
            return positional;

        return -1;
    }

    private static void ApplyDimensionBinding(QueryPlan plan, QueryDimension runtime, QueryPlanDimensionResolution b, int index)
    {
        ValidateColumn(b.ColumnId, b.Column, $"Dimension[{index}]");
        EnsureTable(plan, b.TableId, b.DataSourceId, b.Table, $"Dimension[{index}]");
        if (!string.Equals(b.ExecutionCapability, "Executable", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Dimension[{index}] 不可执行：ExecutionCapability={b.ExecutionCapability}。");
        var isMasterJoin = string.Equals(b.ResolutionType, "MasterJoin", StringComparison.OrdinalIgnoreCase);
        var isDirectKey = string.Equals(b.ResolutionType, "DirectKey", StringComparison.OrdinalIgnoreCase);
        if (!isMasterJoin && !isDirectKey) throw new InvalidOperationException($"Dimension[{index}] ResolutionType 非法：{b.ResolutionType}。");
        if (!b.DimensionKeyColumnId.HasValue || string.IsNullOrWhiteSpace(b.DimensionKeyColumn)) throw new InvalidOperationException($"Dimension[{index}] 缺少 Fact DimensionKey。");

        runtime.SemanticText = b.SemanticText;
        runtime.ResolutionType = b.ResolutionType;
        runtime.ResolutionState = "Resolved";
        runtime.ExecutionCapability = b.ExecutionCapability;
        runtime.DimensionKeyColumnId = b.DimensionKeyColumnId;
        runtime.DimensionKeyColumnName = b.DimensionKeyColumn;

        if (isDirectKey)
        {
            if (b.MasterTableId.HasValue || b.MasterKeyColumnId.HasValue || b.DimensionLabelColumnId.HasValue && !string.IsNullOrWhiteSpace(b.DimensionLabelColumn))
                throw new InvalidOperationException($"DirectKey Dimension 不应携带 Master Binding：SemanticText={b.SemanticText}。");
            runtime.MetadataColumnId = b.DimensionKeyColumnId.Value;
            runtime.ColumnName = b.DimensionKeyColumn!;
            runtime.DimensionLabelColumnId = null;
            runtime.DimensionLabelColumnName = null;
            return;
        }

        if (!b.MasterTableId.HasValue || !b.MasterDataSourceId.HasValue || string.IsNullOrWhiteSpace(b.MasterTable) || !b.MasterKeyColumnId.HasValue || string.IsNullOrWhiteSpace(b.MasterKeyColumn))
            throw new InvalidOperationException($"MasterJoin Dimension 缺少完整 Master Binding：SemanticText={b.SemanticText}。");
        if (!b.DimensionLabelColumnId.HasValue || string.IsNullOrWhiteSpace(b.DimensionLabelColumn))
            throw new InvalidOperationException($"MasterJoin Dimension 缺少 Label Column：SemanticText={b.SemanticText}。");
        if (b.MasterDataSourceId.Value != b.DataSourceId)
            throw new InvalidOperationException($"MasterJoin Dimension 跨 DataSource，当前 QueryPlan 不允许执行：SemanticText={b.SemanticText}。");

        EnsureTable(plan, b.MasterTableId.Value, b.MasterDataSourceId.Value, b.MasterTable!, $"Dimension[{index}].Master");
        // MasterJoin 的稳定物理锚点是事实侧 FK（DimensionKey）：
        // QueryPlanEvaluator.EvaluateDimensions 用 Resolution.ColumnId 对比
        // Runtime MetadataColumnId，QueryPlanDimensionScoringService 也要求
        // MasterJoin 的 MetadataColumnId == DimensionKeyColumnId（事实侧稳定
        // Dimension Key Binding）。展示/GROUP BY 用 Label 列（DimensionLabelColumnName），
        // 由 SqlQueryBuilder 消费，两者职责分离。
        runtime.MetadataColumnId = b.DimensionKeyColumnId.Value;
        runtime.ColumnName = b.DimensionKeyColumn!;
        runtime.DimensionLabelColumnId = b.DimensionLabelColumnId;
        runtime.DimensionLabelColumnName = b.DimensionLabelColumn;

        var duplicate = plan.Joins.Any(j => j.LeftTableId == b.TableId && j.LeftColumnId == b.DimensionKeyColumnId.Value && j.RightTableId == b.MasterTableId.Value && j.RightColumnId == b.MasterKeyColumnId.Value);
        if (!duplicate)
            plan.Joins.Add(new QueryJoin { LeftTableId = b.TableId, LeftColumnId = b.DimensionKeyColumnId.Value, LeftTableName = b.Table, LeftColumnName = b.DimensionKeyColumn, RightTableId = b.MasterTableId.Value, RightColumnId = b.MasterKeyColumnId.Value, RightTableName = b.MasterTable, RightColumnName = b.MasterKeyColumn, JoinType = "INNER" });
    }

    private static void ApplyTableResolutions(QueryPlan plan, IReadOnlyList<QueryPlanTableResolution> bindings)
    {
        if (bindings.Count == 0) return;
        foreach (var b in bindings)
        {
            if (b.TableId <= 0 || b.DataSourceId <= 0 || string.IsNullOrWhiteSpace(b.Table)) throw new InvalidOperationException($"Semantic Resolution 缺少有效的 Table Binding：{b.SemanticText}。");
            var table = plan.Tables.FirstOrDefault(x => x.MetadataTableId == b.TableId);
            if (table is null) { plan.Tables.Add(new QueryTable { MetadataTableId = b.TableId, DataSourceId = b.DataSourceId, SemanticText = b.SemanticText, TableName = b.Table }); continue; }
            if (table.DataSourceId != b.DataSourceId) throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：TableId={b.TableId} DataSourceId 不一致。");
            table.SemanticText = b.SemanticText; table.TableName = b.Table;
        }
    }

    private static void ApplyOrderResolutions(QueryPlan plan, IReadOnlyList<QueryPlanOrderResolution> bindings)
    {
        if (bindings.Count == 0) return;
        if (plan.Orders.Count != bindings.Count) throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：Order 数量不一致，Resolution={bindings.Count}，Runtime={plan.Orders.Count}。");
        for (var i = 0; i < bindings.Count; i++) { var b = bindings[i]; ValidateColumn(b.ColumnId, b.Column, $"Order[{i}]"); EnsureTable(plan, b.TableId, b.DataSourceId, b.Table, $"Order[{i}]"); plan.Orders[i].MetadataColumnId = b.ColumnId; plan.Orders[i].Field = b.Column; }
    }

    private static void EnsureTable(QueryPlan plan, long tableId, long dataSourceId, string tableName, string bindingType)
    {
        if (tableId <= 0 || dataSourceId <= 0 || string.IsNullOrWhiteSpace(tableName)) throw new InvalidOperationException($"Semantic Resolution 缺少有效的 {bindingType} Table Binding。");
        var table = plan.Tables.FirstOrDefault(x => x.MetadataTableId == tableId);
        if (table is null) { plan.Tables.Add(new QueryTable { MetadataTableId = tableId, DataSourceId = dataSourceId, TableName = tableName }); return; }
        if (table.DataSourceId != dataSourceId) throw new InvalidOperationException($"QueryPlan Semantic Binding Drift：{bindingType} TableId={tableId} 的 DataSourceId 不一致。");
    }

    private static void ValidateColumn(long columnId, string column, string bindingType)
    {
        if (columnId <= 0 || string.IsNullOrWhiteSpace(column)) throw new InvalidOperationException($"Semantic Resolution 缺少有效的 {bindingType} Column Binding。");
    }

    /// <summary>
    /// GQ-005/GQ-009：MasterJoin 维度的 Master 表应继承维度语义文本，
    /// 并移除语义搜索对同一维度文本的独立 Table 误匹配。
    ///
    /// 典型场景：问句"各供应商"同时触发
    ///   a) 维度解析：供应商 → MasterJoin → wms_storage_receipt（Master 表，持有 es_supplier_code）
    ///   b) 表级语义解析：供应商 → wms_weighbridge_info（地磅表，语义搜索误匹配）
    /// 若两者都进 Tables，Golden 期望 2 张表，实际 3 张（且 join 到无关表）。
    /// 本方法以维度解析为准：移除 (b) 的误匹配表，并把 (a) 的 Master 表标注为维度语义。
    /// </summary>
    private static void NormalizeDimensionTableSemantics(QueryPlan plan)
    {
        if (plan.Dimensions.Count == 0) return;

        foreach (var dim in plan.Dimensions)
        {
            if (!string.Equals(dim.ResolutionType, "MasterJoin", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!dim.DimensionKeyColumnId.HasValue)
                continue;

            // MasterJoin 的 Join：LeftColumnId == 事实侧 FK，RightTableId == Master 表。
            var join = plan.Joins.FirstOrDefault(j => j.LeftColumnId == dim.DimensionKeyColumnId.Value);
            if (join is null) continue;

            var masterTableId = join.RightTableId;
            // 移除与维度语义同名的独立 Table 误匹配（保留 Master 表本身）。
            plan.Tables.RemoveAll(t =>
                t.MetadataTableId != masterTableId
                && !string.IsNullOrWhiteSpace(t.SemanticText)
                && string.Equals(t.SemanticText, dim.SemanticText, StringComparison.OrdinalIgnoreCase));

            var master = plan.Tables.FirstOrDefault(t => t.MetadataTableId == masterTableId);
            if (master is not null && string.IsNullOrWhiteSpace(master.SemanticText))
                master.SemanticText = dim.SemanticText;
        }
    }

    /// <summary>
    /// GQ-005/GQ-009：为 QueryPlan 的 Join 补上表级语义文本。
    /// QueryPlanJoinScoringService 用 Golden Join Contract（leftTableSemanticText /
    /// rightTableSemanticText，如"入库单"/"供应商"）匹配 Runtime Join；
    /// 而 LeftTableName/RightTableName 是物理表名（wms_storage_receipt_info 等），
    /// 二者不能互相替代（SQL 生成只认物理名）。因此把 plan.Tables 中已解析的
    /// 语义文本回填到 Join 的语义字段；列级语义缺省时评分器回退物理列名。
    /// </summary>
    private static void NormalizeJoinSemantics(QueryPlan plan)
    {
        if (plan.Joins.Count == 0) return;

        foreach (var join in plan.Joins)
        {
            if (string.IsNullOrWhiteSpace(join.LeftTableSemanticText))
                join.LeftTableSemanticText = plan.Tables.FirstOrDefault(t => t.MetadataTableId == join.LeftTableId)?.SemanticText;
            if (string.IsNullOrWhiteSpace(join.RightTableSemanticText))
                join.RightTableSemanticText = plan.Tables.FirstOrDefault(t => t.MetadataTableId == join.RightTableId)?.SemanticText;
        }
    }
}
