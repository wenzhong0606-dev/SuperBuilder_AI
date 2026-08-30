using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Interfaces.BI.Dashboard;

/// <summary>
/// 组件取数解析器端口（P6.3）。
///
/// 把 <see cref="WidgetQueryDsl"/> 经 QueryPlanPipeline 执行为结果行。
/// 默认实现 <c>QueryPlanWidgetDataResolver</c> 对照 QueryPlanPipeline 接入。
/// </summary>
public interface IWidgetDataResolver
{
	/// <summary>解析组件取数。</summary>
	/// <param name="query">组件取数定义（可为 null，表示无需取数）。</param>
	/// <param name="context">平台运行时上下文。</param>
	/// <param name="effectiveFilters">全局与组件级筛选器下推后的完整列表。</param>
	/// <param name="cancellationToken">取消令牌。</param>
	Task<WidgetDataResult> ResolveAsync(
		WidgetQueryDsl? query,
		PlatformContext context,
		IReadOnlyList<FilterDsl> effectiveFilters,
		CancellationToken cancellationToken = default);
}

/// <summary>组件取数解析结果（P6.3）。</summary>
public sealed class WidgetDataResult
{
	/// <summary>是否成功解析到数据。</summary>
	public bool Resolved { get; set; }

	/// <summary>错误信息（解析失败/执行失败时）。</summary>
	public string? Error { get; set; }

	/// <summary>结果列名。</summary>
	public List<string> Columns { get; set; } = new();

	/// <summary>结果行。</summary>
	public List<Dictionary<string, object?>> Rows { get; set; } = new();

	/// <summary>QueryPlanPipeline Decision Gate 结论：Proceed / Blocked / Error / NoQuery。</summary>
	public string Decision { get; set; } = "NoQuery";
}
