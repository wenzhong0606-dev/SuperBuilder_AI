using System;
using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 单表/列扫描失败记录（C7 / 部分成功）。关联原任务 OriginalJobId，供「仅重扫失败项」续扫定位。
/// </summary>
public class MetadataScanJobFailure : BaseEntity
{
	public long JobId { get; set; }

	public long? OriginalJobId { get; set; }

	public long DataSourceId { get; set; }

	public string? Database { get; set; }

	public string? Schema { get; set; }

	public string? TableName { get; set; }

	public string? Stage { get; set; }

	public string? ErrorType { get; set; }

	/// <summary>脱敏后的错误信息（不含连接细节/凭据）。</summary>
	public string? ErrorMessage { get; set; }

	public int RetryCount { get; set; }

	public DateTime FirstFailedAt { get; set; } = DateTime.UtcNow;

	public DateTime LastFailedAt { get; set; } = DateTime.UtcNow;

	public bool Resolved { get; set; }
}
