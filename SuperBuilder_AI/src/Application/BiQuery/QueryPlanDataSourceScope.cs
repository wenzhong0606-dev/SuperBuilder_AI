using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// <see cref="IQueryPlanDataSourceScope"/> 的默认实现（M5-04）。
///
/// 从 <see cref="QueryPlanBuilder"/> 抽出"选表前的数据源候选收敛"逻辑，
/// 使 Builder 回归"只构造"职责。逻辑与重构前逐字节一致：
///
///   - authorizedDataSourceIds 非 null：仅保留属于授权集合的候选；
///     过滤后为空 → 抛 SuperBuilderException(DataSourceForbidden, 403)。
///   - requestedDataSourceId &gt; 0：仅保留属于该数据源的候选；
///     收敛后为空 → 抛 InvalidOperationException（明确报错，不静默回退）。
///   - 两个参数均为 null/空：原样返回（Golden / 默认推断路径不变）。
/// </summary>
public sealed class QueryPlanDataSourceScope : IQueryPlanDataSourceScope
{
	/// <inheritdoc />
	public Task<List<MetadataSemanticSearchResult>> ScopeAsync(
		List<MetadataSemanticSearchResult> candidates,
		long? requestedDataSourceId,
		IReadOnlyCollection<long>? authorizedDataSourceIds,
		string? question = null,
		CancellationToken ct = default)
	{
		if (candidates is null)
			return Task.FromResult(new List<MetadataSemanticSearchResult>());

		var results = candidates;

		// P0-05：在选表前收敛到当前用户被显式授权的数据源集合。
		// null 仅用于 Golden / 内部兼容路径；认证 API 必须传入非 null 集合。
		if (authorizedDataSourceIds is not null)
		{
			var allowed =
				authorizedDataSourceIds
					.Where(id => id > 0)
					.ToHashSet();

			results =
				results
					.Where(r =>
						r.Table != null
						&& allowed.Contains(r.Table.DataSourceId))
					.ToList();

			if (results.Count == 0)
				throw SuperBuilderException.FromCode(
					ErrorCodes.DataSourceForbidden,
					403);
		}

		// P0-01：显式指定数据源时，约束元数据搜索 / 选表范围。
		// 必须在选表前生效：仅保留属于该数据源的候选
		// （表 / 列 / 语义向量均通过 MetadataTable.DataSourceId 关联到父表，
		// 因此按 Table.DataSourceId 过滤即可把整个结果集收敛到目标数据源）。
		if (requestedDataSourceId is { } dsId && dsId > 0)
		{
			var scopedResults =
				results
					.Where(r =>
						r.Table != null
						&& r.Table.DataSourceId == dsId)
					.ToList();

			if (scopedResults.Count == 0)
			{
				throw new InvalidOperationException(
					$"所选数据源（Id={dsId}）下未找到与问题相关的表：{question}");
			}

			results = scopedResults;
		}

		return Task.FromResult(results);
	}
}
