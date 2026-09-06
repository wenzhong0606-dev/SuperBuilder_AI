using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 日志型 Ask 审计 Sink（M6-05）：以结构化日志（Information 级）输出审计记录。
///
/// 仅输出审计记录本身（敏感字段已在 <see cref="AskAuditRecordBuilder"/> 落库前脱敏），
/// 不向日志写入原始问题/SQL/结果样本值。
/// </summary>
public sealed class LogAskAuditSink : IAskAuditSink
{
	private readonly ILogger<LogAskAuditSink> _logger;

	/// <summary>创建日志型 Ask 审计 Sink。</summary>
	public LogAskAuditSink(ILogger<LogAskAuditSink> logger) => _logger = logger;

	/// <inheritdoc />
	public Task RecordAsync(
		AskAuditRecord record,
		CancellationToken ct = default)
	{
		_logger.LogInformation(
			"AskAudit outcome={Outcome} status={Status} decision={Decision} conv={ConversationId} turn={TurnIndex} ds=[{DataSourceIds}] sql={SqlSummary} dur={DurationMs}ms rows={Rows}",
			record.Outcome,
			record.Status,
			record.DecisionType?.ToString() ?? "n/a",
			record.ConversationId ?? "n/a",
			record.TurnIndex,
			record.AuthorizedDataSourceIds is null ? string.Empty : string.Join(",", record.AuthorizedDataSourceIds),
			record.SqlSummary ?? "n/a",
			record.DurationMs,
			record.ResultSample ?? "n/a");
		return Task.CompletedTask;
	}
}
