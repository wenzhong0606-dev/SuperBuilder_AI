using System;
using System.Text.RegularExpressions;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

public partial class QueryPlanBuilder : IQueryPlanBuilder
{
	private void AddOrUpdateQueryField(
		QueryPlan plan,
		MetadataColumn column,
		string? aggregation)
	{

		var existing =
			plan.Fields.FirstOrDefault(
				x =>
					x.MetadataColumnId ==
					column.Id);




		if (existing == null)
		{

			plan.Fields.Add(
				new QueryField
				{
					MetadataColumnId =
						column.Id,

					ColumnName =
						column.ColumnName,

					DataType =
						column.DataType,

					Aggregation =
						string.IsNullOrWhiteSpace(
							aggregation)
						? "NONE"
						: aggregation.ToUpperInvariant()
				});



			return;

		}




		/*
		 * 如果原来没有聚合，
		 * 后面发现Metric需要聚合，
		 * 则更新聚合方式。
		 */

		if (string.IsNullOrWhiteSpace(
			existing.Aggregation)
			||
			existing.Aggregation.Equals(
				"NONE",
				StringComparison.OrdinalIgnoreCase))
		{

			if (!string.IsNullOrWhiteSpace(
				aggregation))
			{

				existing.Aggregation =
					aggregation.ToUpperInvariant();

			}

		}

	}






	/// <summary>
	/// 标准化SQL比较运算符。
	/// </summary>
	private async Task<List<QueryJoinCandidate>> BuildJoinsAsync(
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

		/*
		 * ============================================================
		 * Step 1
		 *
		 * 调用现有JOIN推理服务。
		 *
		 * QueryJoinInferenceService内部已经负责:
		 *
		 * 字段名称
		 * 数据类型
		 * 表名称
		 * MetadataSemantic
		 *
		 * 的关系判断。
		 * ============================================================
		 */

		var candidates =
			await _joinInference.InferAsync(
				metadataResults);

		if (candidates.Count == 0)
		{
			return new List<QueryJoinCandidate>();
		}

		/*
		 * ============================================================
		 * Step 2
		 *
		 * 只保留与当前主表直接相关的JOIN。
		 *
		 * Phase 1.6.2.1暂时不构建完整Join Graph。
		 *
		 * 例如:
		 *
		 * Customer
		 *     ↓
		 * SalesOrder
		 *
		 * 可以。
		 *
		 * Customer
		 *     ↓
		 * SalesOrder
		 *     ↓
		 * Product
		 *
		 * 当前阶段暂不自动扩展第二层。
		 * ============================================================
		 */

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

		/*
		 * ============================================================
		 * Step 3
		 *
		 * 同一个目标表可能存在多个JOIN候选。
		 *
		 * 例如:
		 *
		 * Customer.Id
		 *      ↕
		 * SalesOrder.CustomerId
		 *
		 * Customer.CustomerCode
		 *      ↕
		 * SalesOrder.CustomerCode
		 *
		 * 第一阶段只保留Confidence最高的一条。
		 * ============================================================
		 */

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
	private static QueryJoin BuildQueryJoin(
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
