using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Application.Common.Options;

/// <summary>
/// 查询成本治理配置（M5-06）。
/// <para>
/// 设计原则：默认全部关闭（零默认行为变更），由平台按环境显式开启，
/// 支持渐进式治理。所有阈值均可配置，便于按租户/数据源差异化。
/// </para>
/// </summary>
public sealed class CostGovernanceOptions
{
	public const string SectionName = "CostGovernance";

	/// <summary>无界查询（未设置 LIMIT）自动注入防御性上限并降级执行。默认 false。</summary>
	public bool EnableUnboundedGuard { get; set; }

	/// <summary>无界查询注入的默认行数上限。</summary>
	public int UnboundedDefaultLimit { get; set; } = 1000;

	/// <summary>JOIN 数量治理（预警 + 拒绝）。默认 false。</summary>
	public bool EnableJoinGuard { get; set; }

	/// <summary>JOIN 数超过此值降级为受限执行。</summary>
	public int WarnJoins { get; set; } = 3;

	/// <summary>JOIN 数超过此值直接拒绝执行。</summary>
	public int MaxJoins { get; set; } = 6;

	/// <summary>模型成本治理（需接入 LLM 成本评级遥测）。默认 false。</summary>
	public bool EnableModelCostGuard { get; set; }

	/// <summary>模型成本评级达到此值及以上即拒绝执行。</summary>
	public ModelCostTier RejectModelCostTier { get; set; } = ModelCostTier.High;
}
