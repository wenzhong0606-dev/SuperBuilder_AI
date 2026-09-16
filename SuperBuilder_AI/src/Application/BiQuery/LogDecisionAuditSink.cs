using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 日志型审计 Sink（M5-09）：以结构化日志（Information 级）输出审计记录。
///
/// 不改动任何 Pipeline 行为或返回值，仅把审计记录写入日志管道，
/// 供运维/可观测后端检索。字段以结构化状态对象呈现，便于日志聚合。
/// </summary>
public sealed class LogDecisionAuditSink : IDecisionAuditSink
{
	private readonly ILogger<LogDecisionAuditSink> _logger;

	/// <summary>创建日志型审计 Sink。</summary>
	public LogDecisionAuditSink(ILogger<LogDecisionAuditSink> logger)
	{
		_logger = logger
			?? throw new System.ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task RecordAsync(
		QueryPlanDecisionAuditRecord record,
		CancellationToken ct = default)
	{
		_logger.LogInformation(
			"QueryPlan Decision Audit | Outcome={Outcome} Decision={Decision} Conf={Conf} " +
			"Question={Question} Intent={Intent} Tables={Tables} Model={Model} " +
			"Repair={RepairStatus}({RepairAttempts}) Sql={SqlPresent} CorrelationId={CorrelationId}",
			record.Outcome,
			record.DecisionType,
			record.ConfidenceScore,
			record.Question,
			record.IntentType,
			record.PlanTableNames.Count,
			record.Model,
			record.RepairStatus,
			record.RepairAttempts,
			record.Sql is not null,
			record.CorrelationId);

		return Task.CompletedTask;
	}
}
