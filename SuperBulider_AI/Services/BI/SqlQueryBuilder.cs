using System.Text;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 动态SQL生成服务。
///
/// Phase 1.6.1
///
/// 根据:
///
/// QueryPlan
///     ↓
/// ISqlDialect
///     ↓
/// SqlQuery
///
/// 支持:
///
/// SQL Server
/// MySQL
/// PostgreSQL
///
/// 注意:
///
/// 当前系统是动态数据库平台。
///
/// 不假设数据库存在:
///
/// Foreign Key
/// Navigation
/// Relationship
///
/// 因此本服务不会自动生成JOIN。
///
/// 多表JOIN必须在QueryPlan中明确建立关系后
/// 才能进入SQL生成阶段。
/// </summary>
public class SqlQueryBuilder
	: ISqlQueryBuilder
{
	/// <summary>
	/// 根据QueryPlan和数据库方言生成SQL。
	/// </summary>
	/// <param name="plan">
	/// 查询执行计划。
	/// </param>
	/// <param name="dialect">
	/// 数据库SQL方言。
	/// </param>
	/// <returns>
	/// 包含SQL和参数的SqlQuery。
	/// </returns>
	public Task<SqlQuery> BuildAsync(
		QueryPlan plan,
		ISqlDialect dialect)
	{
		if (plan == null)
		{
			throw new ArgumentNullException(
				nameof(plan));
		}

		if (dialect == null)
		{
			throw new ArgumentNullException(
				nameof(dialect));
		}

		/*
		 * ============================================================
		 * 1.
		 * 验证QueryPlan
		 * ============================================================
		 */

		if (plan.Tables.Count == 0)
		{
			throw new InvalidOperationException(
				"QueryPlan没有查询表。");
		}

		/*
		 * 当前QueryPlan尚未包含JOIN关系模型。
		 *
		 * 因此禁止直接对多个表生成SQL。
		 */

		if (plan.Tables.Count > 1)
		{
			throw new InvalidOperationException(
				"当前QueryPlan包含多个数据表，但QueryPlan尚未定义表之间的动态关系，无法安全生成JOIN SQL。");
		}

		var table =
			plan.Tables[0];

		if (string.IsNullOrWhiteSpace(
			table.TableName))
		{
			throw new InvalidOperationException(
				"QueryPlan中的TableName不能为空。");
		}

		/*
		 * ============================================================
		 * 2.
		 * 创建SQL
		 * ============================================================
		 */

		var sql =
			new StringBuilder();

		var parameters =
			new Dictionary<string, object?>();

		/*
		 * ============================================================
		 * 3.
		 * SELECT
		 * ============================================================
		 */

		sql.Append(
			"SELECT ");

		var selectFields =
			BuildSelectFields(
				plan,
				dialect);

		/*
		 * 如果没有明确字段，
		 * 使用 *。
		 *
		 * 但QueryPlanBuilder正常情况下应该已经有Fields。
		 */

		if (selectFields.Count == 0)
		{
			selectFields.Add("*");
		}

		sql.Append(
			string.Join(
				", ",
				selectFields));

		/*
		 * ============================================================
		 * 4.
		 * FROM
		 * ============================================================
		 */

		sql.Append(
			" FROM ");

		sql.Append(
			dialect.EscapeIdentifier(
				table.TableName));

		/*
		 * ============================================================
		 * 5.
		 * WHERE
		 * ============================================================
		 */

		BuildWhere(
			sql,
			parameters,
			plan,
			dialect);

		/*
		 * ============================================================
		 * 6.
		 * GROUP BY
		 * ============================================================
		 */

		BuildGroupBy(
			sql,
			plan,
			dialect);

		/*
		 * ============================================================
		 * 7.
		 * ORDER BY
		 * ============================================================
		 */

		BuildOrderBy(
			sql,
			plan,
			dialect);

		/*
		 * ============================================================
		 * 8.
		 * LIMIT
		 * ============================================================
		 */

		var finalSql = sql.ToString();

		var limit = ResolveLimit(plan);

		if (limit.HasValue)
		{
			if (limit.Value <= 0)
			{
				throw new InvalidOperationException(
					"查询Limit必须大于0。");
			}

			finalSql =
				dialect.ApplyLimit(
					finalSql,
					limit.Value);
		}

		/*
		 * ============================================================
		 * 9.
		 * 返回SqlQuery
		 * ============================================================
		 */

		return Task.FromResult(
			new SqlQuery
			{
				Sql =
					finalSql,

				Parameters =
					parameters
			});
	}

	/// <summary>
	/// 构建SELECT字段。
	/// </summary>
	private static List<string> BuildSelectFields(
		QueryPlan plan,
		ISqlDialect dialect)
	{
		var result =
			new List<string>();

		foreach (var field in plan.Fields)
		{
			if (string.IsNullOrWhiteSpace(
				field.ColumnName))
			{
				continue;
			}

			var column =
				dialect.EscapeIdentifier(
					field.ColumnName);

			var aggregation =
				NormalizeAggregation(
					field.Aggregation);

			if (aggregation == "NONE")
			{
				result.Add(
					column);

				continue;
			}

			/*
			 * COUNT(*)特殊处理。
			 *
			 * 如果AI以后将Field映射为*，
			 * 则允许:
			 *
			 * COUNT(*)
			 */

			if (aggregation == "COUNT"
				&&
				field.ColumnName == "*")
			{
				result.Add(
					"COUNT(*)");

				continue;
			}

			result.Add(
				$"{aggregation}({column})");
		}

		/*
		 * 如果没有SELECT字段，
		 * 返回空集合，由调用方决定是否使用*。
		 */

		return result;
	}

	/// <summary>
	/// 构建WHERE条件。
	/// </summary>
	private static void BuildWhere(
		StringBuilder sql,
		Dictionary<string, object?> parameters,
		QueryPlan plan,
		ISqlDialect dialect)
	{
		if (plan.Filters.Count == 0)
		{
			return;
		}

		var conditions =
			new List<string>();

		for (
			var i = 0;
			i < plan.Filters.Count;
			i++)
		{
			var filter =
				plan.Filters[i];

			if (string.IsNullOrWhiteSpace(
				filter.Field))
			{
				continue;
			}

			var field =
				dialect.EscapeIdentifier(
					filter.Field);

			var operation =
				NormalizeOperator(
					filter.Operator);

			/*
			 * IS NULL / IS NOT NULL
			 * 不需要参数。
			 */

			if (operation == "IS NULL"
				||
				operation == "IS NOT NULL")
			{
				conditions.Add(
					$"{field} {operation}");

				continue;
			}

			var parameterName =
				dialect.GetParameterName(i);

			// 尝试根据 QueryPlan 中对应字段的数据类型，将参数转换为合适的 CLR 类型，
			// 避免将数值或日期等字段当作字符串传入导致比较不准确。
			var fieldDataType =
				plan.Fields.FirstOrDefault(f =>
					string.Equals(f.ColumnName, filter.Field, StringComparison.OrdinalIgnoreCase))
					?.DataType;

			/*
			 * IN需要特殊处理。
			 *
			 * 当前QueryFilter.Value仍然是string，
			 * 因此这里暂时按照逗号分隔值处理。
			 */

			if (operation == "IN")
			{
				var values =
					ParseInValues(
						filter.Value);

				if (values.Count == 0)
				{
					continue;
				}

				var parameterNames =
					new List<string>();

				for (
					var valueIndex = 0;
					valueIndex < values.Count;
					valueIndex++)
				{
					var name =
						dialect.GetParameterName(
							i * 1000 + valueIndex);

					parameterNames.Add(
						name);

					parameters[name] =
						values[valueIndex];
				}

				conditions.Add(
					$"{field} IN ({string.Join(", ", parameterNames)})");

				continue;
			}

			conditions.Add(
				$"{field} {operation} {parameterName}");

			parameters[parameterName] =
				ConvertParameterValue(filter.Value, fieldDataType);
		}

		if (conditions.Count == 0)
		{
			return;
		}

		sql.Append(
			" WHERE ");

		sql.Append(
			string.Join(
				" AND ",
				conditions));
	}

	/// <summary>
	/// 构建GROUP BY。
	///
	/// V2.0:
	///
	/// 优先使用 QueryPlan.Dimensions。
	///
	/// Legacy fallback:
	///
	/// 当 QueryPlan.Dimensions 为空时，
	/// 回退到 QueryIntent.Dimensions。
	/// </summary>
	private static void BuildGroupBy(
		StringBuilder sql,
		QueryPlan plan,
		ISqlDialect dialect)
	{
		var groups =
			new List<string>();

		/*
		 * ============================================================
		 * V2.0
		 *
		 * QueryPlan.Dimensions
		 * ============================================================
		 */

		if (plan.Dimensions.Count > 0)
		{
			foreach (var dimension in plan.Dimensions)
			{
				if (dimension == null)
				{
					continue;
				}

				if (string.IsNullOrWhiteSpace(
					dimension.ColumnName))
				{
					continue;
				}

				groups.Add(
					dialect.EscapeIdentifier(
						dimension.ColumnName));
			}
		}

		/*
		 * ============================================================
		 * Legacy fallback
		 *
		 * QueryIntent.Dimensions
		 * ============================================================
		 */

		if (groups.Count == 0
			&&
			plan.Intent?.Dimensions != null)
		{
			foreach (var dimension in plan.Intent.Dimensions)
			{
				if (string.IsNullOrWhiteSpace(
					dimension))
				{
					continue;
				}

				groups.Add(
					dialect.EscapeIdentifier(
						dimension));
			}
		}

		if (groups.Count == 0)
		{
			return;
		}

		sql.Append(
			" GROUP BY ");

		sql.Append(
			string.Join(
				", ",
				groups.Distinct(
					StringComparer.OrdinalIgnoreCase)));
	}

	/// <summary>
	/// 构建ORDER BY。
	///
	/// V2.0:
	///
	/// 优先使用 QueryPlan.Orders。
	///
	/// 支持:
	///
	/// 1. 普通字段排序
	/// 2. 指标排序
	/// 3. 聚合指标排序
	///
	/// 例如:
	///
	/// quantity DESC
	///
	/// SUM(quantity) DESC
	///
	/// COUNT(id) DESC
	///
	/// Legacy fallback:
	///
	/// 当 QueryPlan.Orders 为空时，
	/// 回退到 QueryIntent.OrderBy。
	/// </summary>
	private static void BuildOrderBy(
		StringBuilder sql,
		QueryPlan plan,
		ISqlDialect dialect)
	{
		/*
		 * ============================================================
		 * V2.0
		 *
		 * QueryPlan.Orders
		 * ============================================================
		 */

		if (plan.Orders.Count > 0)
		{
			var orderExpressions =
				new List<string>();

			foreach (var order in plan.Orders)
			{
				if (order == null)
				{
					continue;
				}

				if (string.IsNullOrWhiteSpace(
					order.Field))
				{
					continue;
				}

				var direction =
					NormalizeOrderDirection(
						order.Direction);

				var field =
					dialect.EscapeIdentifier(
						order.Field);

				/*
				 * ----------------------------------------------------
				 * 指标排序
				 *
				 * 例如：
				 *
				 * 数量最多
				 *
				 * SUM(quantity) DESC
				 * ----------------------------------------------------
				 */

				if (order.IsMetric
					&&
					order.Aggregation
						!= QueryAggregation.None)
				{
					var aggregation =
						NormalizeAggregation(
							order.Aggregation.ToString());

					if (aggregation != "NONE")
					{
						if (aggregation == "COUNT"
							&&
							order.Field == "*")
						{
							orderExpressions.Add(
								$"COUNT(*) {direction}");
						}
						else
						{
							orderExpressions.Add(
								$"{aggregation}({field}) {direction}");
						}

						continue;
					}
				}

				/*
				 * ----------------------------------------------------
				 * 普通字段排序
				 *
				 * 例如：
				 *
				 * receipt_date DESC
				 * ----------------------------------------------------
				 */

				orderExpressions.Add(
					$"{field} {direction}");
			}

			if (orderExpressions.Count > 0)
			{
				sql.Append(
					" ORDER BY ");

				sql.Append(
					string.Join(
						", ",
						orderExpressions));

				return;
			}
		}

		/*
		 * ============================================================
		 * Legacy fallback
		 *
		 * QueryIntent.OrderBy
		 * ============================================================
		 */

		if (plan.Intent == null
			||
			string.IsNullOrWhiteSpace(
				plan.Intent.OrderBy))
		{
			return;
		}

		var legacyOrderBy =
			plan.Intent.OrderBy;

		var legacyDirection =
			NormalizeOrderDirection(
				plan.Intent.OrderDirection);

		sql.Append(
			" ORDER BY ");

		sql.Append(
			dialect.EscapeIdentifier(
				legacyOrderBy));

		sql.Append(
			" ");

		sql.Append(
			legacyDirection);
	}

	/// <summary>
	/// 标准化聚合类型。
	/// </summary>
	private static string NormalizeAggregation(
		string? aggregation)
	{
		if (string.IsNullOrWhiteSpace(
			aggregation))
		{
			return "NONE";
		}

		return aggregation
			.Trim()
			.ToUpperInvariant() switch
		{
			"SUM" => "SUM",
			"COUNT" => "COUNT",
			"AVG" => "AVG",
			"MAX" => "MAX",
			"MIN" => "MIN",
			"NONE" => "NONE",
			_ => "NONE"
		};
	}

	/// <summary>
	/// 标准化过滤操作符。
	///
	/// 防止AI直接注入SQL片段。
	/// </summary>
	private static string NormalizeOperator(
		string? value)
	{
		if (string.IsNullOrWhiteSpace(
			value))
		{
			return "=";
		}

		return value
			.Trim()
			.ToUpperInvariant() switch
		{
			"=" => "=",
			">" => ">",
			"<" => "<",
			">=" => ">=",
			"<=" => "<=",
			"<>" => "<>",
			"!=" => "<>",
			"LIKE" => "LIKE",
			"IN" => "IN",
			"IS NULL" => "IS NULL",
			"IS NOT NULL" => "IS NOT NULL",
			_ => "="
		};
	}

	/// <summary>
	/// 标准化排序方向。
	/// </summary>
	private static string NormalizeOrderDirection(
		string? direction)
	{
		if (string.Equals(
			direction,
			"DESC",
			StringComparison.OrdinalIgnoreCase))
		{
			return "DESC";
		}

		return "ASC";
	}

	/// <summary>
	/// 解析IN条件值。
	///
	/// 例如:
	///
	/// 1,2,3
	///
	/// 转换为:
	///
	/// @p1
	/// @p2
	/// @p3
	/// </summary>
	private static List<string> ParseInValues(
		string? value)
	{
		if (string.IsNullOrWhiteSpace(
			value))
		{
			return new List<string>();
		}

		return value
			.Split(
				',',
				StringSplitOptions.RemoveEmptyEntries)
			.Select(x =>
				x.Trim())
			.Where(x =>
				!string.IsNullOrWhiteSpace(x))
			.ToList();
	}

	/// <summary>
	/// 根据字段的数据类型将参数字符串转换为合适的 CLR 值。
	/// 支持常见的数值、布尔和日期类型。
	/// </summary>
	private static object? ConvertParameterValue(string? value, string? dataType)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		if (string.IsNullOrWhiteSpace(dataType))
		{
			// 没有类型信息时，直接返回原始字符串
			return value;
		}

		switch (dataType.Trim().ToLowerInvariant())
		{
			case "int":
			case "integer":
			case "smallint":
			case "mediumint":
			case "bigint":
				if (long.TryParse(value, out var l)) return l;
				break;
			case "decimal":
			case "numeric":
			case "float":
			case "double":
				if (decimal.TryParse(value, out var d)) return d;
				break;
			case "bit":
			case "bool":
			case "boolean":
				if (bool.TryParse(value, out var b)) return b;
				break;
			case "date":
			case "datetime":
			case "timestamp":
				if (DateTime.TryParse(value, out var dt)) return dt;
				break;
			default:
				// 对于非数值类型，保留原字符串
				return value;
		}

		return value;
	}

	/// <summary>
	/// 获取最终Limit。
	///
	/// V2.0 优先使用 QueryPlan.Limit。
	///
	/// 为兼容旧版 QueryIntent，
	/// 当 QueryPlan.Limit 没有值时，
	/// 回退到 QueryIntent.Limit。
	/// </summary>
	private static int? ResolveLimit(
		QueryPlan plan)
	{
		if (plan.Limit.HasValue)
		{
			return plan.Limit.Value;
		}

		return plan.Intent?.Limit;
	}
}