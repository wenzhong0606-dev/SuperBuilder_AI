using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Application.Common.Options;

/// <summary>
/// 查询成本治理配置（M5-06）。
/// <para>
/// 设计原则：默认全部关闭（零默认行为变更），由平台按环境显式开启，
/// 支持渐进式治理。所有阈值均可配置，便于按租户/数据源差异化。
/// </para>
/// </summary>
/// <summary>成本治理主开关（M5-08）。默认 Off = 整条成本治理关闭（零默认行为变更）。</summary>
public enum CostGovernanceMode
{
	/// <summary>关闭：所有成本治理裁决均为 NoAction（默认，向后兼容 M5-06 默认全关）。</summary>
	Off = 0,

	/// <summary>阈值模式：按 Enable* 守卫生效（渐进式治理）。</summary>
	Threshold = 1
}

/// <summary>成本治理遥测模式（M5-08）。默认 None = 不采集（零默认行为变更）。</summary>
public enum CostTelemetryMode
{
	/// <summary>不采集遥测（默认）。</summary>
	None = 0,

	/// <summary>结构化日志采集（Warning 级）。</summary>
	Log = 1
}

public sealed class CostGovernanceOptions
{
	public const string SectionName = "CostGovernance";

	/// <summary>成本治理主开关。默认 Off（零默认行为变更）；设为 Threshold 后各 Enable* 守卫才生效。</summary>
	public CostGovernanceMode Mode { get; set; } = CostGovernanceMode.Off;

	/// <summary>模型成本遥测模式。默认 None（零默认行为变更）。</summary>
	public CostTelemetryMode TelemetryMode { get; set; } = CostTelemetryMode.None;

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
