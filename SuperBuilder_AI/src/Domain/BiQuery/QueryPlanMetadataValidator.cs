using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;


namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// QueryPlan Metadata关系验证器。
///
/// Phase 2.1.2
///
/// 职责:
///
/// 验证 QueryPlan 与 Metadata之间的关系。
///
/// 包括:
///
/// 1. Table合法性
/// 2. Field合法性
/// 3. Metric字段合法性
/// 4. Filter字段合法性
/// 5. Dimension字段合法性
/// 6. Order字段合法性
/// 7. Join关系合法性
///
/// 不负责:
///
/// - AI意图理解
/// - Ranking
/// - Aggregation规则
/// - SQL生成
///
/// </summary>
public class QueryPlanMetadataValidator
{

	/// <summary>
	/// 验证 QueryPlan Metadata关系。
	/// </summary>
	public virtual void Validate(
		QueryPlan plan,
		QueryPlanValidationContext context)
	{
		ArgumentNullException.ThrowIfNull(plan);

		ArgumentNullException.ThrowIfNull(context);


		ValidateTables(
			plan,
			context);


		ValidateFields(
			plan,
			context);


		ValidateMetrics(
			plan,
			context);


		ValidateFilters(
			plan,
			context);


		ValidateDimensions(
			plan,
			context);


		ValidateOrders(
			plan,
			context);


		ValidateJoins(
			plan,
			context);
	}



	/// <summary>
	/// 验证 QueryTable。
	/// </summary>
	private static void ValidateTables(
		QueryPlan plan,
		QueryPlanValidationContext context)
	{
		foreach (var table in plan.Tables)
		{
			if (!context.ContainsTable(
				table.MetadataTableId))
			{
				throw new InvalidOperationException(
					$"QueryPlan引用不存在的MetadataTable:{table.MetadataTableId}");
			}
		}
	}



	/// <summary>
	/// 验证 QueryField。
	///
	/// QueryField 使用 MetadataColumnId。
	/// </summary>
	private static void ValidateFields(
		QueryPlan plan,
		QueryPlanValidationContext context)
	{
		foreach (var field in plan.Fields)
		{
			if (!context.Columns.TryGetValue(
				field.MetadataColumnId,
				out var column))
			{
				throw new InvalidOperationException(
					$"QueryField不存在MetadataColumn:{field.MetadataColumnId}");
			}


			if (!string.Equals(
				field.ColumnName,
				column.ColumnName,
				StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException(
					$"QueryField字段名称与Metadata不一致:{field.ColumnName}");
			}
		}
	}



	/// <summary>
	/// 验证 Metric。
	///
	/// 当前 QueryMetric
	/// 没有 MetadataColumnId。
	///
	/// 使用 Field匹配。
	/// </summary>
	private static void ValidateMetrics(
		QueryPlan plan,
		QueryPlanValidationContext context)
	{
		foreach (var metric in plan.Metrics)
		{
			var columns =
				FindColumns(
					plan,
					context,
					metric.Field);


			if (columns.Count == 0)
			{
				throw new InvalidOperationException(
					$"Metric字段不存在:{metric.Field}");
			}


			if (columns.Count > 1)
			{
				throw new InvalidOperationException(
					$"Metric字段存在多个匹配:{metric.Field}");
			}
		}
	}



	/// <summary>
	/// 验证 Filter。
	/// </summary>
	private static void ValidateFilters(
		QueryPlan plan,
		QueryPlanValidationContext context)
	{
		foreach (var filter in plan.Filters)
		{
			var columns =
				FindColumns(
					plan,
					context,
					filter.Field);


			if (columns.Count == 0)
			{
				throw new InvalidOperationException(
					$"Filter字段不存在:{filter.Field}");
			}


			if (columns.Count > 1)
			{
				throw new InvalidOperationException(
					$"Filter字段存在多个匹配:{filter.Field}");
			}
		}
	}



	/// <summary>
	/// 验证 Dimension。
	/// </summary>
	private static void ValidateDimensions(
		QueryPlan plan,
		QueryPlanValidationContext context)
	{
		foreach (var dimension in plan.Dimensions)
		{
			if (!context.ContainsColumn(
				dimension.MetadataColumnId))
			{
				throw new InvalidOperationException(
					$"Dimension字段不存在:{dimension.MetadataColumnId}");
			}
		}
	}



	/// <summary>
	/// 验证 Order。
	///
	/// 优先使用 MetadataColumnId。
	/// </summary>
	private static void ValidateOrders(
		QueryPlan plan,
		QueryPlanValidationContext context)
	{
		foreach (var order in plan.Orders)
		{
			if (order.MetadataColumnId <= 0)
			{
				throw new InvalidOperationException(
					$"Order缺少MetadataColumnId:{order.Field}");
			}


			if (!context.ContainsColumn(
				order.MetadataColumnId))
			{
				throw new InvalidOperationException(
					$"Order字段不存在:{order.MetadataColumnId}");
			}
		}
	}



	/// <summary>
	/// 验证 Join。
	/// </summary>
	private static void ValidateJoins(
		QueryPlan plan,
		QueryPlanValidationContext context)
	{
		foreach (var join in plan.Joins)
		{
			if (!context.ContainsTable(
				join.LeftTableId))
			{
				throw new InvalidOperationException(
					$"Join左表不存在:{join.LeftTableId}");
			}


			if (!context.ContainsTable(
				join.RightTableId))
			{
				throw new InvalidOperationException(
					$"Join右表不存在:{join.RightTableId}");
			}


			if (!context.ContainsColumn(
				join.LeftColumnId))
			{
				throw new InvalidOperationException(
					$"Join左字段不存在:{join.LeftColumnId}");
			}


			if (!context.ContainsColumn(
				join.RightColumnId))
			{
				throw new InvalidOperationException(
					$"Join右字段不存在:{join.RightColumnId}");
			}
		}
	}



	/// <summary>
	/// 根据字段名称搜索 MetadataColumn。
	///
	/// 用于:
	///
	/// QueryMetric
	/// QueryFilter
	///
	/// </summary>
	private static List<MetadataColumn> FindColumns(
		QueryPlan plan,
		QueryPlanValidationContext context,
		string? fieldName)
	{
		var result =
			new List<MetadataColumn>();


		if (string.IsNullOrWhiteSpace(
			fieldName))
		{
			return result;
		}


		foreach (var table in plan.Tables)
		{
			foreach (var column in context.GetColumns(
				table.MetadataTableId))
			{
				if (string.Equals(
					column.ColumnName,
					fieldName,
					StringComparison.OrdinalIgnoreCase))
				{
					result.Add(column);
				}
			}
		}


		return result;
	}
}