using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 阈值型查询成本治理策略（M5-06 默认实现）。
///
/// 默认三项守卫（无界守卫 / JOIN 守卫 / 模型成本守卫）均关闭，
/// 即默认零行为变更；由配置显式开启后才生效（渐进式治理）。
/// </summary>
public sealed class ThresholdCostGovernancePolicy : ICostGovernancePolicy
{
	private readonly CostGovernanceOptions _options;

	/// <summary>创建阈值治理策略。</summary>
	public ThresholdCostGovernancePolicy(IOptions<CostGovernanceOptions> options)
	{
		_options = options?.Value ?? new CostGovernanceOptions();
	}

	/// <inheritdoc />
	public CostGovernanceVerdict Evaluate(
		QueryCostAssessment a,
		QueryPlanDecision? currentDecision,
		CostGovernanceContext context)
	{
		if (context.Bypass)
			return NoAction();

		// 拒绝：JOIN 扇出超过硬上限
		if (_options.EnableJoinGuard && a.JoinCount > _options.MaxJoins)
			return Reject(
				$"查询涉及 {a.JoinCount} 个 JOIN，超过成本治理上限 {_options.MaxJoins}，" +
				"拒绝执行以避免笛卡尔积/全表扫描风暴。");

		// 拒绝：模型成本超过治理阈值（需接入遥测）
		if (_options.EnableModelCostGuard
			&& _options.RejectModelCostTier != ModelCostTier.Low
			&& a.ModelCostTier >= _options.RejectModelCostTier)
			return Reject(
				$"模型成本评级为 {a.ModelCostTier}，达到成本治理拒绝阈值 " +
				$"{_options.RejectModelCostTier}，拒绝执行。");

		// 降级：无界查询自动注入防御性行数上限
		if (_options.EnableUnboundedGuard && a.IsUnbounded)
			return Degrade(
				_options.UnboundedDefaultLimit,
				$"查询未设置结果行数上限（无界），已降级为受限执行并自动注入防御性上限 " +
				$"{_options.UnboundedDefaultLimit}。");

		// 降级：JOIN 数超过预警阈值（保留既有 Limit，仅标记受限执行）
		if (_options.EnableJoinGuard && a.JoinCount > _options.WarnJoins)
			return Degrade(
				null,
				$"查询涉及 {a.JoinCount} 个 JOIN，超过成本治理预警阈值 " +
				$"{_options.WarnJoins}，已降级为受限执行。");

		return NoAction();
	}

	private static CostGovernanceVerdict NoAction() => new();

	private static CostGovernanceVerdict Reject(string reason) =>
		new() { Action = CostGovernanceAction.Reject, Reason = reason };

	private static CostGovernanceVerdict Degrade(int? appliedLimit, string reason) =>
		new() { Action = CostGovernanceAction.Degrade, Reason = reason, AppliedLimit = appliedLimit };
}
