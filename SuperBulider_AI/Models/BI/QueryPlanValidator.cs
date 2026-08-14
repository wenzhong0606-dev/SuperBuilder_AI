namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan V2.0 校验器。
///
/// 职责：
/// 1. Ranking / TopN
/// 2. Detail Ranking / Aggregate Ranking
/// 3. Limit
/// 4. Order
/// 5. Aggregation
/// 6. 字段合法性
///
/// Validator 不负责：
/// - Metadata 查询
/// - AI 意图理解
/// - SQL 生成
///
/// Validator 只负责：
///
/// QueryPlan
///     ↓
/// Normalize
///     ↓
/// Validate
///     ↓
/// QueryPlan
/// </summary>
public class QueryPlanValidator
{
	/// <summary>
	/// 校验并规范化 QueryPlan。
	/// </summary>
	public QueryPlan Validate(QueryPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan);

		if (plan.Tables.Count == 0)
		{
			throw new InvalidOperationException(
				"QueryPlan没有查询表。");
		}

		NormalizeAggregations(plan);

		NormalizeOrders(plan);

		NormalizeLimit(plan);

		DetectRanking(plan);

		ValidateRanking(plan);

		ValidateFields(plan);

		return plan;
	}

	/// <summary>
	/// 规范化 QueryPlan 中的聚合。
	///
	/// 同时处理：
	/// QueryField.Aggregation
	/// QueryMetric.Aggregation
	/// </summary>
	private static void NormalizeAggregations(QueryPlan plan)
	{
		foreach (var field in plan.Fields)
		{
			field.Aggregation =
				NormalizeAggregation(field.Aggregation);
		}

		foreach (var metric in plan.Metrics)
		{
			metric.Aggregation =
				NormalizeAggregation(metric.Aggregation);
		}
	}

	/// <summary>
	/// 统一聚合名称。
	/// </summary>
	private static string NormalizeAggregation(
		string? aggregation)
	{
		if (string.IsNullOrWhiteSpace(aggregation))
		{
			return "NONE";
		}

		return aggregation
			.Trim()
			.ToUpperInvariant() switch
		{
			"AVERAGE" => "AVG",
			"DISTINCT_COUNT" => "DISTINCTCOUNT",
			_ => aggregation.Trim().ToUpperInvariant()
		};
	}

	/// <summary>
	/// 规范化排序。
	///
	/// V2 优先使用：
	/// plan.Orders
	///
	/// Legacy Intent 只作为兼容 fallback。
	/// </summary>
	private static void NormalizeOrders(QueryPlan plan)
	{
		foreach (var order in plan.Orders)
		{
			order.Field =
				order.Field?.Trim() ?? string.Empty;

			order.Direction =
				NormalizeDirection(order.Direction);
		}

		/*
         * Legacy QueryIntent → QueryPlan。
         *
         * 只有 V2 Orders 没有生成时，
         * 才使用 Intent.OrderBy。
         */
		if (plan.Orders.Count == 0 &&
			plan.Intent != null &&
			!string.IsNullOrWhiteSpace(
				plan.Intent.OrderBy))
		{
			plan.Orders.Add(
				new QueryOrder
				{
					Field =
						plan.Intent.OrderBy!.Trim(),

					Direction =
						NormalizeDirection(
							plan.Intent.OrderDirection),

					IsMetric = true,

					MetricName =
						plan.Intent.OrderBy
				});
		}
	}

	/// <summary>
	/// 规范化排序方向。
	/// </summary>
	private static string NormalizeDirection(
		string? direction)
	{
		return string.Equals(
			direction?.Trim(),
			"DESC",
			StringComparison.OrdinalIgnoreCase)
			? "DESC"
			: "ASC";
	}

	/// <summary>
	/// 规范化 Limit。
	///
	/// V2：
	/// plan.Limit
	///
	/// Legacy：
	/// plan.Intent.Limit
	/// </summary>
	private static void NormalizeLimit(QueryPlan plan)
	{
		if (!plan.Limit.HasValue &&
			plan.Intent?.Limit != null)
		{
			plan.Limit =
				plan.Intent.Limit;
		}

		if (!plan.Limit.HasValue)
		{
			return;
		}

		if (plan.Limit.Value <= 0)
		{
			plan.Limit = null;
			return;
		}

		/*
         * BI Agent 第一阶段安全上限。
         */
		if (plan.Limit.Value > 1000)
		{
			plan.Limit = 1000;
		}
	}

	/// <summary>
	/// 判断 Ranking 类型。
	///
	/// 第一优先级：
	/// QueryPlan V2
	///
	/// 第二优先级：
	/// QueryIntent Legacy
	/// </summary>
	private static void DetectRanking(
		QueryPlan plan)
	{
		var intent = plan.Intent;

		/*
         * Ranking 的基本条件：
         *
         * 1. Intent 明确要求 Ranking
         * 或
         * 2. 存在 Order + Limit
         */
		plan.IsRanking =
			intent?.IsRanking == true
			||
			(
				plan.Orders.Count > 0
				&&
				plan.Limit.HasValue
			);

		if (!plan.IsRanking)
		{
			plan.IsDetailRanking = false;
			plan.IsAggregateRanking = false;

			return;
		}

		/*
         * V2 Dimension。
         */
		var hasDimension =
			plan.Dimensions.Count > 0
			||
			plan.Intent?.Dimensions.Count > 0;

		/*
         * V2 Metric。
         *
         * 优先使用 Metrics，
         * Fields 作为兼容判断。
         */
		var hasAggregation =
			plan.Metrics.Any(
				m => IsAggregation(
					m.Aggregation))
			||
			plan.Fields.Any(
				f => IsAggregation(
					f.Aggregation));

		/*
         * Ranking + Dimension + Aggregation
         *
         * =
         *
         * Aggregate Ranking
         */
		if (hasDimension &&
			hasAggregation)
		{
			plan.IsAggregateRanking = true;
			plan.IsDetailRanking = false;
		}
		else
		{
			/*
             * Ranking + 无聚合
             *
             * =
             *
             * Detail Ranking
             */
			plan.IsDetailRanking = true;
			plan.IsAggregateRanking = false;
		}
	}

	/// <summary>
	/// 校验 Ranking。
	/// </summary>
	private static void ValidateRanking(
		QueryPlan plan)
	{
		if (!plan.IsRanking)
		{
			return;
		}

		/*
         * Ranking 必须有 Limit。
         */
		if (!plan.Limit.HasValue)
		{
			throw new InvalidOperationException(
				"Ranking查询缺少Limit。");
		}

		/*
         * Ranking 必须有 Order。
         */
		if (plan.Orders.Count == 0)
		{
			throw new InvalidOperationException(
				"Ranking查询缺少OrderBy。");
		}

		/*
         * Ranking 的 Limit 必须有效。
         */
		if (plan.Limit.Value <= 0)
		{
			throw new InvalidOperationException(
				"Ranking查询的Limit必须大于0。");
		}

		/*
         * ---------------------------------------------------------
         * Detail Ranking
         * ---------------------------------------------------------
         *
         * 示例：
         *
         * 数量最多的十条入库凭证
         *
         * 正确：
         *
         * ORDER BY quantity DESC
         * LIMIT 10
         *
         * 不应该：
         *
         * ORDER BY SUM(quantity) DESC
         */
		if (plan.IsDetailRanking)
		{
			foreach (var order in plan.Orders)
			{
				if (!order.IsMetric)
				{
					continue;
				}

				/*
                 * Detail Ranking 不允许排序聚合。
                 */
				order.Aggregation =
					QueryAggregation.None;
			}
		}

		/*
         * ---------------------------------------------------------
         * Aggregate Ranking
         * ---------------------------------------------------------
         *
         * 示例：
         *
         * 数量最多的十个物料
         *
         * GROUP BY material
         *
         * ORDER BY SUM(quantity) DESC
         *
         * LIMIT 10
         */
		if (plan.IsAggregateRanking)
		{
			if (plan.Dimensions.Count == 0 &&
				plan.Intent?.Dimensions.Count == 0)
			{
				throw new InvalidOperationException(
					"Aggregate Ranking缺少Dimension。");
			}

			NormalizeAggregateOrders(plan);
		}
	}

	/// <summary>
	/// Aggregate Ranking 的 Order
	/// 必须与对应 Metric 的聚合方式保持一致。
	/// </summary>
	private static void NormalizeAggregateOrders(
		QueryPlan plan)
	{
		foreach (var order in plan.Orders)
		{
			if (!order.IsMetric)
			{
				continue;
			}

			/*
             * 优先通过 MetricName 匹配。
             */
			QueryMetric? metric = null;

			if (!string.IsNullOrWhiteSpace(
					order.MetricName))
			{
				metric =
					plan.Metrics.FirstOrDefault(
						m =>
							string.Equals(
								m.Name,
								order.MetricName,
								StringComparison.OrdinalIgnoreCase)
							||
							string.Equals(
								m.Field,
								order.MetricName,
								StringComparison.OrdinalIgnoreCase));
			}

			/*
             * 如果没有 MetricName，
             * 再通过 Field 匹配。
             */
			metric ??=
				plan.Metrics.FirstOrDefault(
					m =>
						string.Equals(
							m.Field,
							order.Field,
							StringComparison.OrdinalIgnoreCase));

			if (metric == null)
			{
				/*
                 * 当前无法确认对应 Metric。
                 *
                 * 不擅自制造 SUM。
                 */
				continue;
			}

			var aggregation =
				NormalizeAggregation(
					metric.Aggregation);

			if (aggregation == "NONE")
			{
				/*
                 * Aggregate Ranking 没有明确聚合，
                 * 默认使用 SUM。
                 *
                 * 这是针对“数量最多的十个物料”
                 * 这一类典型 BI TopN 的确定性兜底。
                 */
				aggregation = "SUM";

				metric.Aggregation =
					aggregation;
			}

			order.Aggregation =
				ToQueryAggregation(
					aggregation);
		}
	}

	/// <summary>
	/// 将字符串聚合转换成 QueryAggregation。
	/// </summary>
	private static QueryAggregation ToQueryAggregation(
		string aggregation)
	{
		return aggregation switch
		{
			"SUM" =>
				QueryAggregation.Sum,

			"COUNT" =>
				QueryAggregation.Count,

			"AVG" =>
				QueryAggregation.Average,

			"MAX" =>
				QueryAggregation.Max,

			"MIN" =>
				QueryAggregation.Min,

			"DISTINCTCOUNT" =>
				QueryAggregation.DistinctCount,

			_ =>
				QueryAggregation.None
		};
	}

	/// <summary>
	/// 校验字段。
	/// </summary>
	private static void ValidateFields(
		QueryPlan plan)
	{
		if (plan.Fields.Count == 0)
		{
			throw new InvalidOperationException(
				"QueryPlan没有任何查询字段。");
		}

		foreach (var field in plan.Fields)
		{
			if (string.IsNullOrWhiteSpace(
					field.ColumnName))
			{
				throw new InvalidOperationException(
					$"QueryPlan存在无效字段：MetadataColumnId={field.MetadataColumnId}");
			}

			field.Aggregation =
				NormalizeAggregation(
					field.Aggregation);

			if (!IsValidAggregation(
					field.Aggregation))
			{
				throw new InvalidOperationException(
					$"不支持的聚合方式：{field.Aggregation}");
			}
		}

		/*
         * Metrics 也必须拥有有效聚合。
         */
		foreach (var metric in plan.Metrics)
		{
			metric.Aggregation =
				NormalizeAggregation(
					metric.Aggregation);

			if (!IsValidAggregation(
					metric.Aggregation))
			{
				throw new InvalidOperationException(
					$"Metric不支持的聚合方式：{metric.Aggregation}");
			}
		}

		/*
         * Orders 必须拥有字段。
         */
		foreach (var order in plan.Orders)
		{
			if (string.IsNullOrWhiteSpace(
					order.Field))
			{
				throw new InvalidOperationException(
					"QueryPlan存在没有Field的Order。");
			}

			order.Direction =
				NormalizeDirection(
					order.Direction);
		}
	}

	/// <summary>
	/// 判断是否为聚合。
	/// </summary>
	private static bool IsAggregation(
		string? aggregation)
	{
		if (string.IsNullOrWhiteSpace(
				aggregation))
		{
			return false;
		}

		return NormalizeAggregation(
				aggregation) switch
		{
			"SUM" => true,
			"COUNT" => true,
			"AVG" => true,
			"MAX" => true,
			"MIN" => true,
			"DISTINCTCOUNT" => true,
			_ => false
		};
	}

	/// <summary>
	/// 判断聚合是否合法。
	/// </summary>
	private static bool IsValidAggregation(
		string? aggregation)
	{
		if (string.IsNullOrWhiteSpace(
				aggregation))
		{
			return true;
		}

		return NormalizeAggregation(
				aggregation) switch
		{
			"NONE" => true,
			"SUM" => true,
			"COUNT" => true,
			"AVG" => true,
			"MAX" => true,
			"MIN" => true,
			"DISTINCTCOUNT" => true,
			_ => false
		};
	}
}