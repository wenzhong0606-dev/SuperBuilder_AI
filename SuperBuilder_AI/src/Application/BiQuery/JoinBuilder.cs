using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// JOIN 路径构建器。
///
/// 根据当前查询召回的 Metadata 结果推断 QueryPlan 中的 JOIN 关系。
///
/// 纯结构化重构：方法体从 QueryPlanBuilder 平移而来，逻辑与输出保持不变。
/// </summary>
public sealed class JoinBuilder
{
	private readonly IQueryJoinInferenceService
		_joinInference;

	/// <summary>
	/// 创建 JOIN 路径构建器。
	/// </summary>
	public JoinBuilder(
		IQueryJoinInferenceService joinInference)
	{
		_joinInference =
			joinInference
			?? throw new ArgumentNullException(
				nameof(joinInference));
	}

	/// <summary>
	/// 根据当前查询召回的Metadata结果推断QueryPlan中的JOIN关系。
	///
	/// Phase 1.6.2.1
	/// </summary>
	internal async Task<List<QueryJoinCandidate>> BuildJoinsAsync(
		MetadataTable mainTable,
		List<MetadataSemanticSearchResult> metadataResults)
	{
		if (mainTable == null)
		{
			throw new ArgumentNullException(
				nameof(mainTable));
		}

		if (metadataResults == null ||
			metadataResults.Count == 0)
		{
			return new List<QueryJoinCandidate>();
		}

		var candidates =
			await _joinInference.InferAsync(
				metadataResults);

		if (candidates.Count == 0)
		{
			return new List<QueryJoinCandidate>();
		}

		var mainTableCandidates =
			candidates
				.Where(x =>
					x.LeftTableId == mainTable.Id ||
					x.RightTableId == mainTable.Id)
				.OrderByDescending(x =>
					x.Confidence)
				.ToList();

		if (mainTableCandidates.Count == 0)
		{
			return new List<QueryJoinCandidate>();
		}

		var selected =
			mainTableCandidates
				.GroupBy(x =>
					x.LeftTableId == mainTable.Id
						? x.RightTableId
						: x.LeftTableId)
				.Select(g =>
					g.OrderByDescending(x =>
						x.Confidence)
					.First())
				.OrderByDescending(x =>
					x.Confidence)
				.Take(2)
				.ToList();

		return selected;
	}

	/// <summary>
	/// 将JOIN候选转换为QueryPlan使用的QueryJoin。
	/// </summary>
	internal QueryJoin BuildQueryJoin(
		QueryJoinCandidate candidate)
	{
		if (candidate == null)
		{
			throw new ArgumentNullException(
				nameof(candidate));
		}

		return new QueryJoin
		{
			LeftTableId =
				candidate.LeftTableId,

			LeftColumnId =
				candidate.LeftColumnId,

			RightTableId =
				candidate.RightTableId,

			RightColumnId =
				candidate.RightColumnId,

			LeftTableName =
				candidate.LeftTableName,

			LeftColumnName =
				candidate.LeftColumnName,

			RightTableName =
				candidate.RightTableName,

			RightColumnName =
				candidate.RightColumnName,

			JoinType =
				"INNER"
		};
	}
}
