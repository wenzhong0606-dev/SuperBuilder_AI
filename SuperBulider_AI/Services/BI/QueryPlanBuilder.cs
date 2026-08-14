using System;
using System.Text.RegularExpressions;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.AI;
using SuperBulider_AI.Models.BI;
using SuperBulider_AI.Models.Metadata;


namespace SuperBulider_AI.Services.BI;

/// <summary>
/// Query计划构建服务。
///
/// Phase 1.6.1
///
/// 负责将:
///
/// QueryIntent
///
/// 转换为:
///
/// QueryPlan
///
/// 核心职责:
///
/// 1. 根据用户查询意图提取业务字段
/// 2. 调用 MetadataSemanticSearchService
/// 3. 获取 MetadataSemanticSearchResult
/// 4. 对候选表进行评分
/// 5. 确定当前查询的主表
/// 6. 将 Metric 映射到 MetadataColumn
/// 7. 将 Filter 映射到 MetadataColumn
/// 8. 将 Dimension 映射到 MetadataColumn
/// 9. 将 OrderBy 映射到 MetadataColumn
/// 10. 生成最终 QueryPlan
///
/// 注意:
///
/// 当前阶段不处理多表 Join。
///
/// 因为系统面对的是动态业务数据库，
/// 数据库结构并不保证存在可直接利用的外键关系。
///
/// 因此 Phase 1.6.1 只负责:
///
/// 用户问题
///     ↓
/// Metadata语义检索
///     ↓
/// 候选表
///     ↓
/// 单表选择
///     ↓
/// 字段映射
///     ↓
/// QueryPlan
///
/// 多表动态关系推断将在后续 Phase 单独建设。
/// </summary>
public class QueryPlanBuilder
	: IQueryPlanBuilder
{


	/// <summary>
	/// Metadata语义检索服务。
	///
	/// 用于根据用户业务语言寻找:
	///
	/// MetadataTable
	/// MetadataColumn
	/// MetadataSemantic
	/// </summary>
	private readonly IMetadataSemanticSearchService
		_metadataSearch;

	/// <summary>
	/// 动态JOIN推理服务。
	///
	/// 用于根据当前查询召回的Metadata结果，
	/// 推断本次QueryPlan可能需要的JOIN关系。
	/// </summary>
	private readonly IQueryJoinInferenceService
		_joinInference;

	/// <summary>
	/// 创建 QueryPlanBuilder。
	/// </summary>
	/// <param name="metadataSearch">
	/// Metadata语义检索服务。
	/// </param>
	/// <param name="joinInference">
	/// 动态JOIN推理服务。
	/// </param>
	public QueryPlanBuilder(
		IMetadataSemanticSearchService metadataSearch,
		IQueryJoinInferenceService joinInference)
	{
		_metadataSearch =
			metadataSearch
			?? throw new ArgumentNullException(
				nameof(metadataSearch));

		_joinInference =
			joinInference
			?? throw new ArgumentNullException(
				nameof(joinInference));
	}

	/// <summary>
	/// 判断某个列是否为非信息化展示字段（例如仅表示删除标记或内部用户ID），不适合单列展示。
	/// </summary>
	private bool IsNonInformativeColumn(MetadataColumn col)
	{
		if (col == null) return true;
		var name = (col.ColumnName ?? string.Empty).ToLowerInvariant();
		// 非信息字段包括布尔型删除标记、软删除标志、以及像 create_by/update_by 这样的用户 ID 列
		if (name == "del_flag" || name == "is_deleted" || name == "deleted" || name.EndsWith("_flag") || name.EndsWith("_status"))
			return true;

		if (name.EndsWith("_by") || name == "create_by" || name == "update_by" || name == "created_by")
			return true;

		// 长文本备注/描述字段单独判断为可选展示，但不是首选
		if (name.Contains("note") || name.Contains("remark") || name.Contains("comment"))
			return false;

		return false;
	}

	/// <summary>
	/// 根据表结构返回一个优先展示列的候选列表（按优先级排序）。
	/// 首选单号/编号、时间、主键、物料/商品/数量等业务字段。
	/// </summary>
	private IEnumerable<MetadataColumn> GetPreferredDisplayColumns(MetadataTable table)
	{
		if (table == null || table.Columns == null) yield break;

		// 1. 业务编号类字段：code / no / number
		foreach (var c in table.Columns.Where(c => c.ColumnName != null && (
				string.Equals(c.ColumnName, "code", StringComparison.OrdinalIgnoreCase)
				|| c.ColumnName.EndsWith("_code", StringComparison.OrdinalIgnoreCase)
				|| c.ColumnName.EndsWith("_no", StringComparison.OrdinalIgnoreCase)
				|| c.ColumnName.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0
				|| c.ColumnName.IndexOf("number", StringComparison.OrdinalIgnoreCase) >= 0
			)))
		{
			yield return c;
		}

		// 2. 时间类字段
		foreach (var c in table.Columns.Where(c => !string.IsNullOrWhiteSpace(c.DataType) && (c.DataType.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0 || c.DataType.IndexOf("time", StringComparison.OrdinalIgnoreCase) >= 0)))
		{
			yield return c;
		}

		// 3. 主键或 id
		foreach (var c in table.Columns.Where(c => c.IsPrimaryKey == true || string.Equals(c.ColumnName, "id", StringComparison.OrdinalIgnoreCase)))
		{
			yield return c;
		}

		// 4. 物料/商品/数量类字段
		foreach (var c in table.Columns.Where(c => c.ColumnName != null && (c.ColumnName.IndexOf("material", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("quantity", StringComparison.OrdinalIgnoreCase) >= 0 || c.ColumnName.IndexOf("amount", StringComparison.OrdinalIgnoreCase) >= 0)))
		{
			yield return c;
		}

		// 5. 兜底：其他非 _by 的字段
		foreach (var c in table.Columns.Where(c => c.ColumnName != null && !c.ColumnName.EndsWith("_by", StringComparison.OrdinalIgnoreCase)).Take(10))
		{
			yield return c;
		}
	}

	/// <summary>
	/// 调试用的查询计划诊断信息。
	/// </summary>
	public class QueryPlanDiagnostics
	{
		public QueryIntent Intent { get; set; } = null!;
		public List<string> BusinessTerms { get; set; } = new();
		public Dictionary<string, List<MetadataSemanticSearchResult>> PerTermResults { get; set; } = new();
		public List<CandidateDiagnostic> Candidates { get; set; } = new();
		public QueryPlan? Plan { get; set; }
		public string? PlanError { get; set; }
	}

	public class CandidateDiagnostic
	{
		public long TableId { get; set; }
		public string? TableName { get; set; }
		public int MatchedColumns { get; set; }
		public double ColumnScore { get; set; }
		public double TableScore { get; set; }
		public double Boost { get; set; }
		public int LocalMatch { get; set; }
		public double FinalScore { get; set; }
	}

	/// <summary>
	/// 构建带诊断信息的 QueryPlan，用于调试链路：intent -> businessTerms -> per-term召回 -> 候选评分 -> 最终 Plan
	/// </summary>
	public async Task<QueryPlanDiagnostics> BuildWithDiagnosticsAsync(QueryIntent intent)
	{
		if (intent == null) throw new ArgumentNullException(nameof(intent));

		var diagnostics = new QueryPlanDiagnostics { Intent = intent };

		// 1. business terms
		var businessTerms = CollectBusinessTerms(intent);
		// fallback when no terms extracted
		if (businessTerms.Count == 0)
		{
			businessTerms = CollectBusinessTermsFallback(intent);
		}
		diagnostics.BusinessTerms = businessTerms;

		// 2. per-term raw results
		foreach (var term in businessTerms)
		{
			var items = await _metadataSearch.SearchAsync(term, 10);
			diagnostics.PerTermResults[term] = items.ToList();
		}

		// 3. aggregated metadata results (same as SearchMetadataAsync)
		var metadataResults = await SearchMetadataAsync(businessTerms);

		// 4. build candidate diagnostics
		var groups = metadataResults.Where(x => x.Table != null).GroupBy(x => x.Table!.Id);
		var candidates = new List<CandidateDiagnostic>();
		foreach (var g in groups)
		{
			var table = g.First().Table!;
			var columnResults = g.Where(x => x.Column != null).ToList();
			var tableResults = g.Where(x => x.IsTableVector).ToList();
			var matchedColumns = columnResults.Select(x => x.Column!.Id).Distinct().Count();
			var columnScore = columnResults.Select(x => x.Score).DefaultIfEmpty(0).Max();
			var tableScore = tableResults.Select(x => x.Score).DefaultIfEmpty(0).Max();

			double boost = 0.0;
			try
			{
				var tableNameText = NormalizeText(table.TableName);
				var tableCommentText = NormalizeText(table.TableComment);
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

			int localMatchCount = 0;
			try
			{
				if (table.Columns != null)
				{
					foreach (var term in businessTerms)
					{
						var t = NormalizeText(term);
						if (string.IsNullOrWhiteSpace(t)) continue;
						foreach (var col in table.Columns)
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

			candidates.Add(new CandidateDiagnostic
			{
				TableId = table.Id,
				TableName = table.TableName,
				MatchedColumns = matchedColumns,
				ColumnScore = columnScore,
				TableScore = tableScore,
				Boost = boost,
				LocalMatch = localMatchCount,
				FinalScore = finalScore
			});
		}

		diagnostics.Candidates = candidates.OrderByDescending(c => c.FinalScore).ThenByDescending(c => c.MatchedColumns).ToList();

		// 5. final plan (reuse existing BuildAsync to ensure consistency)
		try
		{
			diagnostics.Plan = await BuildAsync(intent);
		}
		catch (Exception ex)
		{
			diagnostics.PlanError = ex.Message;
		}

		return diagnostics;
	}






	/// <summary>
	/// 创建查询计划。
	///
	/// 执行流程:
	///
	/// QueryIntent
	///      ↓
	/// 提取业务字段
	///      ↓
	/// MetadataSemanticSearch
	///      ↓
	/// 候选表评分
	///      ↓
	/// 确定主表
	///      ↓
	/// 字段映射
	///      ↓
	/// QueryPlan
	/// </summary>
	public async Task<QueryPlan>
		BuildAsync(
			QueryIntent intent)
	{


		if (intent == null)
		{
			throw new ArgumentNullException(
				nameof(intent));
		}



		if (string.IsNullOrWhiteSpace(
			intent.OriginalQuestion))
		{
			throw new InvalidOperationException(
				"QueryIntent缺少OriginalQuestion。");
		}






		/*
		 * ============================================================
		 * Step 1
		 *
		 * 收集用户查询中的所有业务字段。
		 *
		 * 注意:
		 *
		 * Metric不能只使用Name。
		 *
		 * 例如:
		 *
		 * 用户:
		 *
		 * 2025年入库凭证条数
		 *
		 * AI可能返回:
		 *
		 * Name:
		 * 入库凭证条数
		 *
		 * Field:
		 * 入库凭证
		 *
		 * Aggregation:
		 * COUNT
		 *
		 * 所以:
		 *
		 * Name + Field
		 *
		 * 都应该参与Metadata语义搜索。
		 * ============================================================
		 */

		var businessTerms =
			CollectBusinessTerms(
				intent);

		// 如果没有提取到任何业务词，使用原问题做兜底
		if (businessTerms.Count == 0)
		{
			businessTerms = CollectBusinessTermsFallback(intent);
		}






		if (businessTerms.Count == 0)
		{
			throw new InvalidOperationException(
				"QueryIntent中没有可用于Metadata解析的业务字段。");
		}






		/*
		 * ============================================================
		 * Step 2
		 *
		 * Metadata语义搜索。
		 *
		 * 每一个业务字段单独搜索。
		 *
		 * 例如:
		 *
		 * 入库凭证条数
		 * 入库凭证
		 * 年份
		 * 入库凭证
		 *
		 * 分别获取候选Metadata。
		 *
		 * 这样可以避免把整个用户问题:
		 *
		 * 2025年入库凭证条数
		 *
		 * 作为一个字段去匹配。
		 * ============================================================
		 */

		var metadataResults =
			await SearchMetadataAsync(
				businessTerms);






		if (metadataResults.Count == 0)
		{
			throw new InvalidOperationException(
				$"Metadata语义搜索没有找到任何结果。用户问题：{intent.OriginalQuestion}");
		}






		/*
		 * ============================================================
		 * Step 3
		 *
		 * 确定候选数据表。
		 *
		 * 当前 Phase 1.6.1:
		 *
		 * 只允许确定一个主表。
		 *
		 * 不进行Join。
		 *
		 * 表评分综合考虑:
		 *
		 * 1. 字段命中数量
		 * 2. 字段语义匹配Score
		 * 3. 表级向量Score
		 *
		 * 最终选择最符合用户问题的表。
		 * ============================================================
		 */

		var table =
			SelectBestTable(
				metadataResults,
				businessTerms,
				intent);






		if (table == null)
		{
			throw new InvalidOperationException(
				$"无法根据用户查询确定MetadataTable：{intent.OriginalQuestion}");
		}






		/*
		 * ============================================================
		 * Step 4
		 *
		 * 创建QueryPlan。
		 * ============================================================
		 */

		var plan =
			new QueryPlan
			{
				Intent =
					intent
			};






		/*
		 * ============================================================
		 * Step 5
		 *
		 * 添加主表。
		 * ============================================================
		 */

		plan.Tables.Add(
			new QueryTable
			{
				MetadataTableId =
					table.Id,

				DataSourceId =
					table.DataSourceId,

				TableName =
					table.TableName,

				TableComment =
					table.TableComment
			});


		/*
 * ============================================================
 * Step 5.5
 *
 * Phase 1.6.2.1
 *
 * 推断当前主表可能存在的动态JOIN关系。
 *
 * 注意:
 *
 * 当前阶段只构建:
 *
 * QueryPlan.Tables
 * +
 * QueryPlan.Joins
 *
 * 暂时不改变后续Metric / Filter / Dimension
 * 的字段解析逻辑。
 * ============================================================
 */

		var joinCandidates =
			await BuildJoinsAsync(
				table,
				metadataResults);

		foreach (var candidate in joinCandidates)
		{
			/*
			 * ========================================================
			 * 1.
			 * 创建QueryJoin
			 * ========================================================
			 */

			var queryJoin =
				BuildQueryJoin(
					candidate);

			/*
			 * ========================================================
			 * 2.
			 * 防止重复添加JOIN
			 * ========================================================
			 */

			var joinExists =
				plan.Joins.Any(x =>
					(
						x.LeftTableId ==
							queryJoin.LeftTableId
						&&
						x.LeftColumnId ==
							queryJoin.LeftColumnId
						&&
						x.RightTableId ==
							queryJoin.RightTableId
						&&
						x.RightColumnId ==
							queryJoin.RightColumnId
					)
					||
					(
						x.LeftTableId ==
							queryJoin.RightTableId
						&&
						x.LeftColumnId ==
							queryJoin.RightColumnId
						&&
						x.RightTableId ==
							queryJoin.LeftTableId
						&&
						x.RightColumnId ==
							queryJoin.LeftColumnId
					));

			if (joinExists)
			{
				continue;
			}

			/*
			 * ========================================================
			 * 3.
			 * 添加JOIN
			 * ========================================================
			 */

			plan.Joins.Add(
				queryJoin);

			/*
			 * ========================================================
			 * 4.
			 * 添加JOIN目标表
			 * ========================================================
			 */

			var joinedTableId =
				candidate.LeftTableId == table.Id
					? candidate.RightTableId
					: candidate.LeftTableId;

			var joinedTable =
				metadataResults
					.Where(x =>
						x.Table != null &&
						x.Table.Id == joinedTableId)
					.Select(x =>
						x.Table!)
					.FirstOrDefault();

			if (joinedTable == null)
			{
				continue;
			}

			/*
			 * 防止重复添加Table。
			 */

			var tableExists =
				plan.Tables.Any(x =>
					x.MetadataTableId ==
						joinedTable.Id);

			if (tableExists)
			{
				continue;
			}

			plan.Tables.Add(
				new QueryTable
				{
					MetadataTableId =
						joinedTable.Id,

					DataSourceId =
						joinedTable.DataSourceId,

					TableName =
						joinedTable.TableName,

					TableComment =
						joinedTable.TableComment
				});
		}

		/*
 * ============================================================
 * 默认时间排序规则
 * ============================================================
 *
 * 非常重要：
 *
 * Limit != 时间排序。
 *
 * 例如：
 *
 * 数量最多的十条库存
 *
 * 应该由：
 *
 * OrderBy = 库存数量
 * OrderDirection = DESC
 * Limit = 10
 *
 * 决定。
 *
 * 只有用户明确表达：
 *
 * 最近
 * 最新
 * 最近创建
 * 最新创建
 *
 * 时，才允许自动使用时间字段排序。
 * ============================================================
 */

		var isRecentQuery =
			!string.IsNullOrWhiteSpace(
				intent.OriginalQuestion)
			&&
			(
				intent.OriginalQuestion.Contains(
					"最近",
					StringComparison.OrdinalIgnoreCase)
				||
				intent.OriginalQuestion.Contains(
					"最新",
					StringComparison.OrdinalIgnoreCase)
				||
				intent.OriginalQuestion.Contains(
					"最近创建",
					StringComparison.OrdinalIgnoreCase)
				||
				intent.OriginalQuestion.Contains(
					"最新创建",
					StringComparison.OrdinalIgnoreCase)
				||
				intent.OriginalQuestion.Contains(
					"最近新增",
					StringComparison.OrdinalIgnoreCase)
				||
				intent.OriginalQuestion.Contains(
					"最新新增",
					StringComparison.OrdinalIgnoreCase)
			);

		/*
		 * 只有：
		 *
		 * 1. 用户明确要求最近/最新
		 * 2. AI没有已经识别出的OrderBy
		 *
		 * 才允许自动设置时间排序。
		 *
		 * 绝不能覆盖AI已经识别出的业务排序字段。
		 */

		if (isRecentQuery &&
			string.IsNullOrWhiteSpace(intent.OrderBy))
		{
			try
			{
				/*
				 * 选择时间列优先候选。
				 */

				var timeCandidates =
					new[]
					{
				"create_time",
				"come_time",
				"update_time",
				"affirm_time",
				"created_time",
				"createdat",
				"created"
					};

				MetadataColumn? timeCol = null;

				if (table.Columns != null)
				{
					/*
					 * 第一优先级：
					 *
					 * 常见时间字段名称。
					 */

					timeCol =
						table.Columns.FirstOrDefault(
							c =>
								c.ColumnName != null
								&&
								timeCandidates.Any(
									tc =>
										string.Equals(
											tc,
											c.ColumnName,
											StringComparison.OrdinalIgnoreCase)));

					/*
					 * 第二优先级：
					 *
					 * 根据数据类型寻找时间字段。
					 */

					if (timeCol == null)
					{
						timeCol =
							table.Columns.FirstOrDefault(
								c =>
									!string.IsNullOrWhiteSpace(
										c.DataType)
									&&
									(
										c.DataType.IndexOf(
											"date",
											StringComparison.OrdinalIgnoreCase) >= 0
										||
										c.DataType.IndexOf(
											"time",
											StringComparison.OrdinalIgnoreCase) >= 0
									));
					}
				}

				if (timeCol != null)
				{
					intent.OrderBy =
						timeCol.ColumnName;

					intent.OrderDirection =
						"DESC";
				}

				/*
				 * 确保至少有一个字段用于SELECT。
				 *
				 * 这里仅负责没有Metric/Dimension/Filter/OrderBy
				 * 的纯“最近N条”查询。
				 */

				if (plan.Fields.Count == 0)
				{
					MetadataColumn? defaultCol = null;

					if (table.Columns != null)
					{
						/*
						 * 1. 主键
						 */

						defaultCol =
							table.Columns.FirstOrDefault(
								c =>
									c.IsPrimaryKey == true);

						/*
						 * 2. id
						 */

						if (defaultCol == null)
						{
							defaultCol =
								table.Columns.FirstOrDefault(
									c =>
										string.Equals(
											c.ColumnName,
											"id",
											StringComparison.OrdinalIgnoreCase));
						}

						/*
						 * 3. code / no
						 */

						if (defaultCol == null)
						{
							defaultCol =
								table.Columns.FirstOrDefault(
									c =>
										c.ColumnName != null
										&&
										(
											string.Equals(
												c.ColumnName,
												"code",
												StringComparison.OrdinalIgnoreCase)
											||
											c.ColumnName.EndsWith(
												"_code",
												StringComparison.OrdinalIgnoreCase)
											||
											c.ColumnName.EndsWith(
												"_no",
												StringComparison.OrdinalIgnoreCase)
											||
											c.ColumnName.IndexOf(
												"code",
												StringComparison.OrdinalIgnoreCase) >= 0
										));
						}

						/*
						 * 4. 避免 create_by / update_by 等内部字段。
						 */

						if (defaultCol == null)
						{
							defaultCol =
								table.Columns.FirstOrDefault(
									c =>
										c.ColumnName != null
										&&
										!(
											c.ColumnName.EndsWith(
												"_by",
												StringComparison.OrdinalIgnoreCase)
											||
											string.Equals(
												c.ColumnName,
												"create_by",
												StringComparison.OrdinalIgnoreCase)
											||
											string.Equals(
												c.ColumnName,
												"update_by",
												StringComparison.OrdinalIgnoreCase)
										));
						}

						/*
						 * 5. 最终兜底。
						 */

						if (defaultCol == null)
						{
							defaultCol =
								table.Columns.FirstOrDefault();
						}
					}

					if (defaultCol != null)
					{
						AddOrUpdateQueryField(
							plan,
							defaultCol,
							"NONE");
					}
				}
			}
			catch
			{
				/*
				 * 默认排序属于辅助逻辑。
				 *
				 * 如果发生异常，不影响正常QueryPlan生成。
				 */
			}
		}

		// 将 QueryPlan.DataSourceId 设置为选中的主表的数据源，避免后续使用 DataSourceId 时为默认 0 导致错误
		plan.DataSourceId = table.DataSourceId;






		/*
		 * ============================================================
		 * Step 6
		 *
		 * 处理 Metrics。
		 *
		 * 例如:
		 *
		 * 用户:
		 *
		 * 入库凭证条数
		 *
		 * AI:
		 *
		 * Name:
		 * 入库凭证条数
		 *
		 * Field:
		 * 入库凭证
		 *
		 * Aggregation:
		 * COUNT
		 *
		 * 最终:
		 *
		 * QueryField
		 *
		 * ColumnName:
		 * 实际数据库字段名
		 *
		 * Aggregation:
		 * COUNT
		 * ============================================================
		 */

		foreach (var metric in intent.Metrics)
		{

			var field =
				ResolveMetricField(
					metric,
					table,
					metadataResults);




			if (field == null)
			{
				throw new InvalidOperationException(
					$"无法将用户查询中的业务字段映射到MetadataColumn：{GetMetricDescription(metric)}");
			}




			AddOrUpdateQueryField(
				plan,
				field,
				metric.Aggregation);

		}








		/*
		 * ============================================================
		 * Step 7
		 *
		 * 处理 Filters。
		 *
		 * Filter中的Field同样不能直接拿来生成SQL。
		 *
		 * 例如:
		 *
		 * 用户:
		 *
		 * 年份 = 2025
		 *
		 * AI:
		 *
		 * Field:
		 * 年份
		 *
		 * 最终需要找到真实MetadataColumn:
		 *
		 * OrderDate
		 *
		 * ============================================================
		 */

		var resolvedFilters =
			new List<QueryFilter>();



		foreach (var filter in intent.Filters)
		{

			var column =
				ResolveColumn(
					filter.Field,
					table,
					metadataResults);




			if (column == null)
			{
				throw new InvalidOperationException(
					$"无法将过滤条件字段映射到MetadataColumn：{filter.Field}");
			}




			resolvedFilters.Add(
				new QueryFilter
				{
					Field =
						column.ColumnName ?? string.Empty,

					Operator =
						NormalizeOperator(
							filter.Operator),

					Value =
						filter.Value
				});




			AddOrUpdateQueryField(
				plan,
				column,
				"NONE");

		}






		/*
		 * 使用Metadata解析后的真实字段。
		 */

		plan.Filters =
			resolvedFilters;






		/*
		 * ============================================================
		 * Step 8
		 *
		 * 处理 Dimensions。
		 *
		 * QueryPlan当前没有单独的Dimensions集合。
		 *
		 * 当前架构中:
		 *
		 * QueryPlan.Intent.Dimensions
		 *
		 * 仍然由SqlQueryBuilder使用。
		 *
		 * 因此这里将AI业务名称:
		 *
		 * 客户
		 *
		 * 转换成:
		 *
		 * CustomerName
		 *
		 * ============================================================
		 */

		for (
			int i = 0;
			i < intent.Dimensions.Count;
			i++)
		{

			var dimension =
				intent.Dimensions[i];




			var column = await ResolveColumnAsync(dimension, table, metadataResults);




			if (column == null)
			{
				throw new InvalidOperationException(
					$"无法将分组维度映射到MetadataColumn：{dimension}");
			}




			intent.Dimensions[i] =
				column.ColumnName
				?? dimension;




			AddOrUpdateQueryField(
				plan,
				column,
				"NONE");

		}






		/*
		 * ============================================================
		 * Step 9
		 *
		 * 处理 OrderBy。
		 *
		 * 例如:
		 *
		 * OrderBy:
		 * 入库数
		 *
		 * 需要转换为真实字段:
		 *
		 * COUNT(ReceiptId)
		 *
		 * 当前QueryPlan没有单独的OrderBy对象，
		 * 因此这里仍然修改Intent.OrderBy。
		 * ============================================================
		 */

		if (!string.IsNullOrWhiteSpace(
			intent.OrderBy))
		{

			var orderColumn = await ResolveColumnAsync(intent.OrderBy, table, metadataResults);




			if (orderColumn != null)
			{

				intent.OrderBy =
					orderColumn.ColumnName
					?? intent.OrderBy;




				AddOrUpdateQueryField(
					plan,
					orderColumn,
					"NONE");

			}

		}






		/*
		 * ============================================================
		 * Step 10
		 *
		 * 判断是否聚合。
		 *
		 * 只要存在:
		 *
		 * SUM
		 * COUNT
		 * AVG
		 * MAX
		 * MIN
		 *
		 * 就属于Aggregate Query。
		 * ============================================================
		 */

		plan.IsAggregate =
			plan.Fields.Any(
				x =>
					IsAggregation(
						x.Aggregation));
		// noop: update timestamp

		// 如果当前选中的字段都是非业务展示字段（例如仅返回 del_flag），则自动补充更多有意义的展示字段。
		try
		{
			if (plan.Fields.Count > 0 && plan.Tables.Count > 0)
			{
				var mainTable = table; // 选中的主表
				var allFieldsNonInformative = true;
				foreach (var f in plan.Fields)
				{
					var col = mainTable.Columns?.FirstOrDefault(c => c.Id == f.MetadataColumnId);
					if (col == null) continue;
					if (!IsNonInformativeColumn(col))
					{
						allFieldsNonInformative = false;
						break;
					}
				}

				if (allFieldsNonInformative)
				{
					// 清空并补充优先展示字段
					plan.Fields.Clear();
					var preferred = GetPreferredDisplayColumns(mainTable).Take(6).ToList();
					foreach (var pc in preferred)
					{
						AddOrUpdateQueryField(plan, pc, "NONE");
					}
				}
			}
		}
		catch
		{
			// 忽略任何异常，保持原有行为
		}






		/*
		 * ============================================================
		 * Step 11
		 *
		 * 最终校验。
		 *
		 * 当前阶段必须至少有:
		 *
		 * Table
		 *
		 * 并且最好存在Field。
		 * ============================================================
		 */

		if (plan.Tables.Count == 0)
		{
			throw new InvalidOperationException(
				"QueryPlan没有查询表。");
		}




		if (plan.Fields.Count == 0)
		{
			throw new InvalidOperationException(
				$"QueryPlan没有任何可查询字段：{intent.OriginalQuestion}");
		}






		return plan;

	}






	/// <summary>
	/// 收集QueryIntent中的业务字段。
	///
	/// Metric:
	///
	/// Name
	/// Field
	///
	/// Filter:
	///
	/// Field
	///
	/// Dimension:
	///
	/// Dimension
	///
	/// OrderBy:
	///
	/// OrderBy
	/// </summary>
	private List<string>
		CollectBusinessTerms(
			QueryIntent intent)
	{

		var terms =
			new List<string>();



		/*
		 * Metrics
		 */

		foreach (var metric in intent.Metrics)
		{
			// 原始名称与字段
			AddTerm(terms, metric.Name);
			AddTerm(terms, metric.Field);

			// 生成名称的变体（去掉常见度量词/年份等），例如 "入库凭证条数" -> "入库凭证"
			foreach (var variant in GenerateTermVariants(metric.Name))
			{
				AddTerm(terms, variant);
			}

		}




		/*
		 * Filters
		 */

		foreach (var filter in intent.Filters)
		{

			AddTerm(
				terms,
				filter.Field);

		}




		/*
		 * Dimensions
		 */

		foreach (var dimension in intent.Dimensions)
		{

			AddTerm(
				terms,
				dimension);

		}




		/*
		 * OrderBy
		 */

		AddTerm(
			terms,
			intent.OrderBy);




		return terms
			.Distinct(
				StringComparer.OrdinalIgnoreCase)
			.ToList();

	}

	/// <summary>
	/// 如果 CollectBusinessTerms 未能提取到任何业务词，使用 OriginalQuestion 做兜底处理，
	/// 生成若干变体并拆分关键词以提高召回概率。
	/// </summary>
	private List<string> CollectBusinessTermsFallback(QueryIntent intent)
	{
		var terms = new List<string>();

		if (intent == null || string.IsNullOrWhiteSpace(intent.OriginalQuestion))
		{
			return terms;
		}

		// 先尝试基于原始问题生成变体
		foreach (var v in GenerateTermVariants(intent.OriginalQuestion))
		{
			AddTerm(terms, v);
		}

		// 进一步按空白或标点拆分，去掉表示时间、数量的词
		var parts = Regex.Split(intent.OriginalQuestion, "[\\s\\p{P}\\p{S}]+")
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.Select(p => p.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var stopWords = new[] { "最近", "近", "条", "个", "天", "月", "年", "前" };

		foreach (var p in parts)
		{
			if (stopWords.Any(sw => p.Contains(sw)))
				continue;

			// 排除纯数字
			if (Regex.IsMatch(p, "^\\d+$"))
				continue;

			AddTerm(terms, p);
		}

		return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
	}

	/// <summary>
	/// 为业务文本生成若干变体，用于增强语义检索召回率。
	/// 例如去掉度量后缀（条数/数量/个数/总数/金额），去掉年份（2025年）等。
	/// </summary>
	private IEnumerable<string> GenerateTermVariants(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			yield break;

		var original = value.Trim();
		yield return original;

		// 去掉常见的度量后缀
		var cleaned = Regex.Replace(original, "(条数|数量|个数|总数|金额)$", "", RegexOptions.IgnoreCase).Trim();
		if (!string.Equals(cleaned, original, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cleaned))
			yield return cleaned;

		// 去掉年份例如 2025年
		var noYear = Regex.Replace(cleaned, "\\d{4}年", "", RegexOptions.IgnoreCase).Trim();
		if (!string.Equals(noYear, cleaned, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(noYear))
			yield return noYear;

		// 进一步按空白或标点分割并返回每个子片段
		var parts = Regex.Split(noYear, "[\\u0020\\p{P}\\p{S}]+", RegexOptions.None)
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.Distinct(StringComparer.OrdinalIgnoreCase);

		foreach (var p in parts)
		{
			if (!string.Equals(p, original, StringComparison.OrdinalIgnoreCase) && !string.Equals(p, cleaned, StringComparison.OrdinalIgnoreCase) && !string.Equals(p, noYear, StringComparison.OrdinalIgnoreCase))
				yield return p;
		}
	}






	/// <summary>
	/// 添加业务字段。
	/// </summary>
	private void AddTerm(
		List<string> terms,
		string? value)
	{

		if (string.IsNullOrWhiteSpace(
			value))
		{
			return;
		}



		terms.Add(
			value.Trim());

	}






	/// <summary>
	/// 对所有业务字段进行Metadata语义搜索。
	///
	/// 每个业务字段独立搜索。
	/// </summary>
	private async Task<List<MetadataSemanticSearchResult>>
		SearchMetadataAsync(
			List<string> terms)
	{

		var results =
			new List<MetadataSemanticSearchResult>();



		foreach (var term in terms)
		{

			var items =
				await _metadataSearch
				.SearchAsync(
					term,
					10);




			results.AddRange(
				items);

		}




		/*
		 * 同一个Vector可能因为多个业务字段被重复召回。
		 *
		 * 去重:
		 *
		 * VectorId + Column/Table
		 */

		return results
			.GroupBy(
				x =>
					$"{x.VectorType}:{x.VectorId}",
				StringComparer.OrdinalIgnoreCase)
			.Select(
				x =>
					x.OrderByDescending(
						r => r.Score)
					.First())
			.ToList();

	}






	/// <summary>
	/// 从Metadata搜索结果中选择最佳主表。
	///
	/// 评分原则:
	///
	/// 1. 字段命中数量优先
	/// 2. 字段语义Score
	/// 3. 表向量Score
	///
	/// 当前阶段只选择一个主表。
	/// </summary>
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
	private MetadataColumn?
		ResolveMetricField(
			QueryMetric metric,
			MetadataTable table,
			List<MetadataSemanticSearchResult> results)
	{

		// 优先处理明确的字段映射：如果 Metric.Field 明确为 id 或者类似主键标识，优先使用主键或 id 字段
		try
		{
			if (!string.IsNullOrWhiteSpace(metric.Field) && table?.Columns != null)
			{
				var nf = NormalizeText(metric.Field);
				if (string.Equals(nf, "id", StringComparison.OrdinalIgnoreCase) || nf.EndsWith("id", StringComparison.OrdinalIgnoreCase))
				{
					var pk = table.Columns.FirstOrDefault(c => c.IsPrimaryKey == true);
					if (pk != null) return pk;
					var idCol = table.Columns.FirstOrDefault(c => string.Equals(c.ColumnName, "id", StringComparison.OrdinalIgnoreCase));
					if (idCol != null) return idCol;
				}
			}
		}
		catch { }


		/*
		 * 第一优先级:
		 *
		 * Metric.Field
		 */

		if (!string.IsNullOrWhiteSpace(
			metric.Field))
		{

			var column =
				ResolveColumn(
					metric.Field,
					table,
					results);




			if (column != null)
			{
				return column;
			}

		}




		/*
		 * 第二优先级:
		 *
		 * Metric.Name
		 *
		 * 例如:
		 *
		 * 入库凭证条数
		 *
		 * Metadata:
		 *
		 * 入库凭证
		 */

		if (!string.IsNullOrWhiteSpace(
			metric.Name))
		{

			return ResolveColumn(
				metric.Name,
				table,
				results);

		}




		return null;

	}

	private Task<MetadataColumn?>
		ResolveMetricFieldAsync(
			QueryMetric metric,
			MetadataTable table,
			List<MetadataSemanticSearchResult> results)
	{
		// 当前实现与同步版本一致，保留异步签名以便将来扩展
		return Task.FromResult(ResolveMetricField(metric, table, results));
	}







	/// <summary>
	/// 将业务字段解析为MetadataColumn。
	///
	/// 匹配顺序:
	///
	/// 1. 数据库字段名完全匹配
	/// 2. BusinessKey匹配
	/// 3. 业务名称匹配
	/// 4. SearchText / Semantic匹配
	/// 5. Qdrant Score
	///
	/// 只允许匹配当前主表。
	/// </summary>
	private MetadataColumn?
		ResolveColumn(
			string? businessField,
			MetadataTable table,
			List<MetadataSemanticSearchResult> results)
	{


		if (string.IsNullOrWhiteSpace(
			businessField))
		{
			return null;
		}

		// 优先：如果业务字段与表中某列的 ColumnName 或 BusinessKey 精确匹配，直接返回该列（避免语义召回错误）
		try
		{
			var normalizedField = NormalizeText(businessField);
			// 先在语义检索的 results 中查找精确匹配的列（当 table.Columns 可能未被完整加载时）
			var resultExact = results
				.Where(x => x.Table != null && x.Table.Id == table.Id && x.Column != null)
				.Select(x => x.Column!)
				.FirstOrDefault(c =>
					(!string.IsNullOrWhiteSpace(c.ColumnName) && string.Equals(c.ColumnName, businessField, StringComparison.OrdinalIgnoreCase))
					|| (!string.IsNullOrWhiteSpace(NormalizeText(c.ColumnName)) && string.Equals(NormalizeText(c.ColumnName), normalizedField, StringComparison.OrdinalIgnoreCase))
					|| (!string.IsNullOrWhiteSpace(c.BusinessKey) && string.Equals(NormalizeText(c.BusinessKey), normalizedField, StringComparison.OrdinalIgnoreCase)));

			if (resultExact != null)
			{
				return resultExact;
			}

			if (table.Columns != null)
			{
				var exactMatch = table.Columns.FirstOrDefault(c =>
					(!string.IsNullOrWhiteSpace(c.ColumnName) && string.Equals(c.ColumnName, businessField, StringComparison.OrdinalIgnoreCase))
					|| (!string.IsNullOrWhiteSpace(NormalizeText(c.ColumnName)) && string.Equals(NormalizeText(c.ColumnName), normalizedField, StringComparison.OrdinalIgnoreCase))
					|| (!string.IsNullOrWhiteSpace(c.BusinessKey) && string.Equals(NormalizeText(c.BusinessKey), normalizedField, StringComparison.OrdinalIgnoreCase)));

				if (exactMatch != null)
				{
					return exactMatch;
				}
			}
		}
		catch
		{
			// 忽略匹配异常，继续后续逻辑
		}



		var candidates =
			results

			.Where(
				x =>
					x.Column != null
					&&
					x.Table != null
					&&
					x.Table.Id == table.Id)

			.Select(
				x =>
					new ColumnCandidate
					{
						Result = x,

						Column =
							x.Column!,

						Score =
							x.Score
					})

			.ToList();






		/*
		 * 如果当前查询字段没有从向量结果中召回，
		 * 则尝试从主表本身的Columns中进行匹配。
		 *
		 * 这是非常重要的兜底。
		 *
		 * 因为Qdrant召回并不保证一定包含正确字段。
		 */

		if (table.Columns != null)
		{

			foreach (var column in table.Columns)
			{

				if (candidates.Any(
					x =>
						x.Column.Id ==
						column.Id))
				{
					continue;
				}



				candidates.Add(
					new ColumnCandidate
					{
						Result = null,

						Column =
							column,

						Score = 0
					});

			}

		}






		if (candidates.Count == 0)
		{
			return null;
		}






		/*
		 * ============================================================
		 * 计算字段匹配评分。
		 * ============================================================
		 */

		var scored =
			candidates

			.Select(
				x =>
				{
					var lexicalScore =
						CalculateLexicalScore(
							businessField,
							x.Column);



					var semanticScore =
						x.Score;



					/*
					 * 本地字段名称匹配优先级高于
					 * 单纯Qdrant相似度。
					 */

					var finalScore =
						lexicalScore * 100
						+
						semanticScore * 10;



					return new
					{
						x.Column,

						LexicalScore =
							lexicalScore,

						SemanticScore =
							semanticScore,

						FinalScore =
							finalScore
					};

				})

			.OrderByDescending(
				x =>
					x.FinalScore)

			.ToList();






		var best =
			scored.First();






		/*
		 * 如果已经存在明确的字段名称匹配，
		 * 直接使用。
		 */

		if (best.LexicalScore >= 0.8)
		{
			return best.Column;
		}






		/*
		 * 否则使用语义搜索结果。
		 *
		 * 当前阈值:
		 *
		 * 0.30
		 *
		 * 由于最终实际Embedding模型可能不同，
		 * 这个阈值后续可以配置化。
		 */

		if (best.SemanticScore >= 0.30)
		{
			return best.Column;
		}






		/*
		 * 最后处理一个非常重要的情况:
		 *
		 * Metric:
		 *
		 * 入库凭证条数
		 *
		 * Column:
		 *
		 * 入库凭证
		 *
		 * 两者不是完全相等。
		 *
		 * 如果业务字段是另一个字段的扩展描述，
		 * 则允许通过关键词包含关系进行匹配。
		 */

		var relaxed =
			scored

			.Where(
				x =>
					IsRelatedBusinessText(
						businessField,
						x.Column))

			.OrderByDescending(
				x =>
					x.FinalScore)

			.FirstOrDefault();






		return relaxed?.Column;

	}

	private Task<MetadataColumn?>
		ResolveColumnAsync(
			string? businessField,
			MetadataTable table,
			List<MetadataSemanticSearchResult> results)
	{
		// 目前逻辑为同步，包装为 Task 以便在 BuildAsync 中统一 await
		return Task.FromResult(ResolveColumn(businessField, table, results));
	}






	/// <summary>
	/// 计算业务字段与MetadataColumn的本地文本匹配度。
	///
	/// 匹配内容:
	///
	/// ColumnName
	/// BusinessKey
	/// SearchText
	/// Semantic.BusinessMeaning
	/// Semantic.Keywords
	/// Semantic.Synonyms
	/// Semantic.ExampleQuestions
	/// </summary>
	private double
		CalculateLexicalScore(
			string businessField,
			MetadataColumn column)
	{

		var query =
			NormalizeText(
				businessField);




		if (string.IsNullOrWhiteSpace(
			query))
		{
			return 0;
		}



		var scores =
			new List<double>();






		/*
		 * ColumnName
		 */

		AddTextScore(
			scores,
			query,
			column.ColumnName,
			1.0);






		/*
		 * BusinessKey
		 */

		AddTextScore(
			scores,
			query,
			column.BusinessKey,
			0.95);






		/*
		 * SearchText
		 */

		AddTextScore(
			scores,
			query,
			column.SearchText,
			0.90);






		/*
		 * Semantic
		 */

		if (column.Semantic != null)
		{

			AddTextScore(
				scores,
				query,
				column.Semantic.BusinessMeaning,
				0.95);



			AddTextScore(
				scores,
				query,
				column.Semantic.Keywords,
				0.90);



			AddTextScore(
				scores,
				query,
				column.Semantic.Synonyms,
				0.90);



			AddTextScore(
				scores,
				query,
				column.Semantic.ExampleQuestions,
				0.85);



			AddTextScore(
				scores,
				query,
				column.Semantic.SearchText,
				0.85);

		}






		return scores
			.DefaultIfEmpty(0)
			.Max();

	}






	/// <summary>
	/// 计算文本匹配分数。
	/// </summary>
	private void AddTextScore(
		List<double> scores,
		string query,
		string? target,
		double weight)
	{

		if (string.IsNullOrWhiteSpace(
			target))
		{
			return;
		}



		var normalizedTarget =
			NormalizeText(
				target);




		if (string.IsNullOrWhiteSpace(
			normalizedTarget))
		{
			return;
		}




		/*
		 * 完全相等。
		 */

		if (query.Equals(
			normalizedTarget,
			StringComparison.OrdinalIgnoreCase))
		{

			scores.Add(
				1.0 * weight);

			return;

		}




		/*
		 * 查询内容包含Metadata文本。
		 *
		 * 例如:
		 *
		 * 入库凭证条数
		 *
		 * 包含:
		 *
		 * 入库凭证
		 */

		if (query.Contains(
			normalizedTarget,
			StringComparison.OrdinalIgnoreCase))
		{

			scores.Add(
				0.90 * weight);

			return;

		}




		/*
		 * Metadata文本包含查询内容。
		 */

		if (normalizedTarget.Contains(
			query,
			StringComparison.OrdinalIgnoreCase))
		{

			scores.Add(
				0.85 * weight);

			return;

		}




		/*
		 * 计算中文/英文连续片段重叠。
		 */

		var overlap =
			CalculateTextOverlap(
				query,
				normalizedTarget);




		if (overlap > 0)
		{

			scores.Add(
				overlap * weight);

		}

	}






	/// <summary>
	/// 判断业务字段是否与Metadata字段存在明显业务关联。
	///
	/// 主要解决:
	///
	/// 入库凭证条数
	///
	/// 与:
	///
	/// 入库凭证
	///
	/// 这种情况。
	/// </summary>
	private bool
		IsRelatedBusinessText(
			string businessField,
			MetadataColumn column)
	{

		var query =
			NormalizeText(
				businessField);




		if (string.IsNullOrWhiteSpace(
			query))
		{
			return false;
		}



		var texts =
			new List<string?>()
			{
				column.ColumnName,
				column.BusinessKey,
				column.SearchText
			};




		if (column.Semantic != null)
		{

			texts.Add(
				column.Semantic.BusinessMeaning);

			texts.Add(
				column.Semantic.Keywords);

			texts.Add(
				column.Semantic.Synonyms);

			texts.Add(
				column.Semantic.ExampleQuestions);

			texts.Add(
				column.Semantic.SearchText);

		}




		foreach (var text in texts)
		{

			if (string.IsNullOrWhiteSpace(
				text))
			{
				continue;
			}



			var target =
				NormalizeText(
					text);




			if (query.Contains(
				target,
				StringComparison.OrdinalIgnoreCase)
				||
				target.Contains(
					query,
					StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

		}




		return false;

	}






	/// <summary>
	/// 文本重叠计算。
	///
	/// 使用最长公共连续片段近似计算。
	/// </summary>
	private double
		CalculateTextOverlap(
			string source,
			string target)
	{

		if (string.IsNullOrWhiteSpace(source)
			||
			string.IsNullOrWhiteSpace(target))
		{
			return 0;
		}



		var maxLength =
			Math.Min(
				source.Length,
				target.Length);




		var longest =
			0;



		for (
			int length = maxLength;
			length >= 2;
			length--)
		{

			for (
				int i = 0;
				i + length <= source.Length;
				i++)
			{

				var part =
					source.Substring(
						i,
						length);




				if (target.Contains(
					part,
					StringComparison.OrdinalIgnoreCase))
				{

					longest =
						length;

					goto Found;

				}

			}

		}



	Found:

		if (longest == 0)
		{
			return 0;
		}




		return (double)longest
			/
			Math.Max(
				source.Length,
				target.Length);

	}






	/// <summary>
	/// 标准化业务文本。
	///
	/// 去除:
	///
	/// 空格
	/// 下划线
	/// 横线
	/// 常见标点
	///
	/// 方便:
	///
	/// 入库凭证
	///
	/// 入库_凭证
	///
	/// 入库 凭证
	///
	/// 进行匹配。
	/// </summary>
	private string
		NormalizeText(
			string? text)
	{

		if (string.IsNullOrWhiteSpace(
			text))
		{
			return string.Empty;
		}



		var chars =
			text
			.Trim()
			.ToLowerInvariant()
			.Where(
				c =>
					char.IsLetterOrDigit(c)
					||
					c >= '\u4E00'
					&&
					c <= '\u9FFF')
			.ToArray();




		return new string(
			chars);

	}






	/// <summary>
	/// 添加或者更新QueryField。
	///
	/// 同一个MetadataColumn只生成一个QueryField。
	/// </summary>
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
	private string
		NormalizeOperator(
			string? value)
	{

		if (string.IsNullOrWhiteSpace(
			value))
		{
			return "=";
		}



		var op =
			value.Trim()
			.ToUpperInvariant();




		return op switch
		{
			"=" => "=",
			">" => ">",
			"<" => "<",
			">=" => ">=",
			"<=" => "<=",
			"<>" => "<>",
			"!=" => "!=",
			"LIKE" => "LIKE",
			"IN" => "IN",
			"IS NULL" => "IS NULL",
			"IS NOT NULL" => "IS NOT NULL",

			_ =>
				throw new InvalidOperationException(
					$"不支持的查询操作符：{value}")
		};

	}






	/// <summary>
	/// 判断聚合方式。
	/// </summary>
	private bool
		IsAggregation(
			string? aggregation)
	{

		if (string.IsNullOrWhiteSpace(
			aggregation))
		{
			return false;
		}



		return aggregation.ToUpperInvariant()
			switch
		{
			"SUM" => true,
			"COUNT" => true,
			"AVG" => true,
			"MAX" => true,
			"MIN" => true,

			_ => false
		};

	}






	/// <summary>
	/// 获取Metric错误描述。
	/// </summary>
	private string
		GetMetricDescription(
			QueryMetric metric)
	{

		if (!string.IsNullOrWhiteSpace(
			metric.Field))
		{
			return metric.Field;
		}



		if (!string.IsNullOrWhiteSpace(
			metric.Name))
		{
			return metric.Name;
		}



		return "未知指标";

	}






	/// <summary>
	/// Metadata字段候选对象。
	/// </summary>
	private sealed class ColumnCandidate
	{

		/// <summary>
		/// Metadata语义检索结果。
		/// </summary>
		public MetadataSemanticSearchResult?
			Result
		{
			get;
			set;
		}



		/// <summary>
		/// Metadata字段。
		/// </summary>
		public MetadataColumn
			Column
		{
			get;
			set;
		}
			= null!;



		/// <summary>
		/// Qdrant相似度。
		/// </summary>
		public double Score
		{
			get;
			set;
		}

	}


	/// <summary>
	/// 根据当前查询召回的Metadata结果推断QueryPlan中的JOIN关系。
	///
	/// Phase 1.6.2.1
	///
	/// 当前阶段采用保守策略：
	///
	/// 1. 只考虑当前主表直接连接的候选表。
	/// 2. 使用现有IQueryJoinInferenceService进行关系推断。
	/// 3. 只接受InferenceService已经通过阈值的Candidate。
	/// 4. 同一目标表只保留Confidence最高的一条JOIN。
	/// 5. 当前最多增加2个直接关联表。
	/// 6. 当前默认使用INNER JOIN。
	///
	/// 注意：
	///
	/// 本方法只负责:
	///
	/// QueryJoinCandidate
	///        ↓
	/// QueryJoin
	///
	/// 不负责SQL生成。
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