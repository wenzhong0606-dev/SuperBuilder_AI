using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 结构化日志型模型成本遥测（M5-08）：对拒绝 / 降级裁决输出 Warning 级日志，
/// 供平台日志聚合与告警。仅在 <c>CostGovernance:TelemetryMode=Log</c> 时由
/// <see cref="ConfigurableModelCostTelemetry"/> 选用。
/// </summary>
public sealed class LogModelCostTelemetry : IModelCostTelemetry
{
	private readonly ILogger<LogModelCostTelemetry> _logger;

	/// <summary>创建日志型遥测。</summary>
	public LogModelCostTelemetry(ILogger<LogModelCostTelemetry> logger)
	{
		_logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task RecordAsync(
		QueryCostAssessment assessment,
		CostGovernanceVerdict verdict,
		CostGovernanceContext context,
		CancellationToken ct = default)
	{
		if (verdict.Action == CostGovernanceAction.NoAction)
			return Task.CompletedTask;

		_logger.LogWarning(
			"CostGovernance verdict={Action} tenant={Tenant} joins={Joins} tables={Tables} unbounded={Unbounded} reason={Reason}",
			verdict.Action, context.TenantId, assessment.JoinCount, assessment.TableCount,
			assessment.IsUnbounded, verdict.Reason ?? string.Empty);

		return Task.CompletedTask;
	}
}
