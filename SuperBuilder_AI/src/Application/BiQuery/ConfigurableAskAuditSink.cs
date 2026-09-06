using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 可配置 Ask 审计 Sink（M6-05）：按 <see cref="AskAuditOptions.Mode"/> 路由。
///
/// - None（默认）：委托 <see cref="NoOpAskAuditSink"/>，零行为变更；
/// - Log：委托 <see cref="LogAskAuditSink"/>，结构化日志输出。
///
/// 默认 None 与历史行为逐字节等价，对 Golden 契约免疫；
/// 运维通过配置（而非代码）开启审计输出。
/// </summary>
public sealed class ConfigurableAskAuditSink : IAskAuditSink
{
	private readonly IAskAuditSink _inner;

	/// <summary>创建可配置 Ask 审计 Sink。</summary>
	public ConfigurableAskAuditSink(
		IOptions<AskAuditOptions> options,
		ILoggerFactory loggerFactory)
	{
		var mode = (options ?? throw new System.ArgumentNullException(nameof(options)))
			.Value?.Mode ?? AskAuditMode.None;

		_inner = mode == AskAuditMode.Log
			? new LogAskAuditSink(
				(loggerFactory ?? throw new System.ArgumentNullException(nameof(loggerFactory)))
					.CreateLogger<LogAskAuditSink>())
			: new NoOpAskAuditSink();
	}

	/// <inheritdoc />
	public Task RecordAsync(
		AskAuditRecord record,
		CancellationToken ct = default)
		=> _inner.RecordAsync(record, ct);
}
