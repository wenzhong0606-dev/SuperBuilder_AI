using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Application.Metadata;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 查询计划验证服务。
///
/// Phase 2.1
/// 查询计划基础验证。
///
/// 核心职责：
///
/// QueryPlan
///     ↓
/// Metadata
///     ↓
/// 基础结构验证
///     ↓
/// QueryPlanValidationResult
///
/// 当前阶段只负责验证，
/// 不负责自动修正 QueryPlan。
/// </summary>
public class QueryPlanValidator
{
	private readonly SuperBIContext _context;

	/// <summary>
	/// 创建查询计划验证服务。
	/// </summary>
	public QueryPlanValidator(
		SuperBIContext context)
	{
		_context =
			context
			?? throw new ArgumentNullException(
				nameof(context));
	}

	/// <summary>
	/// 验证查询计划。
	/// </summary>
	/// <param name="plan">
	/// 待验证的查询计划。
	/// </param>
	/// <returns>
	/// 查询计划验证结果。
	/// </returns>
	public async Task<QueryPlanValidationResult>
		ValidateAsync(
			QueryPlan? plan)
	{
		var result =
			new QueryPlanValidationResult();

		if (plan == null)
		{
			result.AddError(
				"PLAN_NULL",
				"QueryPlan不能为空。",
				nameof(QueryPlan));

			return result;
		}

		ValidateBasicStructure(
			plan,
			result);

		if (result.ErrorCount > 0)
		{
			return result;
		}

		var tableIds =
			plan.Tables
				.Select(x => x.MetadataTableId)
				.Where(x => x > 0)
				.Distinct()
				.ToList();

		var columnIds =
			CollectColumnIds(plan);

		var metadataTables =
			await LoadMetadataTablesAsync(
				tableIds);

		var metadataColumns =
			await LoadMetadataColumnsAsync(
				columnIds);

		ValidateTables(
			plan,
			metadataTables,
			result);

		ValidateFields(
			plan,
			metadataTables,
			metadataColumns,
			result);

		ValidateMetrics(
			plan,
			metadataColumns,
			result);

		ValidateDimensions(
			plan,
			metadataColumns,
			result);

		ValidateFilters(
			plan,
			metadataColumns,
			result);

		ValidateOrders(
			plan,
			metadataColumns,
			result);

		ValidateJoins(
			plan,
			metadataTables,
			metadataColumns,
			result);

		ValidateAggregation(
			plan,
			result);

		ValidateLimitAndRanking(
			plan,
			result);

		return result;
	}

	/// <summary>
	/// 验证 QueryPlan 基础结构。
	/// </summary>
	private static void ValidateBasicStructure(
		QueryPlan plan,
		QueryPlanValidationResult result)
	{
		if (plan.DataSourceId <= 0)
		{
			result.AddError(
				"DATASOURCE_INVALID",
				"QueryPlan未设置有效的DataSourceId。",
				nameof(QueryPlan.DataSourceId));
		}

		if (plan.Tables.Count == 0)
		{
			result.AddError(
				"TABLES_EMPTY",
				"QueryPlan没有任何查询表。",
				nameof(QueryPlan.Tables));
		}

		if (plan.Fields.Count == 0 &&
			plan.Metrics.Count == 0 &&
			plan.Dimensions.Count == 0)
		{
			result.AddError(
				"FIELDS_EMPTY",
				"QueryPlan没有任何查询字段、指标或维度。",
				nameof(QueryPlan.Fields));
		}

		foreach (var table in plan.Tables)
		{
			if (table.MetadataTableId <= 0)
			{
				result.AddError(
					"TABLE_ID_INVALID",
					"QueryPlan存在无效的MetadataTableId。",
					nameof(QueryPlan.Tables));
			}

			if (table.DataSourceId <= 0)
			{
				result.AddError(
					"TABLE_DATASOURCE_INVALID",
					$"查询表“{table.TableName}”没有有效的DataSourceId。",
					nameof(QueryPlan.Tables));
			}

			if (table.DataSourceId != plan.DataSourceId)
			{
				result.AddError(
					"TABLE_DATASOURCE_MISMATCH",
					$"查询表“{table.TableName}”的数据源Id={table.DataSourceId}，与QueryPlan.DataSourceId={plan.DataSourceId}不一致。",
					nameof(QueryPlan.Tables));
			}
		}
	}

	/// <summary>
	/// 收集 QueryPlan 中涉及的 MetadataColumnId。
	/// </summary>
	private static List<long> CollectColumnIds(
		QueryPlan plan)
	{
		var ids =
			new HashSet<long>();

		foreach (var field in plan.Fields)
		{
			if (field.MetadataColumnId > 0)
			{
				ids.Add(
					field.MetadataColumnId);
			}
		}

		foreach (var dimension in plan.Dimensions)
		{
			if (dimension.MetadataColumnId > 0)
			{
				ids.Add(
					dimension.MetadataColumnId);
			}
		}

		foreach (var order in plan.Orders)
		{
			if (order.MetadataColumnId > 0)
			{
				ids.Add(
					order.MetadataColumnId);
			}
		}

		foreach (var join in plan.Joins)
		{
			if (join.LeftColumnId > 0)
			{
				ids.Add(
					join.LeftColumnId);
			}

			if (join.RightColumnId > 0)
			{
				ids.Add(
					join.RightColumnId);
			}
		}

		return ids.ToList();
	}

	/// <summary>
	/// 加载 MetadataTable。
	/// </summary>
	private async Task<List<MetadataTable>>
		LoadMetadataTablesAsync(
			List<long> tableIds)
	{
		if (tableIds.Count == 0)
		{
			return new List<MetadataTable>();
		}

		return await _context.MetadataTables.WhereActiveVersion(_context)
			.AsNoTracking()
			.Where(x =>
				tableIds.Contains(x.Id))
			.ToListAsync();
	}

	/// <summary>
	/// 加载 MetadataColumn。
	/// </summary>
	private async Task<List<MetadataColumn>>
		LoadMetadataColumnsAsync(
			List<long> columnIds)
	{
		if (columnIds.Count == 0)
		{
			return new List<MetadataColumn>();
		}

		return await _context.MetadataColumns.WhereActiveVersion(_context)
			.AsNoTracking()
			.Where(x =>
				columnIds.Contains(x.Id))
			.ToListAsync();
	}

	/// <summary>
	/// 验证查询表。
	/// </summary>
	private static void ValidateTables(
		QueryPlan plan,
		List<MetadataTable> metadataTables,
		QueryPlanValidationResult result)
	{
		var metadataTableMap =
			metadataTables.ToDictionary(
				x => x.Id);

		foreach (var table in plan.Tables)
		{
			if (!metadataTableMap.TryGetValue(
				table.MetadataTableId,
				out var metadataTable))
			{
				result.AddError(
					"TABLE_NOT_FOUND",
					$"MetadataTable不存在，Id={table.MetadataTableId}。",
					nameof(QueryPlan.Tables));

				continue;
			}

			if (metadataTable.DataSourceId !=
				plan.DataSourceId)
			{
				result.AddError(
					"TABLE_DATASOURCE_MISMATCH",
					$"MetadataTable“{metadataTable.TableName}”不属于当前DataSourceId={plan.DataSourceId}。",
					nameof(QueryPlan.Tables));
			}

			if (!string.IsNullOrWhiteSpace(
				table.TableName) &&
				!string.Equals(
					table.TableName,
					metadataTable.TableName,
					StringComparison.OrdinalIgnoreCase))
			{
				result.AddWarning(
					"TABLE_NAME_MISMATCH",
					$"QueryTable中的表名“{table.TableName}”与MetadataTable中的表名“{metadataTable.TableName}”不一致。",
					nameof(QueryPlan.Tables));
			}
		}
	}

	/// <summary>
	/// 验证查询字段。
	/// </summary>
	private static void ValidateFields(
		QueryPlan plan,
		List<MetadataTable> metadataTables,
		List<MetadataColumn> metadataColumns,
		QueryPlanValidationResult result)
	{
		var tableIds =
			plan.Tables
				.Select(x => x.MetadataTableId)
				.ToHashSet();

		var columnMap =
			metadataColumns.ToDictionary(
				x => x.Id);

		foreach (var field in plan.Fields)
		{
			if (field.MetadataColumnId <= 0)
			{
				result.AddError(
					"FIELD_ID_INVALID",
					$"查询字段“{field.ColumnName}”没有有效的MetadataColumnId。",
					nameof(QueryPlan.Fields));

				continue;
			}

			if (!columnMap.TryGetValue(
				field.MetadataColumnId,
				out var column))
			{
				result.AddError(
					"FIELD_NOT_FOUND",
					$"MetadataColumn不存在，Id={field.MetadataColumnId}。",
					nameof(QueryPlan.Fields));

				continue;
			}

			if (!tableIds.Contains(
				column.MetadataTableId))
			{
				result.AddError(
					"FIELD_TABLE_MISMATCH",
					$"字段“{column.ColumnName}”不属于当前QueryPlan中的任何查询表。",
					nameof(QueryPlan.Fields));
			}

			if (!string.IsNullOrWhiteSpace(
				field.ColumnName) &&
				!string.Equals(
					field.ColumnName,
					column.ColumnName,
					StringComparison.OrdinalIgnoreCase))
			{
				result.AddWarning(
					"FIELD_NAME_MISMATCH",
					$"QueryField中的字段名“{field.ColumnName}”与MetadataColumn中的字段名“{column.ColumnName}”不一致。",
					nameof(QueryPlan.Fields));
			}
		}
	}

	/// <summary>
	/// 验证指标。
	/// </summary>
	private static void ValidateMetrics(
		QueryPlan plan,
		List<MetadataColumn> metadataColumns,
		QueryPlanValidationResult result)
	{
		var columnMap =
			metadataColumns.ToDictionary(
				x => x.Id);

		foreach (var metric in plan.Metrics)
		{
			if (string.IsNullOrWhiteSpace(
				metric.Name))
			{
				result.AddError(
					"METRIC_NAME_EMPTY",
					"QueryMetric的Name不能为空。",
					nameof(QueryPlan.Metrics));
			}

			if (string.IsNullOrWhiteSpace(
				metric.Field))
			{
				result.AddError(
					"METRIC_FIELD_EMPTY",
					$"指标“{metric.Name}”没有指定Field。",
					nameof(QueryPlan.Metrics));

				continue;
			}

			// M5-02：以统一字段引用词汇表达"物理解析"判定（口径与原有列名匹配完全一致）。
			if (!SemanticFieldBindingMatcher.IsPhysicallyResolved(SemanticFieldReference.FromPlan(metric), metadataColumns))
			{
				result.AddWarning(
					"METRIC_FIELD_NOT_RESOLVED",
					$"指标“{metric.Name}”的Field“{metric.Field}”无法直接通过字段名解析到MetadataColumn。",
					nameof(QueryPlan.Metrics));
			}

			var aggregation =
				metric.GetAggregation();

			var supported =
				aggregation == QueryAggregation.None ||
				aggregation == QueryAggregation.Sum ||
				aggregation == QueryAggregation.Count ||
				aggregation == QueryAggregation.Average ||
				aggregation == QueryAggregation.Max ||
				aggregation == QueryAggregation.Min ||
				aggregation == QueryAggregation.DistinctCount;

			if (!supported)
			{
				result.AddError(
					"METRIC_AGGREGATION_INVALID",
					$"指标“{metric.Name}”使用了不支持的聚合方式“{metric.Aggregation}”。",
					nameof(QueryPlan.Metrics));
			}
		}
	}

	/// <summary>
	/// 验证维度。
	/// </summary>
	private static void ValidateDimensions(
		QueryPlan plan,
		List<MetadataColumn> metadataColumns,
		QueryPlanValidationResult result)
	{
		var columnMap =
			metadataColumns.ToDictionary(
				x => x.Id);

		foreach (var dimension in plan.Dimensions)
		{
			if (dimension.MetadataColumnId <= 0)
			{
				result.AddError(
					"DIMENSION_ID_INVALID",
					$"维度“{dimension.ColumnName}”没有有效的MetadataColumnId。",
					nameof(QueryPlan.Dimensions));

				continue;
			}

			if (!columnMap.ContainsKey(
				dimension.MetadataColumnId))
			{
				result.AddError(
					"DIMENSION_NOT_FOUND",
					$"维度对应的MetadataColumn不存在，Id={dimension.MetadataColumnId}。",
					nameof(QueryPlan.Dimensions));
			}
		}
	}

	/// <summary>
	/// 验证过滤条件。
	/// </summary>
	private static void ValidateFilters(
		QueryPlan plan,
		List<MetadataColumn> metadataColumns,
		QueryPlanValidationResult result)
	{
		var validOperators =
			new HashSet<string>(
				StringComparer.OrdinalIgnoreCase)
			{
				"=",
				"!=",
				"<>",
				">",
				"<",
				">=",
				"<=",
				"LIKE",
				"NOT LIKE",
				"IN",
				"NOT IN",
				"IS NULL",
				"IS NOT NULL"
			};

		foreach (var filter in plan.Filters)
		{
			if (string.IsNullOrWhiteSpace(
				filter.Field))
			{
				result.AddError(
					"FILTER_FIELD_EMPTY",
					"过滤条件Field不能为空。",
					nameof(QueryPlan.Filters));
			}

			if (!validOperators.Contains(
				filter.Operator))
			{
				result.AddError(
					"FILTER_OPERATOR_INVALID",
					$"字段“{filter.Field}”使用了不支持的过滤操作符“{filter.Operator}”。",
					nameof(QueryPlan.Filters));
			}

			if (!string.Equals(
				filter.Operator,
				"IS NULL",
				StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(
					filter.Operator,
					"IS NOT NULL",
					StringComparison.OrdinalIgnoreCase) &&
				string.IsNullOrWhiteSpace(
					filter.Value))
			{
				result.AddError(
					"FILTER_VALUE_EMPTY",
					$"字段“{filter.Field}”的过滤值不能为空。",
					nameof(QueryPlan.Filters));
			}

			// M5-02：以统一字段引用词汇表达"物理解析"判定（口径与原有列名匹配完全一致）。
			if (!SemanticFieldBindingMatcher.IsPhysicallyResolved(SemanticFieldReference.FromPlan(filter), metadataColumns))
			{
				result.AddWarning(
					"FILTER_FIELD_NOT_RESOLVED",
					$"过滤字段“{filter.Field}”无法直接通过字段名解析到当前MetadataColumn集合。",
					nameof(QueryPlan.Filters));
			}
		}
	}

	/// <summary>
	/// 验证排序。
	/// </summary>
	private static void ValidateOrders(
		QueryPlan plan,
		List<MetadataColumn> metadataColumns,
		QueryPlanValidationResult result)
	{
		foreach (var order in plan.Orders)
		{
			if (order.MetadataColumnId <= 0)
			{
				result.AddError(
					"ORDER_COLUMN_INVALID",
					$"排序字段“{order.Field}”没有有效的MetadataColumnId。",
					nameof(QueryPlan.Orders));
			}

			if (!string.Equals(
				order.Direction,
				"ASC",
				StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(
					order.Direction,
					"DESC",
					StringComparison.OrdinalIgnoreCase))
			{
				result.AddError(
					"ORDER_DIRECTION_INVALID",
					$"排序字段“{order.Field}”使用了非法排序方向“{order.Direction}”。",
					nameof(QueryPlan.Orders));
			}

			if (order.IsMetric &&
				string.IsNullOrWhiteSpace(
					order.MetricName))
			{
				result.AddError(
					"ORDER_METRIC_NAME_EMPTY",
					$"排序字段“{order.Field}”标记为指标排序，但MetricName为空。",
					nameof(QueryPlan.Orders));
			}

			if (order.MetadataColumnId > 0 &&
				!metadataColumns.Any(
					x =>
						x.Id ==
						order.MetadataColumnId))
			{
				result.AddError(
					"ORDER_COLUMN_NOT_FOUND",
					$"排序字段对应的MetadataColumn不存在，Id={order.MetadataColumnId}。",
					nameof(QueryPlan.Orders));
			}
		}
	}

	/// <summary>
	/// 验证 JOIN。
	/// </summary>
	private static void ValidateJoins(
		QueryPlan plan,
		List<MetadataTable> metadataTables,
		List<MetadataColumn> metadataColumns,
		QueryPlanValidationResult result)
	{
		var tableIds =
			metadataTables
				.Select(x => x.Id)
				.ToHashSet();

		var columnMap =
			metadataColumns.ToDictionary(
				x => x.Id);

		foreach (var join in plan.Joins)
		{
			if (join.LeftTableId <= 0 ||
				!tableIds.Contains(
					join.LeftTableId))
			{
				result.AddError(
					"JOIN_LEFT_TABLE_INVALID",
					$"JOIN左侧表不存在，TableId={join.LeftTableId}。",
					nameof(QueryPlan.Joins));
			}

			if (join.RightTableId <= 0 ||
				!tableIds.Contains(
					join.RightTableId))
			{
				result.AddError(
					"JOIN_RIGHT_TABLE_INVALID",
					$"JOIN右侧表不存在，TableId={join.RightTableId}。",
					nameof(QueryPlan.Joins));
			}

			if (!columnMap.TryGetValue(
				join.LeftColumnId,
				out var leftColumn))
			{
				result.AddError(
					"JOIN_LEFT_COLUMN_INVALID",
					$"JOIN左侧字段不存在，ColumnId={join.LeftColumnId}。",
					nameof(QueryPlan.Joins));
			}
			else if (leftColumn.MetadataTableId !=
				join.LeftTableId)
			{
				result.AddError(
					"JOIN_LEFT_COLUMN_TABLE_MISMATCH",
					$"JOIN左侧字段“{leftColumn.ColumnName}”不属于指定的左侧表。",
					nameof(QueryPlan.Joins));
			}

			if (!columnMap.TryGetValue(
				join.RightColumnId,
				out var rightColumn))
			{
				result.AddError(
					"JOIN_RIGHT_COLUMN_INVALID",
					$"JOIN右侧字段不存在，ColumnId={join.RightColumnId}。",
					nameof(QueryPlan.Joins));
			}
			else if (rightColumn.MetadataTableId !=
				join.RightTableId)
			{
				result.AddError(
					"JOIN_RIGHT_COLUMN_TABLE_MISMATCH",
					$"JOIN右侧字段“{rightColumn.ColumnName}”不属于指定的右侧表。",
					nameof(QueryPlan.Joins));
			}

			if (!string.Equals(
				join.JoinType,
				"INNER",
				StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(
					join.JoinType,
					"LEFT",
					StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(
					join.JoinType,
					"RIGHT",
					StringComparison.OrdinalIgnoreCase))
			{
				result.AddError(
					"JOIN_TYPE_INVALID",
					$"JOIN使用了不支持的JOIN类型“{join.JoinType}”。",
					nameof(QueryPlan.Joins));
			}

			if (join.LeftTableId ==
				join.RightTableId &&
				join.LeftColumnId ==
				join.RightColumnId)
			{
				result.AddError(
					"JOIN_SELF_REFERENCE",
					"JOIN不能使用同一张表的同一个字段连接自身。",
					nameof(QueryPlan.Joins));
			}
		}
	}

	/// <summary>
	/// 验证聚合状态。
	/// </summary>
	private static void ValidateAggregation(
		QueryPlan plan,
		QueryPlanValidationResult result)
	{
		var hasAggregation =
			plan.Metrics.Any(
				x =>
					x.GetAggregation() !=
					QueryAggregation.None)
			||
			plan.Fields.Any(
				x =>
					!string.IsNullOrWhiteSpace(
						x.Aggregation) &&
					!string.Equals(
						x.Aggregation,
						"NONE",
						StringComparison.OrdinalIgnoreCase));

		if (hasAggregation &&
			!plan.IsAggregate)
		{
			result.AddWarning(
				"AGGREGATION_FLAG_MISMATCH",
				"QueryPlan存在聚合字段或指标，但IsAggregate=false。",
				nameof(QueryPlan.IsAggregate));
		}

		if (plan.IsAggregate &&
			plan.Metrics.Count == 0 &&
			plan.Fields.All(
				x =>
					string.IsNullOrWhiteSpace(
						x.Aggregation) ||
					string.Equals(
						x.Aggregation,
						"NONE",
						StringComparison.OrdinalIgnoreCase)))
		{
			result.AddWarning(
				"AGGREGATION_WITHOUT_METRIC",
				"QueryPlan标记为聚合查询，但没有发现明确的聚合指标。",
				nameof(QueryPlan.IsAggregate));
		}
	}

	/// <summary>
	/// 验证 Limit 与 Ranking。
	/// </summary>
	private static void ValidateLimitAndRanking(
		QueryPlan plan,
		QueryPlanValidationResult result)
	{
		if (plan.Limit.HasValue &&
			plan.Limit.Value <= 0)
		{
			result.AddError(
				"LIMIT_INVALID",
				$"QueryPlan.Limit必须大于0，当前值={plan.Limit.Value}。",
				nameof(QueryPlan.Limit));
		}

		if (plan.IsRanking &&
			!plan.Limit.HasValue)
		{
			result.AddWarning(
				"RANKING_WITHOUT_LIMIT",
				"QueryPlan标记为Ranking查询，但没有设置Limit。",
				nameof(QueryPlan.Limit));
		}

		if (plan.IsDetailRanking &&
			plan.IsAggregateRanking)
		{
			result.AddError(
				"RANKING_MODE_CONFLICT",
				"QueryPlan不能同时标记为明细Ranking和聚合Ranking。",
				nameof(QueryPlan.IsRanking));
		}

		if (plan.IsAggregateRanking &&
			!plan.IsAggregate)
		{
			result.AddWarning(
				"AGGREGATE_RANKING_FLAG_MISMATCH",
				"QueryPlan标记为聚合Ranking，但IsAggregate=false。",
				nameof(QueryPlan.IsAggregate));
		}
	}
}
