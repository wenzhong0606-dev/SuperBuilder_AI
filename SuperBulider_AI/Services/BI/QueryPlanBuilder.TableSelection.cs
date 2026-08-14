using System;
using System.Text.RegularExpressions;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.AI;
using SuperBulider_AI.Models.BI;
using SuperBulider_AI.Models.Metadata;

namespace SuperBulider_AI.Services.BI;

public partial class QueryPlanBuilder : IQueryPlanBuilder
{
	private MetadataTable?
		SelectBestTable(
			List<MetadataSemanticSearchResult> results,
			List<string> businessTerms,
			QueryIntent? intent = null)
	{


		var candidates =
			results

			.Where(
				x =>
					x.Table != null)

			.GroupBy(
				x =>
					x.Table!.Id)

			.Select(
				group =>
				{

					var table =
						group.First().Table!;



					var columnResults =
						group
						.Where(
							x =>
								x.Column != null)
						.ToList();



					var tableResults =
						group
						.Where(
							x =>
								x.IsTableVector)
						.ToList();




					/*
					 * 字段命中数量。
					 */

					var matchedColumns =
						columnResults
						.Select(
							x =>
								x.Column!.Id)
						.Distinct()
						.Count();




					/*
					 * 字段最高语义分数。
					 */

					var columnScore =
						columnResults
						.Select(
							x =>
								x.Score)
						.DefaultIfEmpty(0)
						.Max();




					/*
					 * 表级最高语义分数。
					 */

					var tableScore =
						tableResults
						.Select(
							x =>
								x.Score)
						.DefaultIfEmpty(0)
						.Max();





					/*
					 * 综合评分。
					 *
					 * 字段命中数量权重最高。
					 *
					 * 因为业务查询最终还是需要真实字段。
					 */

					// 根据业务关键词对表名/表注释进行加分，避免召回语义相近但业务不相关的表被错误选中。
					var boost = 0.0;
					try
					{
						var tableNameText = NormalizeText(table.TableName);
						var tableCommentText = NormalizeText(table.TableComment);
						foreach (var term in businessTerms)
						{
							var t = NormalizeText(term);
							if (string.IsNullOrWhiteSpace(t))
								continue;

							if ((!string.IsNullOrWhiteSpace(tableNameText) && tableNameText.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
								(!string.IsNullOrWhiteSpace(tableCommentText) && tableCommentText.Contains(t, StringComparison.OrdinalIgnoreCase)))
							{
								// 发现表名或表注释包含业务关键词，给予显著加分
								boost += 50.0;
								break;
							}
						}

						// 使用通用的基于元数据文本匹配的规则替代手工关键词列表：
						// - 如果 businessTerms 与 table.SearchText/tableName/tableComment 存在子串匹配，给予较高权重
						// - 否则如果 businessTerms 与任意列的 Semantic 文本存在匹配，给予中等权重
						try
						{
							var tableSearchText = NormalizeText(table.SearchText);
							foreach (var term in businessTerms)
							{
								var t = NormalizeText(term);
								if (string.IsNullOrWhiteSpace(t)) continue;

								// 表级文本匹配（表名/注释/预计算搜索文本）
								if ((!string.IsNullOrWhiteSpace(tableNameText) && tableNameText.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
									(!string.IsNullOrWhiteSpace(tableCommentText) && tableCommentText.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
									(!string.IsNullOrWhiteSpace(tableSearchText) && tableSearchText.Contains(t, StringComparison.OrdinalIgnoreCase)))
								{
									boost += 200.0;
									break;
								}

								// 列语义字段匹配（Semantic.BusinessMeaning/Keywords/Synonyms/ExampleQuestions/SearchText）
								if (table.Columns != null)
								{
									var colMatched = false;
									foreach (var col in table.Columns)
									{
										try
										{
											if (col.Semantic == null) continue;
											var semText = NormalizeText(string.Join(" ", new[] { col.Semantic.BusinessMeaning, col.Semantic.Keywords, col.Semantic.Synonyms, col.Semantic.ExampleQuestions, col.Semantic.SearchText }));
											if (!string.IsNullOrWhiteSpace(semText) && semText.Contains(t, StringComparison.OrdinalIgnoreCase))
											{
												boost += 100.0;
												colMatched = true;
												break;
											}
										}
										catch { }
									}
									if (colMatched) break;
								}
							}
						}
						catch { }
					}
					catch
					{
						// 忽略任何解析异常，继续正常评分
					}

					// 计算表内本地文本与业务词的直接匹配数（列级别），作为额外加分。
					var localMatchCount = 0;
					try
					{
						if (table.Columns != null)
						{
							foreach (var term in businessTerms)
							{
								var t = NormalizeText(term);
								if (string.IsNullOrWhiteSpace(t))
									continue;

								foreach (var col in table.Columns)
								{
									try
									{
										// 精确或包含匹配提升优先级
										var colName = NormalizeText(col.ColumnName);
										var colComment = NormalizeText(col.ColumnComment);
										if ((!string.IsNullOrWhiteSpace(colName) && colName.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
											(!string.IsNullOrWhiteSpace(colComment) && colComment.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
											IsRelatedBusinessText(term, col))
										{
											localMatchCount++;
										}
									}
									catch
									{
										// 忽略单列错误
									}
								}
							}
						}
					}
					catch
					{
						// 忽略任何解析异常
					}

					// 如果表中存在与用户意图中明确指定的 metric.field 或 filter.field 完全匹配的列，给予较大加权。
					var exactFieldBoost = 0.0;
					try
					{
						if (intent != null && table.Columns != null)
						{
							var metricFields = intent.Metrics?.Select(m => NormalizeText(m.Field ?? m.Name)).Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
							var filterFields = intent.Filters?.Select(f => NormalizeText(f.Field)).Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();

							foreach (var col in table.Columns)
							{
								var colNorm = NormalizeText(col.ColumnName);
								if (metricFields.Any(mf => !string.IsNullOrWhiteSpace(mf) && string.Equals(mf, colNorm, StringComparison.OrdinalIgnoreCase)))
								{
									// 强烈偏好包含 metric field 的表（例如 metric.Field = id）
									exactFieldBoost += 200.0;
								}

								if (filterFields.Any(ff => !string.IsNullOrWhiteSpace(ff) && string.Equals(ff, colNorm, StringComparison.OrdinalIgnoreCase)))
								{
									// 偏好包含 filter 字段的表（例如 create_time）
									exactFieldBoost += 120.0;
								}

								// 如果 metric 要求是 id/count 且该列为主键，则额外提升
								if (metricFields.Any(mf => string.Equals(mf, "id", StringComparison.OrdinalIgnoreCase)) && col.IsPrimaryKey == true)
								{
									exactFieldBoost += 150.0;
								}
							}
						}
					}
					catch { }

					// 如果表同时包含 metric 中的字段 和 filter 中的字段，给予额外成对加权，明显优先
					var pairBoost = 0.0;
					try
					{
						if (intent != null && table.Columns != null)
						{
							var metricFields = intent.Metrics?.Select(m => NormalizeText(m.Field ?? m.Name)).Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
							var filterFields = intent.Filters?.Select(f => NormalizeText(f.Field)).Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
							if (metricFields.Count > 0 && filterFields.Count > 0)
							{
								foreach (var mf in metricFields)
								{
									foreach (var ff in filterFields)
									{
										var hasMetric = table.Columns.Any(c => string.Equals(NormalizeText(c.ColumnName), mf, StringComparison.OrdinalIgnoreCase));
										var hasFilter = table.Columns.Any(c => string.Equals(NormalizeText(c.ColumnName), ff, StringComparison.OrdinalIgnoreCase));
										if (hasMetric && hasFilter)
										{
											pairBoost += 400.0; // 大幅度提升同时包含 metric 与 filter 的表
											goto PairBoostDone;
										}
									}
								}
							}
						}
					}
					catch { }
				PairBoostDone:;

					var score =
						matchedColumns * 10.0
						+
						columnScore * 5.0
						+
						tableScore * 2.0
						+
						boost
						+
						localMatchCount * 20.0
						+
						exactFieldBoost
						+
						pairBoost;



					return new
					{
						Table = table,

						MatchedColumns =
							matchedColumns,

						ColumnScore =
							columnScore,

						TableScore =
							tableScore,

						Score =
							score
					};

				})

			.OrderByDescending(
				x =>
					x.Score)

			.ThenByDescending(
				x =>
					x.MatchedColumns)

			.FirstOrDefault();






		// 在选表完成前记录每个候选的评分明细，便于调试
		try
		{
			foreach (var c in new[] { candidates })
			{
				if (c == null) continue;
				Console.WriteLine($"SelectBestTable Debug: Table={c.Table?.TableName}, MatchedColumns={c.MatchedColumns}, ColumnScore={c.ColumnScore}, TableScore={c.TableScore}, FinalScore={c.Score}");
			}
		}
		catch
		{
			// 忽略日志异常
		}

		// 应用规则：优先选择有列命中的表；如果有表 matchedColumns>0 的候选，则过滤掉 matchedColumns==0 的表
		var filteredCandidates =
			results
			.Where(x => x.Table != null)
			.GroupBy(x => x.Table!.Id)
			.Select(group => new
			{
				Table = group.First().Table!,
				MatchedColumns = group.Where(x => x.Column != null).Select(x => x.Column!.Id).Distinct().Count(),
				ColumnScore = group.Where(x => x.Column != null).Select(x => x.Score).DefaultIfEmpty(0).Max(),
				TableScore = group.Where(x => x.IsTableVector).Select(x => x.Score).DefaultIfEmpty(0).Max(),
				Results = group.ToList()
			})
			.ToList();

		if (filteredCandidates.Any(x => x.MatchedColumns > 0))
		{
			filteredCandidates = filteredCandidates.Where(x => x.MatchedColumns > 0).ToList();
		}

		// 重新计算得分并选择最高
		var scoredCandidates = filteredCandidates.Select(x =>
		{
			var matchedColumns = x.MatchedColumns;
			var columnScore = x.ColumnScore;
			var tableScore = x.TableScore;

			double boost = 0.0;
			try
			{
				var tableNameText = NormalizeText(x.Table.TableName);
				var tableCommentText = NormalizeText(x.Table.TableComment);
				foreach (var term in businessTerms)
				{
					var t = NormalizeText(term);
					if (string.IsNullOrWhiteSpace(t)) continue;
					if ((!string.IsNullOrWhiteSpace(tableNameText) && tableNameText.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
						(!string.IsNullOrWhiteSpace(tableCommentText) && tableCommentText.Contains(t, StringComparison.OrdinalIgnoreCase)))
					{
						boost += 50.0;
						break;
					}
				}
			}
			catch { }

			// 计算本地列匹配数
			var localMatchCount = 0;
			try
			{
				if (x.Table.Columns != null)
				{
					foreach (var term in businessTerms)
					{
						var t = NormalizeText(term);
						if (string.IsNullOrWhiteSpace(t)) continue;
						foreach (var col in x.Table.Columns)
						{
							try
							{
								var colName = NormalizeText(col.ColumnName);
								var colComment = NormalizeText(col.ColumnComment);
								if ((!string.IsNullOrWhiteSpace(colName) && colName.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
									(!string.IsNullOrWhiteSpace(colComment) && colComment.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
									IsRelatedBusinessText(term, col))
								{
									localMatchCount++;
								}
							}
							catch { }
						}
					}
				}
			}
			catch { }

			var finalScore = matchedColumns * 10.0 + columnScore * 5.0 + tableScore * 2.0 + boost + localMatchCount * 20.0;

			return new { Table = x.Table, FinalScore = finalScore, MatchedColumns = matchedColumns, ColumnScore = columnScore, TableScore = tableScore, Boost = boost, LocalMatch = localMatchCount };
		})
		.OrderByDescending(x => x.FinalScore)
		.ThenByDescending(x => x.MatchedColumns)
		.ToList();

		try
		{
			foreach (var c in scoredCandidates)
			{
				Console.WriteLine($"ScoredCandidate: Table={c.Table.TableName}, FinalScore={c.FinalScore}, MatchedColumns={c.MatchedColumns}, ColumnScore={c.ColumnScore}, TableScore={c.TableScore}, Boost={c.Boost}, LocalMatch={c.LocalMatch}");
			}
		}
		catch { }

		return scoredCandidates.FirstOrDefault()?.Table;

	}






	/// <summary>
	/// 解析Metric对应的MetadataColumn。
	///
	/// Metric必须优先使用:
	///
	/// Field
	///
	/// 然后才使用:
	///
	/// Name
	///
	/// 这样可以解决:
	///
	/// 入库凭证条数
	///
	/// 被整体当成字段名称的问题。
	/// </summary>
}
