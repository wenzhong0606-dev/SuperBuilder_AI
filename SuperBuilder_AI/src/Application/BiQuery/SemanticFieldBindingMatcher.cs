using System;
using System.Collections.Generic;
using System.Linq;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// M5-02：统一字段解析规则（单一事实来源）。
///
/// 将原先散落在 <c>QueryPlanBuilder.SemanticResolution</c> 中的
/// <c>MatchMetricBinding</c> / <c>MatchFilterBinding</c> / <c>MatchDimensionBinding</c> 三套近义、
/// 且存在细微分叉的匹配逻辑收敛为一条规则，使 Builder 对 Metric / Dimension / Filter 的
/// "运行时意图项 ↔ 权威绑定"匹配完全一致；同时供 Validator 以相同词汇判定字段是否已解析，
/// 从而保证 Understanding / Builder / Validator 三阶段对"同一个字段"给出一致结论。
///
/// 匹配优先级（任一命中即返回该绑定索引；找不到未使用绑定时返回 -1，调用方据此丢弃幻影项）：
///   1) 运行时 SemanticText 与绑定 SemanticText 精确匹配（首选）；
///   2) 运行时物理列（PhysicalColumn）与绑定的 SemanticText 或 PhysicalColumn 同名；
///   3) 运行时 SemanticText 与绑定的 PhysicalColumn 同名（维度旧逻辑的"语义↔物理"互换情形）；
///   4) 位置回退：仅当运行时确有可用引用（HasToken）且 positional 在范围内且未消费。
///
/// 该优先级是原三套逻辑的**并集**，不会削弱任何已有匹配路径；对位置回退统一要求 HasToken，
/// 以根治旧维度匹配缺失该守卫时可能把空维度误绑到无关绑定的"幽灵维度"风险（GQ-008）。
/// </summary>
public static class SemanticFieldBindingMatcher
{
    /// <summary>
    /// 为单个运行时字段引用在权威绑定集合中寻找最佳匹配。
    /// </summary>
    public static int Match(
        SemanticFieldReference runtime,
        IReadOnlyList<SemanticFieldBinding> bindings,
        HashSet<int> used,
        int positional)
    {
        if (runtime is null || bindings is null || bindings.Count == 0) return -1;

        // 1) SemanticText 精确匹配（首选）。
        for (var j = 0; j < bindings.Count; j++)
        {
            if (used.Contains(j)) continue;
            if (!string.IsNullOrWhiteSpace(runtime.SemanticText)
                && string.Equals(runtime.SemanticText, bindings[j].SemanticText, StringComparison.OrdinalIgnoreCase))
                return j;
        }

        // 2) 运行时物理列 ↔ 绑定 SemanticText / 物理列。
        for (var j = 0; j < bindings.Count; j++)
        {
            if (used.Contains(j)) continue;
            if (!string.IsNullOrWhiteSpace(runtime.PhysicalColumn)
                && (string.Equals(runtime.PhysicalColumn, bindings[j].SemanticText, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(runtime.PhysicalColumn, bindings[j].PhysicalColumn, StringComparison.OrdinalIgnoreCase)))
                return j;
        }

        // 3) 运行时 SemanticText ↔ 绑定物理列（维度旧逻辑的"语义↔物理"互换情形）。
        for (var j = 0; j < bindings.Count; j++)
        {
            if (used.Contains(j)) continue;
            if (!string.IsNullOrWhiteSpace(runtime.SemanticText)
                && !string.IsNullOrWhiteSpace(runtime.PhysicalColumn)
                && string.Equals(runtime.SemanticText, bindings[j].PhysicalColumn, StringComparison.OrdinalIgnoreCase))
                return j;
        }

        // 4) 位置回退（1:1 常见情形）：仅当运行时确有可识别引用（HasToken）。
        if (runtime.HasToken && positional >= 0 && positional < bindings.Count && !used.Contains(positional))
            return positional;

        return -1;
    }

    /// <summary>
    /// Builder 视角：是否存在一条可与运行时引用匹配的权威绑定（不关心具体索引）。
    /// 与 <see cref="Match"/> 共用同一规则，供上游快速判定是否为"幻影项"。
    /// </summary>
    public static bool CanResolve(
        SemanticFieldReference runtime,
        IReadOnlyList<SemanticFieldBinding> authoritativeBindings)
    {
        if (runtime is null || authoritativeBindings is null || authoritativeBindings.Count == 0) return false;
        return Match(runtime, authoritativeBindings, new HashSet<int>(), -1) >= 0;
    }

    /// <summary>
    /// Validator 视角：给定运行时计划字段引用与已加载的 MetadataColumn 集合，
    /// 判定该字段是否已"物理解析"（即存在列名匹配的物理列）。
    /// 与 Validator 原有 <c>FirstOrDefault(x =&gt; x.ColumnName == field)</c> 判定口径完全一致，
    /// 只是改以统一词汇 <see cref="SemanticFieldReference"/> 表达，保证三阶段词汇一致。
    /// </summary>
    public static bool IsPhysicallyResolved(
        SemanticFieldReference runtime,
        IReadOnlyList<MetadataColumn> metadataColumns)
    {
        if (runtime is null || metadataColumns is null || metadataColumns.Count == 0) return false;
        if (string.IsNullOrWhiteSpace(runtime.PhysicalColumn)) return false;
        return metadataColumns.Any(c =>
            !string.IsNullOrWhiteSpace(c.ColumnName)
            && string.Equals(c.ColumnName, runtime.PhysicalColumn, StringComparison.OrdinalIgnoreCase));
    }
}
