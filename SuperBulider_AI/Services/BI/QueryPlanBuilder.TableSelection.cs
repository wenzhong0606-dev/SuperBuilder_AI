using System;
using System.Collections.Generic;
using System.Linq;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

public partial class QueryPlanBuilder
{
	/// <summary>
	/// 候选表评分结果。
	///
	/// 注意：
	/// 当前阶段只负责把原有 SelectBestTable 中的评分过程
	/// 结构化，不改变实际生效的评分公式。
	/// </summary>
	private sealed class TableCandidateScore
	{
		public MetadataTable Table { get; init; } = null!;

		public int MatchedColumns { get; init; }

		public double ColumnScore { get; init; }

		public double TableScore { get; init; }

		public double Boost { get; init; }

		public int LocalMatchCount { get; init; }

		public double FinalScore { get; init; }
	}

	/// <summary>
	/// 根据 MetadataSemanticSearchResult 选择当前查询的最佳主表。
	///
	/// 当前阶段：
	///
	/// Metadata Search
	///      ↓
	/// Table Candidates
	///      ↓
	/// Candidate Scoring
	///      ↓
	/// Candidate Filtering
	///      ↓
	/// Candidate Ranking
	///      ↓
	/// Best Table
	///
	/// 不负责：
	///
	/// 1. Metadata 搜索
	/// 2. 字段解析
	/// 3. JOIN 推理
	/// 4. QueryPlan 组装
	/// </summary>
	private MetadataTable?
		SelectBestTable(
			List<MetadataSemanticSearchResult> results,
			List<string> businessTerms,
			QueryIntent? intent = null)
	{
		if (results == null || results.Count == 0)
		{
			return null;
		}

		/*
         * ============================================================
         * Step 1
         *
         * 按 MetadataTable 分组。
         *
         * 同一张表可能因为：
         *
         * 1. 多个 business term
         * 2. table vector
         * 3. column vector
         *
         * 被多次召回。
         *
         * 所以首先按照 Table.Id 聚合。
         * ============================================================
         */

		var groupedCandidates =
			results
				.Where(x => x.Table != null)
				.GroupBy(x => x.Table!.Id)
				.Select(group => new
				{
					Table = group.First().Table!,
					Results = group.ToList()
				})
				.ToList();

		if (groupedCandidates.Count == 0)
		{
			return null;
		}

		/*
         * ============================================================
         * Step 2
         *
         * 只保留有字段命中的候选表。
         *
         * 原有逻辑：
         *
         * 如果至少存在一张 MatchedColumns > 0 的表，
         * 那么 MatchedColumns == 0 的表全部淘汰。
         *
         * 如果所有表都没有字段命中，
         * 则保留所有候选。
         * ============================================================
         */

		var candidatesWithColumnMatches =
			groupedCandidates
				.Where(x =>
					x.Results
						.Where(r => r.Column != null)
						.Select(r => r.Column!.Id)
						.Distinct()
						.Any())
				.ToList();

		var candidates =
			candidatesWithColumnMatches.Count > 0
				? candidatesWithColumnMatches
				: groupedCandidates;

		/*
         * ============================================================
         * Step 3
         *
         * 对每张候选表只计算一次评分。
         *
         * 这是本次重构的核心。
         *
         * 原代码这里存在：
         *
         * 第一套 candidates 评分
         * +
         * 第二套 filteredCandidates / scoredCandidates 评分
         *
         * 第一套结果最终没有真正参与 return。
         *
         * 现在统一为单一评分管线。
         * ============================================================
         */

		var scoredCandidates =
			candidates
				.Select(x =>
					ScoreTableCandidate(
						x.Table,
						x.Results,
						businessTerms))
				.Where(x => x != null)
				.Cast<TableCandidateScore>()
				.OrderByDescending(x => x.FinalScore)
				.ThenByDescending(x => x.MatchedColumns)
				.ThenByDescending(x => x.ColumnScore)
				.ThenByDescending(x => x.TableScore)
				.ToList();

		/*
         * ============================================================
         * Step 4
         *
         * Diagnostics / Debug。
         *
         * 注意：
         *
         * 这里打印的就是最终真正参与排序的评分，
         * 不再打印一个实际上没有参与最终选择的第一轮评分。
         * ============================================================
         */

		// M5 修复：移除 Console.WriteLine 调试输出（注释本身已说明"不再打印第一轮评分"）

		return scoredCandidates
			.FirstOrDefault()
			?.Table;
	}

	/// <summary>
	/// 对单个候选表进行评分。
	///
	/// 当前评分公式严格保持原代码最终实际生效的第二轮：
	///
	/// MatchedColumns * 10
	/// + ColumnScore * 5
	/// + TableScore * 2
	/// + Boost
	/// + LocalMatchCount * 20
	///
	/// 注意：
	///
	/// 原代码第一轮曾计算 ExactFieldBoost / PairBoost，
	/// 但最终 return 使用的 scoredCandidates 并没有使用它们。
	///
	/// 为避免“代码整理”同时改变当前线上行为，
	/// 本次先不把它们加入最终分数。
	/// </summary>
	private TableCandidateScore?
		ScoreTableCandidate(
			MetadataTable table,
			List<MetadataSemanticSearchResult> results,
			List<string> businessTerms)
	{
		if (table == null)
		{
			return null;
		}

		/*
         * ============================================================
         * 1. 字段命中数量
         * ============================================================
         */

		var matchedColumns =
			results
				.Where(x => x.Column != null)
				.Select(x => x.Column!.Id)
				.Distinct()
				.Count();

		/*
         * ============================================================
         * 2. 字段最高语义分数
         * ============================================================
         */

		var columnScore =
			results
				.Where(x => x.Column != null)
				.Select(x => x.Score)
				.DefaultIfEmpty(0)
				.Max();

		/*
         * ============================================================
         * 3. 表级最高语义分数
         * ============================================================
         */

		var tableScore =
			results
				.Where(x => x.IsTableVector)
				.Select(x => x.Score)
				.DefaultIfEmpty(0)
				.Max();

		/*
         * ============================================================
         * 4. 表级文本 Boost
         *
         * 保留原有逻辑：
         *
         * businessTerms
         *      ↓
         * TableName / TableComment
         *      ↓
         * +50
         *
         * TableName / TableComment / SearchText
         *      ↓
         * +200
         *
         * Column Semantic
         *      ↓
         * +100
         *
         * ============================================================
         */

		var boost =
			CalculateTableBoost(
				table,
				businessTerms);

		/*
         * ============================================================
         * 5. 本地列匹配数量
         * ============================================================
         */

		var localMatchCount =
			CalculateLocalMatchCount(
				table,
				businessTerms);

		/*
         * ============================================================
         * 6. 最终评分
         *
         * 严格保持当前实际最终生效的评分公式。
         * ============================================================
         */

		var finalScore =
			matchedColumns * 10.0
			+
			columnScore * 5.0
			+
			tableScore * 2.0
			+
			boost
			+
			localMatchCount * 20.0;

		return new TableCandidateScore
		{
			Table = table,

			MatchedColumns =
				matchedColumns,

			ColumnScore =
				columnScore,

			TableScore =
				tableScore,

			Boost =
				boost,

			LocalMatchCount =
				localMatchCount,

			FinalScore =
				finalScore
		};
	}

	/// <summary>
	/// 计算表级业务文本匹配 Boost。
	///
	/// 保留原有规则，不修改权重。
	/// </summary>
	private double
		CalculateTableBoost(
			MetadataTable table,
			List<string> businessTerms)
	{
		if (table == null ||
			businessTerms == null ||
			businessTerms.Count == 0)
		{
			return 0.0;
		}

		var boost = 0.0;

		try
		{
			var tableNameText =
				NormalizeText(
					table.TableName);

			var tableCommentText =
				NormalizeText(
					table.TableComment);

			/*
             * --------------------------------------------------------
             * 第一层：
             *
             * TableName / TableComment
             *
             * +50
             * --------------------------------------------------------
             */

			foreach (var term in businessTerms)
			{
				var normalizedTerm =
					NormalizeText(term);

				if (string.IsNullOrWhiteSpace(
					normalizedTerm))
				{
					continue;
				}

				if (
					(
						!string.IsNullOrWhiteSpace(
							tableNameText)
						&&
						tableNameText.Contains(
							normalizedTerm,
							StringComparison.OrdinalIgnoreCase)
					)
					||
					(
						!string.IsNullOrWhiteSpace(
							tableCommentText)
						&&
						tableCommentText.Contains(
							normalizedTerm,
							StringComparison.OrdinalIgnoreCase)
					)
				)
				{
					boost += 50.0;
					break;
				}
			}

			/*
             * --------------------------------------------------------
             * 第二层：
             *
             * TableName
             * TableComment
             * SearchText
             *
             * +200
             *
             * 或：
             *
             * Column Semantic
             *
             * +100
             * --------------------------------------------------------
             */

			var tableSearchText =
				NormalizeText(
					table.SearchText);

			foreach (var term in businessTerms)
			{
				var normalizedTerm =
					NormalizeText(term);

				if (string.IsNullOrWhiteSpace(
					normalizedTerm))
				{
					continue;
				}

				/*
                 * 表级文本匹配。
                 */

				if (
					(
						!string.IsNullOrWhiteSpace(
							tableNameText)
						&&
						tableNameText.Contains(
							normalizedTerm,
							StringComparison.OrdinalIgnoreCase)
					)
					||
					(
						!string.IsNullOrWhiteSpace(
							tableCommentText)
						&&
						tableCommentText.Contains(
							normalizedTerm,
							StringComparison.OrdinalIgnoreCase)
					)
					||
					(
						!string.IsNullOrWhiteSpace(
							tableSearchText)
						&&
						tableSearchText.Contains(
							normalizedTerm,
							StringComparison.OrdinalIgnoreCase)
					)
				)
				{
					boost += 200.0;
					break;
				}

				/*
                 * 列语义匹配。
                 */

				if (table.Columns == null)
				{
					continue;
				}

				var columnSemanticMatched =
					false;

				foreach (var column in table.Columns)
				{
					try
					{
						if (column.Semantic == null)
						{
							continue;
						}

						var semanticText =
							NormalizeText(
								string.Join(
									" ",
									new[]
									{
										column.Semantic.BusinessMeaning,
										column.Semantic.Keywords,
										column.Semantic.Synonyms,
										column.Semantic.ExampleQuestions,
										column.Semantic.SearchText
									}));

						if (
							!string.IsNullOrWhiteSpace(
								semanticText)
							&&
							semanticText.Contains(
								normalizedTerm,
								StringComparison.OrdinalIgnoreCase)
						)
						{
							boost += 100.0;
							columnSemanticMatched = true;
							break;
						}
					}
					catch
					{
						/*
                         * 单列 Semantic 数据异常不能影响
                         * 整个候选表评分。
                         */
					}
				}

				if (columnSemanticMatched)
				{
					break;
				}
			}
		}
		catch
		{
			/*
             * Boost 属于辅助评分。
             *
             * 即使 Metadata 数据异常，
             * 也不能导致整个 QueryPlan 生成失败。
             */
		}

		return boost;
	}

	/// <summary>
	/// 计算候选表内部列与业务词的直接匹配数量。
	///
	/// 保留原有规则：
	///
	/// ColumnName
	/// ColumnComment
	/// IsRelatedBusinessText
	/// </summary>
	private int
		CalculateLocalMatchCount(
			MetadataTable table,
			List<string> businessTerms)
	{
		if (table == null ||
			table.Columns == null ||
			businessTerms == null ||
			businessTerms.Count == 0)
		{
			return 0;
		}

		var localMatchCount = 0;

		try
		{
			foreach (var term in businessTerms)
			{
				var normalizedTerm =
					NormalizeText(term);

				if (string.IsNullOrWhiteSpace(
					normalizedTerm))
				{
					continue;
				}

				foreach (var column in table.Columns)
				{
					try
					{
						var columnName =
							NormalizeText(
								column.ColumnName);

						var columnComment =
							NormalizeText(
								column.ColumnComment);

						var matched =
							(
								!string.IsNullOrWhiteSpace(
									columnName)
								&&
								columnName.Contains(
									normalizedTerm,
									StringComparison.OrdinalIgnoreCase)
							)
							||
							(
								!string.IsNullOrWhiteSpace(
									columnComment)
								&&
								columnComment.Contains(
									normalizedTerm,
									StringComparison.OrdinalIgnoreCase)
							)
							||
							IsRelatedBusinessText(
								term,
								column);

						if (matched)
						{
							localMatchCount++;
						}
					}
					catch
					{
						/*
                         * 单列异常不影响其他列。
                         */
					}
				}
			}
		}
		catch
		{
			/*
             * 本地匹配属于辅助评分。
             */
		}

		return localMatchCount;
	}
}