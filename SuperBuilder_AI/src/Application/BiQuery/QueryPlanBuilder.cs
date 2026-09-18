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
	/// <summary>
	/// 数据源候选收敛服务（M5-04）。
	///
	/// 选表前的数据源权限 / 请求约束逻辑已从本类抽出到
	/// <see cref="IQueryPlanDataSourceScope"/>，Builder 仅委托它执行，
	/// 自身不再承载权限 / 安全判断。
	/// </summary>
	private readonly IQueryPlanDataSourceScope
		_dataSourceScope;

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
	private readonly QueryPlanValidator _validator;

	// A4 重构：协作子服务（职责拆分，逻辑保持不变）
	private readonly BusinessTermExtractor _businessTermExtractor;
	private readonly FieldResolver _fieldResolver;
	private readonly TableSelector _tableSelector;
	private readonly JoinBuilder _joinBuilder;

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
		IQueryJoinInferenceService joinInference,
		QueryPlanValidator validator,
		IQueryPlanDataSourceScope dataSourceScope)
	{
		_metadataSearch =
			metadataSearch
			?? throw new ArgumentNullException(
				nameof(metadataSearch));

		_joinInference =
			joinInference
			?? throw new ArgumentNullException(
				nameof(joinInference));

		_validator =
			validator
			?? throw new ArgumentNullException(
				nameof(validator));

		_dataSourceScope =
			dataSourceScope
			?? throw new ArgumentNullException(
				nameof(dataSourceScope));

		_businessTermExtractor =
			new BusinessTermExtractor(
				_metadataSearch);

		_fieldResolver =
			new FieldResolver();

		_tableSelector =
			new TableSelector(
				_fieldResolver);

		_joinBuilder =
			new JoinBuilder(
				_joinInference);
	}

	/// <summary>
	/// 判断某个列是否为非信息化展示字段（例如仅表示删除标记或内部用户ID），不适合单列展示。
	/// </summary>
	public async Task<QueryPlan>
		BuildAsync(
			QueryIntent intent,
			long? requestedDataSourceId = null,
			IReadOnlyCollection<long>? authorizedDataSourceIds = null)
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

		// M5-04：选表前的数据源候选收敛（授权集合约束 + 显式请求数据源约束）
		// 已抽离到 IQueryPlanDataSourceScope，Builder 仅委托执行，自身不再承载
		// 权限 / 安全判断。逻辑与重构前逐字节一致（见 QueryPlanDataSourceScope）。
		metadataResults = await _dataSourceScope.ScopeAsync(
			metadataResults,
			requestedDataSourceId,
			authorizedDataSourceIds,
			intent.OriginalQuestion);

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
				intent,
				authorizedDataSourceIds);






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

		// P3 业务实体语义上下文：把归一化阶段识别出的候选业务实体透传到计划，
		// 供编排层/可解释性参考（不覆盖 Metadata 解析结果）。
		if (intent.BusinessEntityHints is { Count: > 0 })
			plan.BusinessEntityContext = intent.BusinessEntityHints;






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
					table.TableComment,

				CatalogName =
					table.CatalogName,

				SchemaName =
					table.SchemaName
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
						joinedTable.TableComment,

					CatalogName =
						joinedTable.CatalogName,

					SchemaName =
						joinedTable.SchemaName
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
					/*
					 * 对未指定具体字段的“最近N条”类纯明细查询，
					 * 不再只补单个默认列（之前只返回 come_time 等时间字段，
					 * 导致用户无法看到业务信息）。改为按首选展示列补 6 个字段。
					 *
					 * 注意：必须确认当前意图没有指标/维度/过滤条件，
					 * 否则聚合/筛选查询应由后续 Step 6-9 处理，不能在此兜底。
					 */
					var isPureDetail =
						intent.Metrics.Count == 0
						&& intent.Dimensions.Count == 0
						&& intent.Filters.Count == 0;

					if (isPureDetail && table.Columns != null)
					{
						var preferred =
							GetPreferredDisplayColumns(table)
								.Take(6)
								.ToList();

						foreach (var pc in preferred)
						{
							AddOrUpdateQueryField(
								plan,
								pc,
								"NONE");
						}

						// 兜底：首选列不足目标数量时（metadata 标记稀疏），
						// 按业务相关性补足该表的非内部字段，
						// 确保“最近的十个入库单”等首轮问题能直接返回多列业务字段。
						if (plan.Fields.Count < FallbackTargetColumnCount
							&& table.Columns != null)
						{
							var need =
								FallbackTargetColumnCount - plan.Fields.Count;

							var more =
								GetFallbackDisplayColumns(
									table,
									plan.Fields.Select(f => f.ColumnName).Where(n => !string.IsNullOrWhiteSpace(n))!)
									.Take(need)
									.ToList();

							foreach (var fc in more)
							{
								AddOrUpdateQueryField(
									plan,
									fc,
									"NONE");
							}
						}

						// 极端兜底：如果首选列全空，至少保证有一个字段。
						if (plan.Fields.Count == 0)
						{
							var columns = table.Columns;
							var lastResort =
								columns?.FirstOrDefault();

							if (lastResort != null)
							{
								AddOrUpdateQueryField(
									plan,
									lastResort,
									"NONE");
							}
						}
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

		/*
		 * ============================================================
		 * Step 5.6（M0-09 明细列表兜底补列）
		 *
		 * 关键修复：
		 *
		 * 上面的默认时间排序只在“用户说了最近/最新 且 AI 未返回 OrderBy”
		 * 时设置。但实战中 AI（Qwen）对“最近的十个入库单”通常会直接返回
		 * OrderBy=come_time，导致 Step5.5 的整个外层分支被跳过，
		 * 补列逻辑从不执行，最终 SELECT 只剩 come_time 一列。
		 *
		 * 这里把“纯明细补列”从“OrderBy 是否为空”的错误前置条件中解耦：
		 * 只要当前意图属于明细列表（非聚合 + 有 Limit/OrderBy），
		 * 且 SELECT 字段不足目标数量，就按首选展示列 + 业务相关性兜底
		 * 补足到 FallbackTargetColumnCount 列。
		 *
		 * 注意（本次修复）：判定不再要求 Dimensions/Filters 为空。
		 * 实战中 LLM（Qwen）对「最近的十个入库单」常顺手返回一个 Dimension
		 * （如 status），旧判定使其被当作分组查询，补列被整体跳过，
		 * SELECT 最终只剩「时间字段 + status」这类几乎无业务价值的列。
		 * 聚合查询由 IsDetailListQuery 内部的聚合检测拦截，不受影响。
		 *
		 * 该逻辑对“首轮提问”与“refine 显示更多字段”均生效，
		 * 且不会影响聚合 / 带指标的查询。
		 * ============================================================
		 */

		var isPureDetailList = IsDetailListQuery(intent, plan);

		if (isPureDetailList
			&& table.Columns != null
			&& plan.Fields.Count < FallbackTargetColumnCount)
		{
			try
			{
				var preferred =
					GetPreferredDisplayColumns(table)
						.Take(FallbackTargetColumnCount)
						.ToList();

				foreach (var pc in preferred)
				{
					AddOrUpdateQueryField(
						plan,
						pc,
						"NONE");
				}

				// 兜底：首选列不足目标数量时（metadata 标记稀疏），
				// 按业务相关性补足该表的非内部字段。
				if (plan.Fields.Count < FallbackTargetColumnCount
					&& table.Columns != null)
				{
					var need =
						FallbackTargetColumnCount - plan.Fields.Count;

					var more =
						GetFallbackDisplayColumns(
							table,
							plan.Fields.Select(f => f.ColumnName).Where(n => !string.IsNullOrWhiteSpace(n))!)
							.Take(need)
							.ToList();

					foreach (var fc in more)
					{
						AddOrUpdateQueryField(
							plan,
							fc,
							"NONE");
					}
				}

				// 极端兜底：如果首选列全空，至少保证有一个字段。
				if (plan.Fields.Count == 0)
				{
					var columns = table.Columns;
					var lastResort =
						columns?.FirstOrDefault();

					if (lastResort != null)
					{
						AddOrUpdateQueryField(
							plan,
							lastResort,
							"NONE");
					}
				}
			}
			catch
			{
				// 辅助逻辑异常不影响主流程
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

			// ------------------------------------------------------------
			// V2：同步 QueryMetric
			// ------------------------------------------------------------
			var existingMetric =
				plan.Metrics.FirstOrDefault(x =>
					string.Equals(
						x.Field,
						field.ColumnName,
						StringComparison.OrdinalIgnoreCase));

			if (existingMetric == null)
			{
				plan.Metrics.Add(
					new QueryMetric
					{
						Name = metric.Name ?? string.Empty,
						Field = field.ColumnName ?? string.Empty,
						Aggregation = string.IsNullOrWhiteSpace(metric.Aggregation)
							? "NONE"
							: metric.Aggregation.Trim().ToUpperInvariant(),
						Alias = metric.Name,
						IsOrderingMetric =
							!string.IsNullOrWhiteSpace(intent.OrderBy)
							&&
							(
								string.Equals(
									intent.OrderBy,
									metric.Name,
									StringComparison.OrdinalIgnoreCase)
								||
								string.Equals(
									intent.OrderBy,
									metric.Field,
									StringComparison.OrdinalIgnoreCase)
							)
					});
			}
			else
			{
				existingMetric.Name =
					string.IsNullOrWhiteSpace(existingMetric.Name)
						? metric.Name ?? string.Empty
						: existingMetric.Name;

				existingMetric.Aggregation =
					string.IsNullOrWhiteSpace(metric.Aggregation)
						? existingMetric.Aggregation
						: metric.Aggregation.Trim().ToUpperInvariant();
			}

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
					SemanticText =
						filter.SemanticText,

					MetadataTableId =
						column.MetadataTableId,

					TableName =
						table.TableName,

					MetadataColumnId =
						column.Id,

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


			// ------------------------------------------------------------
			// V2：同步 QueryDimension
			// ------------------------------------------------------------
			var existingDimension =
				plan.Dimensions.FirstOrDefault(x =>
					x.MetadataColumnId == column.Id
					||
					string.Equals(
						x.ColumnName,
						column.ColumnName,
						StringComparison.OrdinalIgnoreCase));

			if (existingDimension == null)
			{
				plan.Dimensions.Add(
					new QueryDimension
					{
						MetadataColumnId = column.Id,
						MetadataTableId = column.MetadataTableId,
						TableName = table.TableName,
						ColumnName = column.ColumnName ?? string.Empty,
						Alias = dimension,
						SemanticType = "Dimension"
					});
			}
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

				// ------------------------------------------------------------
				// V2：同步 QueryOrder
				// ------------------------------------------------------------
				var orderDirection =
					string.Equals(
						intent.OrderDirection,
						"DESC",
						StringComparison.OrdinalIgnoreCase)
						? "DESC"
						: "ASC";

				var orderingMetric =
					plan.Metrics.FirstOrDefault(x =>
						string.Equals(
							x.Field,
							orderColumn.ColumnName,
							StringComparison.OrdinalIgnoreCase));

				var existingOrder =
					plan.Orders.FirstOrDefault(x =>
						string.Equals(
							x.Field,
							orderColumn.ColumnName,
							StringComparison.OrdinalIgnoreCase));

				if (existingOrder == null)
				{
					plan.Orders.Add(
						new QueryOrder
						{
							MetadataColumnId = orderColumn.Id,
							MetadataTableId = orderColumn.MetadataTableId,
							TableName = table.TableName,
							Field = orderColumn.ColumnName ?? string.Empty,
							Direction = orderDirection,
							IsMetric = orderingMetric != null,
							// 评估框架比对 MetricSemanticText，应使用 SemanticText 而非 Name
							MetricName = orderingMetric?.SemanticText ?? orderingMetric?.Name,
							// 聚合排序的 Aggregation 必须与 Metric 一致（R2 修复）
							Aggregation =
								orderingMetric?.GetAggregation() ??
								QueryAggregation.None
						});
				}
				else
				{
					existingOrder.Direction = orderDirection;
					existingOrder.MetadataColumnId = orderColumn.Id;
					existingOrder.MetadataTableId = orderColumn.MetadataTableId;
					existingOrder.TableName = table.TableName;

					if (orderingMetric != null)
					{
						existingOrder.IsMetric = true;
						existingOrder.MetricName = orderingMetric.SemanticText ?? orderingMetric.Name;
						existingOrder.Aggregation = orderingMetric.GetAggregation();
					}
				}

			}

		}



		// ------------------------------------------------------------
		// V2：同步 Limit
		// ------------------------------------------------------------
		if (intent.Limit.HasValue)
		{
			plan.Limit = intent.Limit.Value;
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




		// 明细列表判定同样不再要求 plan.Dimensions 为空：
		// LLM 顺手返回的 Dimension（如 status）不应阻止明细补列。
		// 聚合查询由 IsDetailListQuery 拦截，plan.Metrics.Count == 0 额外保留原语义。
		var isDetailList =
			plan.Tables.Any(
				t => t.MetadataTableId > 0)
			&& IsDetailListQuery(intent, plan);

		// 字段扩展请求：用户明确说“显示更多字段/列”等。
		// 实战中 LLM 常把这类 refine 指令误解析为带 dimension/metric 的查询，
		// 导致 isDetailList 为 false。这里把字段扩展从 isDetailList 解耦：
		// 只要非聚合、无指标、字段不足，就强制补足到 FallbackTargetColumnCount。
		var fieldExpansionRequested =
			IsFieldExpansionRequested(intent.OriginalQuestion)
			&& !plan.IsAggregate
			&& !plan.Metrics.Any(m => IsAggregation(m.Aggregation))
			&& plan.Fields.Count > 0
			&& plan.Fields.Count < FallbackTargetColumnCount;

		if ((isDetailList || fieldExpansionRequested)
			&& table is { Columns: not null } expansionTable
			&& plan.Fields.Count < FallbackTargetColumnCount)
		{
			/*
			 * 合法明细列表（目标表已解析 + 含 Limit/Order + 非聚合 + 无指标/维度）
			 * 允许无显式字段：按首选展示列补全后进入 SQL Builder。
			 * 这类请求不需要指标/维度，误报 SB_BI_002 会阻断
			 * 「列出最近十张入库单」等明细场景。
			 *
			 * 另外，当用户明确说“显示更多字段”等 refine 指令时，
			 * 即使 LLM 把该指令误解析为带 dimension/metric（导致 isDetailList=false），
			 * 也应在当前已选字段基础上补足到 FallbackTargetColumnCount，
			 * 避免 refine 后反而只剩一个时间字段。
			 *
			 * 两层兜底：
			 *  1) GetPreferredDisplayColumns —— 业务语义推荐的列
			 *  2) 兜底：表的前 N 个非 _by/_flag/del_flag 列
			 * 第二层确保即便 metadata 列标记（如 IsPreferredDisplay）稀疏，
			 * refine「显示更多字段」仍能稳定输出该表的多个核心列。
			 */
			var preferred =
				GetPreferredDisplayColumns(expansionTable)
					.Take(FallbackTargetColumnCount)
					.ToList();

			foreach (var pc in preferred)
			{
				AddOrUpdateQueryField(
					plan,
					pc,
					"NONE");
			}

			// 兜底：首选列不足目标数量时（metadata 标记稀疏），
			// 按业务相关性补足该表的非内部字段。
			if (plan.Fields.Count < FallbackTargetColumnCount
				&& table.Columns != null)
			{
				var need =
					FallbackTargetColumnCount - plan.Fields.Count;

					var more =
						GetFallbackDisplayColumns(
						expansionTable,
						plan.Fields.Select(f => f.ColumnName).Where(n => !string.IsNullOrWhiteSpace(n))!)
						.Take(need)
						.ToList();

					foreach (var fc in more)
					{
						AddOrUpdateQueryField(
							plan,
							fc,
							"NONE");
					}
			}
		}

		// 对具备标准软删除字段的明细列表默认排除已删除记录。
		// 规则依据当前已解析 Metadata，而不是硬编码业务表；过滤列不进入 SELECT。
		if (isDetailList && table?.Columns != null)
		{
			ApplyConventionalSoftDeleteFilter(plan, table);
		}

		// 明细列表已按首选列补全；若仍无字段（非明细查询且无字段），按原规则报错。
		if (plan.Fields.Count == 0)
		{
			throw new InvalidOperationException(
				$"QueryPlan没有任何可查询字段：{intent.OriginalQuestion}");
		}



		// ------------------------------------------------------------
		// Step 10b：最终确定聚合 / 去重语义
		//
		// 必须在全部 Metric / Field 的 Aggregation 完成语义解析之后计算，
		// 否则早期 plan.Metrics 中的 Aggregation 可能尚未被解析为
		// SUM / COUNT 等，导致 IsAggregate 误判为 false
		// （例如“按 SUM 数量 TopN”的聚合排名场景）。
		// ------------------------------------------------------------
		plan.IsAggregate =
			plan.Metrics.Any(
				x =>
					IsAggregation(
						x.Aggregation))
			|| plan.Fields.Any(
				x =>
					IsAggregation(
						x.Aggregation));

		// 推断 Distinct：
		// “不同 X 数量” / “distinct X” 表示去重计数（COUNT DISTINCT）。
		// 当前 QueryIntent 未携带 Distinct 标记，
		// 这里基于原始问题中明确的去重语义做保守推断。
		if (!plan.Distinct
			&& !string.IsNullOrWhiteSpace(intent.OriginalQuestion)
			&& (intent.OriginalQuestion.Contains("不同")
				|| intent.OriginalQuestion.Contains("distinct", StringComparison.OrdinalIgnoreCase)))
		{
			plan.Distinct = true;
		}

		// [TRACE-D9] 验证 Step 10b 是否真正执行及其输入/输出
		try
		{
			var aggList = string.Join(",", plan.Metrics.Select(m => m.Aggregation));
			var fieldAggList = string.Join(",", plan.Fields.Select(f => f.Aggregation));
			System.IO.File.AppendAllText(
				"C:/tmp/step10b.log",
				$"[{DateTime.Now:HH:mm:ss.fff}] Step10b question='{intent.OriginalQuestion}' metrics=[{aggList}] fieldsAgg=[{fieldAggList}] metricsAny={plan.Metrics.Any(x => IsAggregation(x.Aggregation))} fieldsAny={plan.Fields.Any(x => IsAggregation(x.Aggregation))} => IsAggregate={plan.IsAggregate} Distinct={plan.Distinct}\n");
		}
		catch { }

		var validationResult =
			await _validator.ValidateAsync(plan);

			if (!validationResult.IsValid)
			{
				throw new InvalidOperationException(
					validationResult.ToErrorMessage());
			}

		return plan;

	}






	/// <summary>
	/// 检测用户问题中是否包含“显示更多字段/列”类明确扩展字段的意图。
	/// 用于 refine 轮次在 LLM 仍只返回少量字段时强制补全首选展示列。
	/// </summary>
	private bool IsFieldExpansionRequested(string? question)
	{
		if (string.IsNullOrWhiteSpace(question))
			return false;

		var q = question;
		return
			q.Contains("显示更多字段", StringComparison.OrdinalIgnoreCase)
			|| q.Contains("显示所有字段", StringComparison.OrdinalIgnoreCase)
			|| q.Contains("显示更多列", StringComparison.OrdinalIgnoreCase)
			|| q.Contains("更多列", StringComparison.OrdinalIgnoreCase)
			|| q.Contains("展开列", StringComparison.OrdinalIgnoreCase)
			|| q.Contains("显示全部字段", StringComparison.OrdinalIgnoreCase)
			|| q.Contains("全部字段", StringComparison.OrdinalIgnoreCase)
			|| q.Contains("详细字段", StringComparison.OrdinalIgnoreCase)
			|| q.Contains("更多字段", StringComparison.OrdinalIgnoreCase)
			|| q.Contains("多显示", StringComparison.OrdinalIgnoreCase)
			|| q.Contains("显示详细信息", StringComparison.OrdinalIgnoreCase);
	}

	private static void ApplyConventionalSoftDeleteFilter(QueryPlan plan, MetadataTable table)
	{
		if (plan.Filters.Any(f =>
			string.Equals(f.Field, "del_flag", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(f.Field, "is_deleted", StringComparison.OrdinalIgnoreCase)))
		{
			return;
		}

		var column = table.Columns?.FirstOrDefault(c =>
			string.Equals(c.ColumnName, "del_flag", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(c.ColumnName, "is_deleted", StringComparison.OrdinalIgnoreCase));
		if (column is null || string.IsNullOrWhiteSpace(column.ColumnName)) return;

		plan.Filters.Add(new QueryFilter
		{
			SemanticText = "未删除",
			// 必须绑定列归属：多表（JOIN）场景下若只给 Field，SqlQueryBuilder 无法解析
			// 表名，会退化为裸列名 `del_flag`；而事实表与明细表普遍都有 del_flag，
			// MySQL 会抛 "Column 'del_flag' in where clause is ambiguous"。
			// 口径与 DetailQueryProjectionPolicy.ApplySoftDelete 保持一致。
			MetadataTableId = table.Id,
			TableName = table.TableName,
			MetadataColumnId = column.Id,
			Field = column.ColumnName,
			DataType = column.DataType,
			Operator = "=",
			Value = IsBooleanDataType(column.DataType) ? "false" : "0"
		});
	}

	private static bool IsBooleanDataType(string? dataType)
		=> !string.IsNullOrWhiteSpace(dataType)
			&& (dataType.Contains("bool", StringComparison.OrdinalIgnoreCase)
				|| dataType.Equals("bit", StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// 明细列表兜底补列的目标列数。
	/// 当首选展示列不足该数量（metadata 标记稀疏）或用户明确要求“显示更多字段”时，
	/// 按业务相关性补足到该数量，确保“最近的十个入库单”等场景能直接看到多列业务信息。
	/// </summary>
	private const int FallbackTargetColumnCount = 8;

	/// <summary>
	/// 当 <see cref="GetPreferredDisplayColumns"/> 给出的首选展示列不足时使用的兜底列集合。
	/// 返回该表中“非内部字段”（排除 _by / _flag / del_flag / create_by / update_by / is_deleted
	/// 以及 blob / text / clob 大字段），并排除 alreadySelected 中已选列，避免重复。
	/// 结果按业务相关性排序：编号/代码 &gt; 名称 &gt; 数量/金额 &gt; 时间 &gt; id/主键 &gt; 其它，
	/// 以保证最关键的列（如数量、编码、名称）优先出现在明细列表中。
	/// </summary>
	private static IEnumerable<MetadataColumn> GetFallbackDisplayColumns(
		MetadataTable table,
		IEnumerable<string>? alreadySelected)
	{
		if (table.Columns == null)
			yield break;

		var selected =
			new HashSet<string>(
				alreadySelected ?? Enumerable.Empty<string>(),
				StringComparer.OrdinalIgnoreCase);

		var candidates =
			table.Columns
				.Where(c =>
					c != null
					&& !string.IsNullOrWhiteSpace(c.ColumnName)
					&& !c.ColumnName.EndsWith("_by", StringComparison.OrdinalIgnoreCase)
					&& !c.ColumnName.EndsWith("_flag", StringComparison.OrdinalIgnoreCase)
					&& !string.Equals(c.ColumnName, "del_flag", StringComparison.OrdinalIgnoreCase)
					&& !string.Equals(c.ColumnName, "create_by", StringComparison.OrdinalIgnoreCase)
					&& !string.Equals(c.ColumnName, "update_by", StringComparison.OrdinalIgnoreCase)
					&& !string.Equals(c.ColumnName, "is_deleted", StringComparison.OrdinalIgnoreCase)
					&& (c.DataType == null
						|| !(c.DataType.IndexOf("blob", StringComparison.OrdinalIgnoreCase) >= 0
							|| c.DataType.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0
							|| c.DataType.IndexOf("clob", StringComparison.OrdinalIgnoreCase) >= 0))
					&& !selected.Contains(c.ColumnName))
				.ToList();

		static int Rank(MetadataColumn c)
		{
			var n = c.ColumnName!;

			// 数量/金额是明细记录最核心的业务值，始终最高优先级。
			if (n.IndexOf("quantity", StringComparison.OrdinalIgnoreCase) >= 0
				|| n.IndexOf("qty", StringComparison.OrdinalIgnoreCase) >= 0
				|| n.IndexOf("amount", StringComparison.OrdinalIgnoreCase) >= 0
				|| n.IndexOf("price", StringComparison.OrdinalIgnoreCase) >= 0
				|| n.IndexOf("num", StringComparison.OrdinalIgnoreCase) >= 0
				|| n.IndexOf("count", StringComparison.OrdinalIgnoreCase) >= 0)
				return -1;

			if (n.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0
				|| n.IndexOf("_no", StringComparison.OrdinalIgnoreCase) >= 0
				|| n.EndsWith("no", StringComparison.OrdinalIgnoreCase)
				|| n.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0)
				return 0;

			if (n.IndexOf("time", StringComparison.OrdinalIgnoreCase) >= 0
				|| n.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0)
				return 1;

			if (c.IsPrimaryKey == true
				|| n.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
				return 2;

			return 3;
		}

		foreach (var c in candidates.OrderBy(Rank))
			yield return c;
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
}
