using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 可配置审计 Sink（M5-09）：按 <see cref="DecisionAuditOptions.Mode"/> 路由。
///
/// - None（默认）：委托 <see cref="NoOpDecisionAuditSink"/>，零行为变更；
/// - Log：委托 <see cref="LogDecisionAuditSink"/>，结构化日志输出。
///
/// 默认 None 与历史行为逐字节等价，对 Golden 契约免疫；
/// 运维通过配置（而非代码）开启审计输出。
/// </summary>
public sealed class ConfigurableDecisionAuditSink : IDecisionAuditSink
{
	private readonly IDecisionAuditSink _inner;

	/// <summary>
	/// 创建可配置审计 Sink。
	/// </summary>
	/// <param name="options">审计配置（<c>DecisionAudit</c> 配置节）。</param>
	/// <param name="loggerFactory">日志工厂，供 Log 模式构造内部 Sink。</param>
	public ConfigurableDecisionAuditSink(
		IOptions<DecisionAuditOptions> options,
		ILoggerFactory loggerFactory)
	{
		var mode = (options ?? throw new System.ArgumentNullException(nameof(options)))
			.Value?.Mode ?? DecisionAuditMode.None;

		_inner = mode == DecisionAuditMode.Log
			? new LogDecisionAuditSink(
				(loggerFactory ?? throw new System.ArgumentNullException(nameof(loggerFactory)))
					.CreateLogger<LogDecisionAuditSink>())
			: new NoOpDecisionAuditSink();
	}

	/// <inheritdoc />
	public Task RecordAsync(
		QueryPlanDecisionAuditRecord record,
		CancellationToken ct = default)
	{
		return _inner.RecordAsync(record, ct);
	}
}
