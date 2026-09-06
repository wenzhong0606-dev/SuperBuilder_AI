using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 空操作模型成本遥测（M5-08 默认）：不采集、不输出，零行为变更。
/// </summary>
public sealed class NoOpModelCostTelemetry : IModelCostTelemetry
{
	/// <inheritdoc />
	public Task RecordAsync(
		QueryCostAssessment assessment,
		CostGovernanceVerdict verdict,
		CostGovernanceContext context,
		CancellationToken ct = default)
		=> Task.CompletedTask;
}
