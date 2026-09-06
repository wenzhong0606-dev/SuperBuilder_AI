using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 数据源元数据扫描任务（M4-05）。记录一次扫描的队列/运行/完成状态与可观测计数，
/// 供前端轮询进度。错误字段仅保存脱敏后的异常类型名与摘要，绝不保存连接串等敏感信息。
/// </summary>
public class MetadataScanJob : BaseEntity
{
    /// <summary>所属租户标识。</summary>
    public long TenantId { get; set; }

    /// <summary>关联的数据源标识。</summary>
    public long DataSourceId { get; set; }

    /// <summary>关联的数据源实体。</summary>
    public DataSource? DataSource { get; set; }

    /// <summary>触发扫描的用户标识（字符串形式，兼容平台/租户管理员 Id）。</summary>
    public string? TriggeredBy { get; set; }

    /// <summary>扫描状态：Queued / Running / Succeeded / Failed。</summary>
    public MetadataScanJobStatus Status { get; set; } = MetadataScanJobStatus.Queued;

    /// <summary>进度百分比（0-100）。</summary>
    public int ProgressPercent { get; set; }

    /// <summary>扫描开始时间（UTC）。</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>扫描结束时间（UTC）。</summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>已扫描的表数量。</summary>
    public int TablesScanned { get; set; }

    /// <summary>已扫描的字段数量。</summary>
    public int ColumnsScanned { get; set; }

    /// <summary>检测到的孤儿对象数量（源中已不存在、且未被行级安全策略/学习记录引用）。</summary>
    public int OrphansDetected { get; set; }

    /// <summary>失败时的错误码（仅异常类型名，脱敏）。成功或排队中为 null。</summary>
    public string? ErrorCode { get; set; }

    /// <summary>失败时的错误摘要（脱敏，绝不含有连接串）。成功或排队中为 null。</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 元数据扫描任务状态（M4-05）。
/// </summary>
public enum MetadataScanJobStatus
{
    /// <summary>已入队，等待后台处理器取走。</summary>
    Queued,

    /// <summary>正在执行。</summary>
    Running,

    /// <summary>成功完成。</summary>
    Succeeded,

    /// <summary>失败。</summary>
    Failed
}
