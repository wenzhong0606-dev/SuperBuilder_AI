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

    /// <summary>当前扫描阶段（Connecting / DiscoveringTables / SyncingMetadata / GeneratingSemantics / IndexingVectors / Finalizing 等）。</summary>
    public string Stage { get; set; } = "Queued";

    /// <summary>结构化富进度 JSON：真实 processed/total、当前对象、ETA、差异摘要和最近事件。</summary>
    public string? ProgressDetailsJson { get; set; }

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

    /// <summary>取消时间（UTC），仅 Cancelled 状态填充。</summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>扫描范围 JSON（§10.1 ScanScope）：Databases/Schemas/排除项/ScanViews。</summary>
    public string? ScopeJson { get; set; }

    /// <summary>本次扫描写入的批次版本号（§L.1）；由 DataSource.NextMetadataVersion 事务内原子分配，单调不重号。</summary>
    public int BatchVersion { get; set; }

    /// <summary>种子版本：本次任务启动时复制自的 active 版本号（§L.6 统一播种规则）。</summary>
    public int SeedVersion { get; set; }

    /// <summary>失败项续扫来源任务 Id（§L.6 retry-failed）；仅重扫失败项时填充。</summary>
    public long? OriginalJobId { get; set; }

    /// <summary>最近一次心跳时间（UTC），用于重启恢复超时判定（C4）。</summary>
    public DateTime? LastHeartbeatUtc { get; set; }

    /// <summary>激活后写入的版本号（成功切换 active 指针时填充）。</summary>
    public int? ActivatedVersion { get; set; }

    /// <summary>结构化失败原因（脱敏枚举值）：orphaned_references / vector_index_incomplete / worker_interrupted / heartbeat_timeout 等。</summary>
    public string? FailedReason { get; set; }
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
    Failed,

    /// <summary>已请求取消，后台在安全点（阶段/每表边界）退出中。</summary>
    Cancelling,

    /// <summary>已取消；staging 不可见、active 不变，由 GC 回收。</summary>
    Cancelled,

    /// <summary>部分表失败，但可用数据已入库并激活（SQL 部分成功且必需向量全 Synced，§10.6）。</summary>
    PartiallySucceeded,

    /// <summary>失败项重试中（§L.6）。</summary>
    Retrying
}
