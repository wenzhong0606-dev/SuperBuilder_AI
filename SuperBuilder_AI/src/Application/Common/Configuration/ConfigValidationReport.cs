namespace SuperBuilder_AI.Application.Common.Configuration;

/// <summary>配置问题严重度。</summary>
public enum ConfigSeverity
{
	/// <summary>致命：非开发环境下缺失关键配置，必须 fail-fast。</summary>
	Error,

	/// <summary>告警：开发/测试环境降级提示，不阻断启动。</summary>
	Warning
}

/// <summary>单条配置问题。仅携带配置键路径与原因，绝不携带配置值（防密钥泄漏）。</summary>
public sealed record ConfigIssue(string Key, string Message, ConfigSeverity Severity);

/// <summary>统一配置校验报告。</summary>
public sealed record ConfigValidationReport(IReadOnlyList<ConfigIssue> Issues)
{
	/// <summary>致命问题（应触发 fail-fast）。</summary>
	public IReadOnlyList<ConfigIssue> Errors =>
		Issues.Where(i => i.Severity == ConfigSeverity.Error).ToList();

	/// <summary>告警问题（不阻断启动，仅记录）。</summary>
	public IReadOnlyList<ConfigIssue> Warnings =>
		Issues.Where(i => i.Severity == ConfigSeverity.Warning).ToList();

	/// <summary>是否存在致命问题。</summary>
	public bool HasErrors => Issues.Any(i => i.Severity == ConfigSeverity.Error);
}
