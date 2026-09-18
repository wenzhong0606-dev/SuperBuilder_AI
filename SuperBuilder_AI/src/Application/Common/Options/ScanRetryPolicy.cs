namespace SuperBuilder_AI.Application.Common.Options;

/// <summary>
/// 扫描失败重试策略（C7 / 部分成功，§L.6）。
/// 通过 appsettings 的 "MetadataScan:RetryPolicy" 段注入；缺省使用下方安全默认值。
/// </summary>
public class ScanRetryPolicy
{
	/// <summary>单表/列失败最大重试次数（§L.6 MaxAttempts=3）。</summary>
	public int MaxAttempts { get; set; } = 3;

	/// <summary>连接类错误是否重试：默认 false（快速失败，不浪费配额）。</summary>
	public bool RetryOnConnectionError { get; set; }

	/// <summary>权限类错误是否重试：默认 false（标记需用户处理，不自动重试）。</summary>
	public bool RetryOnPermissionError { get; set; }

	/// <summary>表/列/向量抖动等瞬时错误是否可重试：默认 true。</summary>
	public bool RetryOnTransientError { get; set; } = true;
}
