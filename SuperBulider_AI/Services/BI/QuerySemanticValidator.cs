using SuperBulider_AI.Models.BI;
using SuperBulider_AI.Models.Metadata;


namespace SuperBulider_AI.Services.BI;

/// <summary>
/// QueryPlan业务语义验证器。
///
/// Phase 2.2.3
///
/// 职责:
///
/// QueryPlan
///
///     ↓
///
/// MetadataColumn
///
///     ↓
///
/// MetadataSemantic
///
///
/// 验证:
///
/// 1. Metric业务语义
/// 2. Dimension业务语义
/// 3. Filter时间语义
/// 4. Aggregation合法性
///
/// 不负责:
///
/// - Metadata搜索
/// - AI理解
/// - SQL生成
/// </summary>
public class QuerySemanticValidator
{

	/// <summary>
	/// 执行语义验证。
	/// </summary>
	public QuerySemanticValidationResult Validate(
		QueryPlan plan,
		QueryPlanValidationContext context)
	{
		ArgumentNullException.ThrowIfNull(plan);

		ArgumentNullException.ThrowIfNull(context);


		var result =
			new QuerySemanticValidationResult();


		ValidateMetrics(
			plan,
			context,
			result);


		ValidateDimensions(
			plan,
			context,
			result);


		ValidateFilters(
			plan,
			context,
			result);


		ValidateAggregations(
			plan,
			context,
			result);


		return result;
	}



	#region Metric


	/// <summary>
	/// Metric业务语义验证。
	/// </summary>
	private static void ValidateMetrics(
		QueryPlan plan,
		QueryPlanValidationContext context,
		QuerySemanticValidationResult result)
	{
		foreach (var metric in plan.Metrics)
		{
			var column =
				FindColumn(
					metric.Field,
					context);



			if (column == null)
			{
				result.AddError(
					"MetricFieldNotFound",
					metric.Field,
					$"Metric字段不存在:{metric.Field}");

				continue;
			}



			if (!CanAggregate(
				metric.SemanticType,
				column,
				metric.Aggregation))
			{
				result.AddError(
					"InvalidMetricAggregation",
					metric.Field,
					$"业务语义{metric.SemanticType}不支持聚合:{metric.Aggregation}",
					column.Id,
					column.MetadataTableId);
			}
		}
	}


	#endregion



	#region Dimension


	/// <summary>
	/// Dimension业务语义验证。
	///
	/// QueryDimension真实模型:
	///
	/// MetadataColumnId
	/// ColumnName
	/// SemanticType
	/// </summary>
	private static void ValidateDimensions(
		QueryPlan plan,
		QueryPlanValidationContext context,
		QuerySemanticValidationResult result)
	{
		foreach (var dimension in plan.Dimensions)
		{

			var column =
				FindColumnById(
					dimension.MetadataColumnId,
					context);



			column ??=
				FindColumn(
					dimension.ColumnName,
					context);



			if (column == null)
			{
				result.AddError(
					"DimensionFieldNotFound",
					dimension.ColumnName,
					$"Dimension字段不存在:{dimension.ColumnName}");

				continue;
			}



			if (IsNumeric(column))
			{
				result.AddWarning(
					"NumericDimension",
					dimension.ColumnName,
					$"数值字段不建议作为Dimension:{dimension.ColumnName}",
					column.Id,
					column.MetadataTableId);
			}



			if (!string.IsNullOrWhiteSpace(
				dimension.SemanticType))
			{
				var type =
					dimension.SemanticType
						.Trim()
						.ToUpperInvariant();



				var allowed =
					new[]
					{
						"DIMENSION",
						"ENTITY",
						"CATEGORY",
						"TIME"
					};



				if (!allowed.Contains(type))
				{
					result.AddWarning(
						"InvalidDimensionSemanticType",
						dimension.ColumnName,
						$"未知Dimension语义类型:{dimension.SemanticType}",
						column.Id,
						column.MetadataTableId);
				}
			}
		}
	}


	#endregion



	#region Filter


	/// <summary>
	/// Filter语义验证。
	/// </summary>
	private static void ValidateFilters(
		QueryPlan plan,
		QueryPlanValidationContext context,
		QuerySemanticValidationResult result)
	{
		foreach (var filter in plan.Filters)
		{

			var column =
				FindColumn(
					filter.Field,
					context);



			if (column == null)
			{
				result.AddError(
					"FilterFieldNotFound",
					filter.Field,
					$"Filter字段不存在:{filter.Field}");

				continue;
			}



			if (IsDateOperator(filter.Operator)
				&&
				!IsDateColumn(column))
			{
				result.AddError(
					"InvalidDateFilter",
					filter.Field,
					$"字段{filter.Field}不是日期字段",
					column.Id,
					column.MetadataTableId);
			}
		}
	}


	#endregion



	#region Aggregation


	/// <summary>
	/// QueryField聚合验证。
	/// </summary>
	private static void ValidateAggregations(
		QueryPlan plan,
		QueryPlanValidationContext context,
		QuerySemanticValidationResult result)
	{
		foreach (var field in plan.Fields)
		{
			if (string.IsNullOrWhiteSpace(
				field.Aggregation)
				||
				field.Aggregation == "NONE")
			{
				continue;
			}



			var column =
				FindColumn(
					field.ColumnName,
					context);



			if (column == null)
			{
				continue;
			}



			if (!CanAggregate(
				null,
				column,
				field.Aggregation))
			{
				result.AddError(
					"InvalidFieldAggregation",
					field.ColumnName,
					$"字段{field.ColumnName}不能执行聚合:{field.Aggregation}",
					column.Id,
					column.MetadataTableId);
			}
		}
	}


	#endregion



	#region Metadata Lookup


	private static MetadataColumn? FindColumn(
		string field,
		QueryPlanValidationContext context)
	{
		return context.Columns.Values
			.FirstOrDefault(
				x =>
					string.Equals(
						x.ColumnName,
						field,
						StringComparison.OrdinalIgnoreCase));
	}



	private static MetadataColumn? FindColumnById(
		long id,
		QueryPlanValidationContext context)
	{
		if (id <= 0)
		{
			return null;
		}


		return context.Columns.TryGetValue(
			id,
			out var column)
			? column
			: null;
	}


	#endregion



	#region Semantic Rules


	private static bool CanAggregate(
		string? semanticType,
		MetadataColumn column,
		string? aggregation)
	{
		if (string.IsNullOrWhiteSpace(
			aggregation)
			||
			aggregation == "NONE")
		{
			return true;
		}



		var semantic =
			semanticType?
				.Trim()
				.ToUpperInvariant();



		switch (semantic)
		{
			case "AMOUNT":
			case "QUANTITY":
			case "RATIO":

				return aggregation switch
				{
					"SUM" => true,
					"AVG" => true,
					"MAX" => true,
					"MIN" => true,
					"COUNT" => true,
					_ => false
				};


			case "COUNT":

				return aggregation == "COUNT"
					||
					aggregation == "DISTINCTCOUNT";


			case "DATE":

				return aggregation == "COUNT";


		}



		var type =
			column.DataType
				.ToLowerInvariant();



		var numeric =
			type.Contains("int")
			||
			type.Contains("decimal")
			||
			type.Contains("float")
			||
			type.Contains("double");



		if (numeric)
		{
			return aggregation switch
			{
				"SUM" => true,
				"AVG" => true,
				"MAX" => true,
				"MIN" => true,
				"COUNT" => true,
				_ => false
			};
		}



		return aggregation switch
		{
			"COUNT" => true,

			"DISTINCTCOUNT" => true,

			_ => false
		};
	}



	private static bool IsNumeric(
		MetadataColumn column)
	{
		var type =
			column.DataType
				.ToLowerInvariant();


		return type.Contains("int")
			||
			type.Contains("decimal")
			||
			type.Contains("float")
			||
			type.Contains("double");
	}



	private static bool IsDateColumn(
		MetadataColumn column)
	{
		var type =
			column.DataType
				.ToLowerInvariant();


		return type.Contains("date")
			||
			type.Contains("time");
	}



	private static bool IsDateOperator(
		string op)
	{
		return op.Contains(
			"DATE",
			StringComparison.OrdinalIgnoreCase)
			||
			op.Contains(
				"YEAR",
				StringComparison.OrdinalIgnoreCase)
			||
			op.Contains(
				"MONTH",
				StringComparison.OrdinalIgnoreCase);
	}


	#endregion
}