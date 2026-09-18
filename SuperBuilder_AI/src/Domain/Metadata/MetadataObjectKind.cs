namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 元数据的物理对象类型。用于区分基表与视图，支撑 C2 的 ScanViews 开关与同名表跨 schema/schema 并存。
/// </summary>
public enum MetadataObjectKind
{
    /// <summary>基表（物理表）。</summary>
    Table = 0,

    /// <summary>视图（虚拟表）。</summary>
    View = 1
}
