namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 成本治理动作（M5-06）。
/// </summary>
public enum CostGovernanceAction
{
	/// <summary>无动作，正常放行。</summary>
	NoAction = 0,

	/// <summary>降级执行：注入防御性上限或标记受限，仍进入 SQL Builder。</summary>
	Degrade = 1,

	/// <summary>拒绝执行：禁止进入 SQL Builder。</summary>
	Reject = 2
}

/// <summary>
/// 成本治理裁决（M5-06）：策略层对评估信号的最终结论。
/// </summary>
public sealed record CostGovernanceVerdict
{
	/// <summary>治理动作。</summary>
	public CostGovernanceAction Action { get; init; } = CostGovernanceAction.NoAction;

	/// <summary>决策/拦截原因（写入 Decision.Reason 与 EarlyResponse.ErrorMessage）。</summary>
	public string? Reason { get; init; }

	/// <summary>降级时注入的计划行数上限；null 表示仅标记受限、不修改 Limit。</summary>
	public int? AppliedLimit { get; init; }
}

/// <summary>
/// 成本治理上下文（M5-06）：由 <see cref="ICostGovernanceContextResolver"/> 解析，
/// 携带租户与特权豁免信息，供策略层判断是否豁免治理。
/// </summary>
public sealed record CostGovernanceContext
{
	/// <summary>当前执行租户。</summary>
	public long TenantId { get; init; }

	/// <summary>是否豁免成本治理（平台管理员等特权角色）。</summary>
	public bool Bypass { get; init; }

	/// <summary>创建治理上下文。</summary>
	public CostGovernanceContext(long tenantId, bool bypass)
	{
		TenantId = tenantId;
		Bypass = bypass;
	}
}
