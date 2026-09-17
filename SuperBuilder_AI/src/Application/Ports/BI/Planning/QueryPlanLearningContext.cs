using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// 本次查询命中的「自主学习规则」上下文（Phase 4）。
///
/// <para>
/// 只承载一个事实：本次查询回放了哪些类型的学习规则。
/// 它被透传到 Confidence 阶段，作为正向证据写进
/// <see cref="SuperBuilder_AI.Models.BI.QueryPlanConfidenceEvidence"/>，
/// 使「用了学习规则」可见、可审计 —— 与用户当轮显式表纠正
/// （<c>TableCorrectionHonored</c>）同等待遇。
/// </para>
///
/// <para>
/// 该上下文不参与 SQL 生成，也不改变召回与计划构建。
/// 为 <c>null</c> 或空集合时，全链路行为必须与引入前逐字节一致（Golden 零回归）。
/// </para>
/// </summary>
public sealed class QueryPlanLearningContext
{
	/// <summary>未命中任何学习规则时的共享空实例。</summary>
	public static readonly QueryPlanLearningContext Empty = new();

	/// <summary>
	/// 本次命中的学习规则类型（已去重，保持稳定顺序）。
	/// </summary>
	public IReadOnlyList<CorrectionKind> RuleKinds { get; init; } =
		Array.Empty<CorrectionKind>();

	/// <summary>
	/// 本次命中的学习规则条数（未去重，用于审计与计量）。
	/// </summary>
	public int RuleCount { get; init; }

	/// <summary>是否命中任一条学习规则。</summary>
	public bool Any => RuleCount > 0;

	/// <summary>
	/// 命中的规则里是否包含表级覆盖（<see cref="CorrectionKind.TableOverride"/>）。
	/// 为真表示本次计划的主表是被学习规则锁定的，而非完全由语义检索得出。
	/// </summary>
	public bool HasTableOverride =>
		RuleKinds.Contains(CorrectionKind.TableOverride);

	/// <summary>
	/// 由命中的规则类型序列构造上下文；序列为空时返回 <see cref="Empty"/>。
	/// </summary>
	public static QueryPlanLearningContext From(
		IReadOnlyList<CorrectionKind>? kinds)
	{
		if (kinds is null || kinds.Count == 0)
		{
			return Empty;
		}

		return new QueryPlanLearningContext
		{
			RuleKinds = kinds.Distinct().ToList(),
			RuleCount = kinds.Count
		};
	}
}
