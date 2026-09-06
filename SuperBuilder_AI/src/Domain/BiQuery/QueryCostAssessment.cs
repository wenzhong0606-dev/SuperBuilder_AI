namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 查询成本严重度（M5-06，由 <see cref="ICostGovernancePolicy"/> 判定）。
/// </summary>
public enum QueryCostSeverity
{
	/// <summary>无成本风险，正常执行。</summary>
	None = 0,

	/// <summary>受限执行（降级）：注入防御性行数上限或标记受限，仍可执行。</summary>
	LimitedExecution = 1,

	/// <summary>拒绝执行：成本超过治理阈值，禁止进入 SQL Builder。</summary>
	Reject = 2
}

/// <summary>
/// 模型成本评级（M5-06）。
///
/// 默认实现不接入遥测，恒为 <see cref="Low"/>；治理默认关闭，
/// 评级仅作为可扩展点，待接入 LLM 成本估算后启用。
/// </summary>
public enum ModelCostTier
{
	/// <summary>低成本。</summary>
	Low = 0,

	/// <summary>中等成本。</summary>
	Medium = 1,

	/// <summary>高成本。</summary>
	High = 2
}

/// <summary>
/// 查询成本评估原始信号（M5-06）。
///
/// 由 <see cref="IQueryCostClassifier"/> 从 QueryPlan 抽取，不含阈值判断；
/// 阈值判定交由 <see cref="ICostGovernancePolicy"/>，保证单一职责。
/// </summary>
public sealed record QueryCostAssessment
{
	/// <summary>JOIN 数量。</summary>
	public int JoinCount { get; init; }

	/// <summary>参与的表数量（主表 + 各 JOIN）。</summary>
	public int TableCount { get; init; }

	/// <summary>是否无界（未设置任何结果行数上限）。</summary>
	public bool IsUnbounded { get; init; }

	/// <summary>请求的结果行数上限（plan.Limit 或 intent.Limit）；无则为 null。</summary>
	public int? RequestedLimit { get; init; }

	/// <summary>模型成本评级（默认 Low，未接入遥测）。</summary>
	public ModelCostTier ModelCostTier { get; init; } = ModelCostTier.Low;

	/// <summary>评估过程中的提示信息（供审计/解释使用，不影响决策）。</summary>
	public List<string> Notes { get; init; } = new();
}
