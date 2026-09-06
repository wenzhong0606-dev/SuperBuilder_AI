using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// 模型 / 成本治理遥测（M5-08）：记录成本治理裁决（拒绝 / 降级）用于可观测性与反馈闭环。
/// 默认 <see cref="NoOpModelCostTelemetry"/>（零行为）；配置为 Log 时由
/// <see cref="LogModelCostTelemetry"/> 输出结构化日志。
/// </summary>
public interface IModelCostTelemetry
{
	/// <summary>
	/// 记录一次成本治理裁决。仅当 <paramref name="verdict"/> 非 NoAction 时被真实遥测实现关注。
	/// </summary>
	Task RecordAsync(
		QueryCostAssessment assessment,
		CostGovernanceVerdict verdict,
		CostGovernanceContext context,
		CancellationToken ct = default);
}
