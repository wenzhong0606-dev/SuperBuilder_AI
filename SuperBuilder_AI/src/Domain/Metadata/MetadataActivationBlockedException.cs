using System;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 元数据激活被阻断（§L.5 / §10.6）：孤儿引用（orphaned_references）或向量不完整（vector_index_incomplete）。
/// 抛出后指针不翻转、active 不变、旧版本行与引用完好；宿主捕获后置任务 Failed 并落结构化原因。
/// </summary>
public sealed class MetadataActivationBlockedException : Exception
{
    /// <summary>结构化失败原因（脱敏枚举值）：orphaned_references / vector_index_incomplete。</summary>
    public string Reason { get; }

    /// <summary>阻断时附带的依赖清单（孤儿引用）。</summary>
    public IReadOnlyList<OrphanReference> Orphans { get; }

    public MetadataActivationBlockedException(string reason, IReadOnlyList<OrphanReference>? orphans = null)
        : base($"元数据激活被阻断：{reason}")
    {
        Reason = reason;
        Orphans = orphans ?? Array.Empty<OrphanReference>();
    }
}

/// <summary>
/// 激活阻断时的孤儿引用条目（§L.5b），供前端展示依赖清单与处理入口。
/// </summary>
public sealed class OrphanReference
{
    /// <summary>引用类型：RLS / BINDING / LEARNING。</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>引用实体 Id（RowLevelSecurityPolicy / PhysicalBinding / MetadataLearningRecord）。</summary>
    public long Id { get; init; }

    /// <summary>所属数据源 Id。</summary>
    public long DataSourceId { get; init; }

    /// <summary>表名（表级引用）。</summary>
    public string? Table { get; init; }

    /// <summary>列名（列级引用）。</summary>
    public string? Column { get; init; }
}
