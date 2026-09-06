using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.AI;

namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// 数据源候选收敛服务（M5-04）。
///
/// 把 <see cref="QueryPlanBuilder"/> 原先内联的"选表前权限/数据源约束"逻辑
/// （P0-05 授权数据源集合收敛、P0-01 显式请求数据源收敛）抽离为独立、可测试、
/// 可替换的协作服务，使 Builder 回归"只构造、不承担权限/安全判断"的职责。
///
/// 行为契约（与重构前逐字节一致）：
///   - 两个数据源参数均为 null/空时：原样返回候选集（Golden / 默认路径不变）；
///   - authorizedDataSourceIds 非 null：过滤出属于授权集合的候选，
///     若过滤后为空则抛出 DataSourceForbidden(403)；
///   - requestedDataSourceId &gt; 0：收敛到该数据源的候选，
///     若收敛后为空则抛出 InvalidOperationException（明确报错，不静默回退）。
/// </summary>
public interface IQueryPlanDataSourceScope
{
	/// <summary>
	/// 对元数据候选集执行授权/请求数据源收敛，返回收敛后的候选集。
	/// </summary>
	/// <param name="candidates">元数据语义搜索返回的候选集。</param>
	/// <param name="requestedDataSourceId">
	/// 显式请求的数据源约束；为 null 或 &lt;=0 时保持原有推断行为（不收敛）。
	/// </param>
	/// <param name="authorizedDataSourceIds">
	/// 当前用户被显式授权的数据源集合；为 null 时不做授权收敛（Golden / 内部兼容路径）。
	/// </param>
	/// <param name="question">
	/// 用户原始问题文本（仅用于"显式请求数据源下无匹配候选"时的报错文案，便于定位）。
	/// 为 null 时回退到空文案。
	/// </param>
	/// <param name="ct">取消令牌。</param>
	Task<List<MetadataSemanticSearchResult>> ScopeAsync(
		List<MetadataSemanticSearchResult> candidates,
		long? requestedDataSourceId,
		IReadOnlyCollection<long>? authorizedDataSourceIds,
		string? question = null,
		CancellationToken ct = default);
}
