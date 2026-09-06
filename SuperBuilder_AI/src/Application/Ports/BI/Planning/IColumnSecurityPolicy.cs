using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// 列级安全策略应用结果（M5-05）。
/// </summary>
public sealed class ColumnSecurityResult
{
	/// <summary>被拦截（已移出 Plan）的列引用。</summary>
	public List<BlockedColumnReference> Blocked { get; set; } = new();

	/// <summary>是否对 Plan 做了任何修改（移除了至少一个引用）。</summary>
	public bool Modified => Blocked.Count > 0;

	/// <summary>
	/// 主投影（Fields + Metrics + Dimensions）在被拦截后是否全部清空。
	/// 此时 Plan 已无法执行，应由管线以 403 拒绝，而非静默发出空查询。
	/// </summary>
	public bool RequiresRejection { get; set; }
}

/// <summary>
/// 被列级安全拦截的单个列引用记录（用于审计 / Explainability）。
/// </summary>
public sealed class BlockedColumnReference
{
	/// <summary>来源集合：Field / Dimension / Order / Join / Filter / Metric。</summary>
	public string Collection { get; set; } = string.Empty;

	/// <summary>受限列 MetadataColumnId（按列名反查不到时为 null）。</summary>
	public long? ColumnId { get; set; }

	/// <summary>受限列物理名。</summary>
	public string? ColumnName { get; set; }

	/// <summary>受限列所属表名（若可解析）。</summary>
	public string? TableName { get; set; }

	/// <summary>拦截原因。</summary>
	public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 列级安全策略：将未授权受限列从 QueryPlan 中剔除
/// （不进入 Plan / SQL / 结果），满足 M5-05 验收"未授权 / 脱敏字段不进入 Plan"。
/// </summary>
public interface IColumnSecurityPolicy
{
	/// <summary>
	/// 对 <paramref name="plan"/> 应用列级安全：剔除未授权受限列。
	/// </summary>
	/// <param name="plan">待净化的查询计划（会被原地改写）。</param>
	/// <param name="ctx">列级安全运行时上下文。</param>
	/// <param name="validationContext">
	/// 验证上下文（提供 表→列 映射，用于把 Filter / Metric 的纯列名反查为 MetadataColumnId）。
	/// 可为 null（此时仅处理带 MetadataColumnId 的引用）。
	/// </param>
	Task<ColumnSecurityResult> ApplyAsync(
		QueryPlan plan,
		ColumnSecurityContext ctx,
		QueryPlanValidationContext? validationContext,
		CancellationToken ct = default);
}
