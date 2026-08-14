namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan V2.0 校验器。
///
/// 主要负责解决：
///
/// 1. Ranking / TopN
/// 2. Detail Ranking 与 Aggregate Ranking 区分
/// 3. Limit
/// 4. Order
/// 5. Aggregation 冲突
/// 6. Invalid OrderDirection
///
/// 注意：
///
/// Validator 不负责 Metadata 查询。
/// Validator 不负责 SQL 生成。
///
/// Validator 只负责：
///
/// QueryPlan
///     ↓
/// 校验
///     ↓
/// 修正
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
	/// 规范化聚合方式。
	/// </summary>
	private static void NormalizeAggregations(QueryPlan plan)
	{
		foreach (var field in plan.Fields)
		{
			if (string.IsNullOrWhiteSpace(field.Aggregation))
			{
				field.Aggregation = "NONE";
				continue;
			}

			field.Aggregation =
				field.Aggregation
					.Trim()
					.ToUpperInvariant();

			field.Aggregation =
				field.Aggregation switch
				{
					"AVERAGE" => "AVG",
					"DISTINCT_COUNT" => "DISTINCTCOUNT",
					_ => field.Aggregation
				};
		}

		foreach (var metric in plan.Metrics)
		{
			metric.Aggregation =
				metric.Aggregation
					.Trim()
					.ToUpperInvariant();

			if (string.IsNullOrWhiteSpace(metric.Aggregation))
			{
				metric.Aggregation = "NONE";
			}
		}
	}

	/// <summary>
	/// 规范化排序。
	/// </summary>
	private static void NormalizeOrders(QueryPlan plan)
	{
		foreach (var order in plan.Orders)
		{
			order.Direction =
				string.Equals(
					order.Direction,
					"DESC",
					StringComparison.OrdinalIgnoreCase)
					? "DESC"
					: "ASC";
		}

		/*
         * Legacy QueryIntent → QueryPlan。
         *
         * 第一阶段兼容旧代码。
         */
		if (plan.Orders.Count == 0 &&
			plan.Intent != null &&
			!string.IsNullOrWhiteSpace(plan.Intent.OrderBy))
		{
			plan.Orders.Add(
				new QueryOrder
				{
					Field = plan.Intent.OrderBy!,
					Direction =
						string.Equals(
							plan.Intent.OrderDirection,
							"DESC",
							StringComparison.OrdinalIgnoreCase)
							? "DESC"
							: "ASC"
				});
		}
	}

	/// <summary>
	/// 规范化 Limit。
	/// </summary>
	private static void NormalizeLimit(QueryPlan plan)
	{
		if (plan.Limit == null &&
			plan.Intent?.Limit != null)
		{
			plan.Limit = plan.Intent.Limit;
		}

		if (plan.Limit.HasValue)
		{
			if (plan.Limit.Value <= 0)
			{
				plan.Limit = null;
			}
			else if (plan.Limit.Value > 1000)
			{
				/*
                 * BI Agent 第一阶段安全限制。
                 */
				plan.Limit = 1000;
			}
		}
	}

	/// <summary>
	/// 判断 Ranking 类型。
	/// </summary>
	private static void DetectRanking(QueryPlan plan)
	{
		var intent = plan.Intent;

		plan.IsRanking =
			intent?.IsRanking == true
			||
			plan.Orders.Count > 0
			&& plan.Limit.HasValue;

		if (!plan.IsRanking)
		{
			return;
		}

		/*
         * 判断是否存在 Dimension。
         */
		var hasDimension =
			plan.Dimensions.Count > 0
			||
			plan.Intent?.Dimensions.Count > 0;

		/*
         * 判断是否存在聚合。
         */
		var hasAggregation =
			plan.Fields.Any(
				f => IsAggregation(f.Aggregation));

		/*
         * Ranking + Dimension + Aggregation
         *
         * = Aggregate Ranking
         */
		if (hasDimension && hasAggregation)
		{
			plan.IsAggregateRanking = true;
			plan.IsDetailRanking = false;
		}
		else
		{
			/*
             * Ranking + 无聚合
             *
             * = Detail Ranking
             */
			plan.IsDetailRanking = true;
			plan.IsAggregateRanking = false;
		}
	}

	/// <summary>
	/// 校验 Ranking。
	/// </summary>
	private static void ValidateRanking(QueryPlan plan)
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
         * ---------------------------------------------------------
         * Detail Ranking
         * ---------------------------------------------------------
         *
         * 例如：
         *
         * 数量最多的十条入库凭证
         *
         * 应该：
         *
         * SELECT quantity
         * FROM receipt
         * ORDER BY quantity DESC
         * LIMIT 10
         *
         * 而不是：
         *
         * SELECT SUM(quantity)
         * ...
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
                 * 明细排名：
                 *
                 * 如果只有一个普通字段，
                 * 不允许因为 Ranking 自动变成 SUM。
                 */
				if (order.Aggregation != QueryAggregation.None)
				{
					order.Aggregation =
						QueryAggregation.None;
				}
			}
		}

		/*
         * ---------------------------------------------------------
         * Aggregate Ranking
         * ---------------------------------------------------------
         *
         * 例如：
         *
         * 数量最多的十个物料
         *
         * GROUP BY material
         * ORDER BY SUM(quantity) DESC
         * LIMIT 10
         */
		if (plan.IsAggregateRanking)
		{
			if (plan.Dimensions.Count == 0 &&
				plan.Intent?.Dimensions.Count == 0)
			{
				/*
                 * 有聚合 Ranking，
                 * 但没有维度。
                 *
                 * 不能安全地认为是 TopN。
                 */
				throw new InvalidOperationException(
					"Aggregate Ranking缺少Dimension。");
			}
		}
	}

	/// <summary>
	/// 校验字段。
	/// </summary>
	private static void ValidateFields(QueryPlan plan)
	{
		if (plan.Fields.Count == 0)
		{
			throw new InvalidOperationException(
				"QueryPlan没有任何查询字段。");
		}

		foreach (var field in plan.Fields)
		{
			if (string.IsNullOrWhiteSpace(field.ColumnName))
			{
				throw new InvalidOperationException(
					$"QueryPlan存在无效字段：MetadataColumnId={field.MetadataColumnId}");
			}

			if (!IsValidAggregation(field.Aggregation))
			{
				throw new InvalidOperationException(
					$"不支持的聚合方式：{field.Aggregation}");
			}
		}
	}

	/// <summary>
	/// 判断是否为聚合。
	/// </summary>
	private static bool IsAggregation(
		string? aggregation)
	{
		if (string.IsNullOrWhiteSpace(aggregation))
		{
			return false;
		}

		return aggregation
			.Trim()
			.ToUpperInvariant() switch
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
		if (string.IsNullOrWhiteSpace(aggregation))
		{
			return true;
		}

		return aggregation
			.Trim()
			.ToUpperInvariant() switch
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