namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 扫描可观测遥测（M4-05）。由 <see cref="MetadataScannerService"/> 在扫描过程中填充，
/// 供后台作业回写到 <see cref="MetadataScanJob"/> 记录，向前端暴露进度与计数。
/// </summary>
public class ScanTelemetry
{
    /// <summary>已扫描（处理）的表数量。</summary>
    public int TablesScanned { get; set; }

    /// <summary>已扫描（处理）的字段数量。</summary>
    public int ColumnsScanned { get; set; }

    /// <summary>已清理（删除）的孤儿对象数量（源中已不存在、且未被行级安全策略/学习记录引用）。</summary>
    public int OrphansDetected { get; set; }
}
