using System;
using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 向量 GC 持久待办（§L.7 / §10.7）。在删除/取消同一事务内写入，进程退出不丢；
/// MetadataVectorGcJob 后台轮询 Pending 待办，按 data_source_id / 旧版本删 Qdrant point。
/// 可重试、幂等。
/// </summary>
public class MetadataVectorGcRequest : BaseEntity
{
	/// <summary>目标数据源（按 data_source_id 定位 point）。删除数据源时填；旧版清理可空（按 OldVersion 匹配）。</summary>
	public long? DataSourceId { get; set; }

	/// <summary>租户（仅用于审计/隔离）。</summary>
	public long TenantId { get; set; }

	/// <summary>待清理的旧版本号（激活后旧 active）。GC 删除 metadata_version &lt; active 且 data_source_id 匹配的 point。</summary>
	public int? OldVersion { get; set; }

	/// <summary>触发原因：DataSourceDeleted / StagingGc。</summary>
	public string Reason { get; set; } = string.Empty;

	/// <summary>状态：Pending / Running / Done / Failed。</summary>
	public string Status { get; set; } = "Pending";

	/// <summary>待删 point ID 快照（§10.7）：删除前枚举该 ds 全部 VectorId 持久化，覆盖从未回填、无 data_source_id 的旧 point。</summary>
	public string? PayloadJson { get; set; }

	public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

	public DateTime? LastAttemptAt { get; set; }

	public string? Error { get; set; }
}
