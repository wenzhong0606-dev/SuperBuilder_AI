using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// 已学习到的一条纠正（含持久化身份，便于命中计数回写）。
/// </summary>
public sealed class LearnedCorrection
{
	/// <summary>规则主键。</summary>
	public long RuleId { get; set; }

	/// <summary>规则类型。</summary>
	public CorrectionKind Kind { get; set; }

	/// <summary>限定数据源（可空）。</summary>
	public long? DataSourceId { get; set; }

	/// <summary>规则载荷。</summary>
	public CorrectionPayload Payload { get; set; } = new();

	/// <summary>历史命中次数。</summary>
	public int HitCount { get; set; }
}

/// <summary>
/// 一次查询命中的学习规则集合。
/// </summary>
public sealed class CorrectionResolution
{
	/// <summary>命中的规则。</summary>
	public List<LearnedCorrection> Corrections { get; set; } = new();

	/// <summary>是否命中任一条。</summary>
	public bool Any => Corrections.Count > 0;

	/// <summary>首个表级覆盖目标物理表名（无则 null）。</summary>
	public string? TableOverride =>
		Corrections
			.FirstOrDefault(c => c.Kind == CorrectionKind.TableOverride)?
			.Payload.TableName;

	/// <summary>指定列上的值映射规则。</summary>
	public LearnedCorrection? ValueMapFor(string columnName) =>
		Corrections.FirstOrDefault(c =>
			c.Kind == CorrectionKind.ValueMap
			&& string.Equals(c.Payload.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));

	/// <summary>指定列上的外键链接规则。</summary>
	public LearnedCorrection? ForeignKeyFor(string columnName) =>
		Corrections.FirstOrDefault(c =>
			c.Kind == CorrectionKind.FkJoin
			&& string.Equals(c.Payload.SourceColumn, columnName, StringComparison.OrdinalIgnoreCase));

	/// <summary>指定列上的展示声明规则。</summary>
	public LearnedCorrection? DisplayFor(string columnName) =>
		Corrections.FirstOrDefault(c =>
			c.Kind == CorrectionKind.ColumnDisplay
			&& string.Equals(c.Payload.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// 自主学习纠错服务：显式纠正落库（Capture），下次同问句自动回放（Resolve）。
///
/// <para>
/// 严格 tenant+user 隔离；仅在用户显式纠正时写入，绝不从模糊问句静默学习（防规则中毒）。
/// </para>
/// </summary>
public interface ICorrectionLearningService
{
	/// <summary>
	/// 解析当前问句命中的学习规则。
	/// </summary>
	Task<CorrectionResolution> ResolveAsync(
		long tenantId,
		long? userId,
		string question,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// 落库一条显式纠正（同键聚合，命中计数累加）。
	/// </summary>
	Task CaptureAsync(
		long tenantId,
		long userId,
		long? dataSourceId,
		string question,
		CorrectionKind kind,
		CorrectionPayload payload,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// 把命中的表级纠正合成进问题（复用 Refine 的锁表句式），使下次无需再纠正。
	/// </summary>
	string ComposeLearnedQuestion(string question, CorrectionResolution resolution);

	/// <summary>
	/// 回写命中统计（命中次数 +1，记录最近命中时间）。
	/// </summary>
	Task MarkMatchedAsync(
		CorrectionResolution resolution,
		CancellationToken cancellationToken = default);
}
