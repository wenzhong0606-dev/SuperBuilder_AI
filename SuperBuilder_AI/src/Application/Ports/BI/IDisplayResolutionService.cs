using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// 单列出译码命中记录。
/// </summary>
public sealed class DisplayResolutionHit
{
	/// <summary>结果列名（Rows 中的键）。</summary>
	public string ColumnName { get; set; } = string.Empty;

	/// <summary>命中策略：dictionary / foreign-key / value-map / learned-value-map / learned-foreign-key。</summary>
	public string Strategy { get; set; } = string.Empty;

	/// <summary>取数来源（{表}.{键列} → {展示列}），便于审计与排障。</summary>
	public string? Source { get; set; }

	/// <summary>实际替换成功的单元格数。</summary>
	public int ResolvedCellCount { get; set; }
}

/// <summary>
/// 译码执行报告：用于可观测性与测试断言；未命中或降级不影响主流程。
/// </summary>
public sealed class DisplayResolutionReport
{
	/// <summary>命中的列译码记录。</summary>
	public List<DisplayResolutionHit> Hits { get; set; } = new();

	/// <summary>被跳过的列及原因（未授权 / 失配 / 异常），一律降级不抛错。</summary>
	public List<string> Skipped { get; set; } = new();

	/// <summary>是否发生任何译码。</summary>
	public bool AnyApplied => Hits.Count > 0;

	/// <summary>被译码的列名集合。</summary>
	public IReadOnlyCollection<string> ResolvedColumns =>
		Hits.Select(h => h.ColumnName).ToList();
}

/// <summary>
/// 结果译码服务：对执行结果逐列做 code→text 富化。
///
/// <para>
/// 优先级：跨源字典表（如 PMIS）→ 同源外键元数据 → 学习规则 → 列内嵌值映射。
/// 字典/外键均走参数化 IN 点查，不做 SQL JOIN；未授权或结构失配一律降级（原值返回），绝不抛错。
/// </para>
/// </summary>
public interface IDisplayResolutionService
{
	/// <summary>
	/// 就地富化查询结果。
	/// </summary>
	/// <param name="plan">已定型的查询计划（提供主表名与数据源）。</param>
	/// <param name="result">执行结果（Rows 就地修改）。</param>
	/// <param name="tenantId">当前租户（隔离根）。</param>
	/// <param name="userId">当前用户（学习规则隔离；可空）。</param>
	/// <param name="authorizedDataSourceIds">已授权数据源集合；非空时跨源字典必须命中该集合。</param>
	/// <param name="learnedCorrections">
	/// 本次查询已解析的学习规则（由调用方一次 Resolve 后透传，避免重复查询；可空）。
	/// </param>
	/// <param name="cancellationToken">取消令牌。</param>
	Task<DisplayResolutionReport> EnrichAsync(
		QueryPlan plan,
		QueryResult result,
		long tenantId,
		long? userId,
		IReadOnlyCollection<long>? authorizedDataSourceIds,
		CorrectionResolution? learnedCorrections = null,
		CancellationToken cancellationToken = default);
}
