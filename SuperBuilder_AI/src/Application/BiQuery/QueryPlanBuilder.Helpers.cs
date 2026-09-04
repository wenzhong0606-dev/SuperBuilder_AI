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
	/// 判断列是否为非信息化展示字段（例如仅表示删除标记或内部用户ID），不适合单列展示。
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
	/// 判断列是否为内部字段（软删除标记、操作人 ID 等），不适合作为明细列表展示列。
	/// 与 <see cref="GetFallbackDisplayColumns"/> 的排除规则保持一致。
	/// </summary>
	private static bool IsInternalColumn(string? name)
	{
		if (string.IsNullOrWhiteSpace(name)) return true;

		if (name.EndsWith("_by", StringComparison.OrdinalIgnoreCase)
			|| name.EndsWith("_flag", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(name, "is_deleted", StringComparison.OrdinalIgnoreCase)
			|| name.IndexOf("deleted", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}

		// 审计人 ID（创建人/修改人）以 _id 结尾、不以下划线 _by 结尾，
		// 容易被误当作业务外键优先展示，必须显式排除。
		// 同时覆盖 LLM 可能写出的 created_by / updated_by 变体。
		return string.Equals(name, "creator_id", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(name, "modifier_id", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(name, "created_by", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(name, "updated_by", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 判断列是否为审计时间戳（create_time / update_time 等）。
	/// 这类列对「入库单明细」几乎没有业务价值，排序时应排在业务时间之后。
	///
	/// 注意：必须以**列尾**精确匹配审计时间列名，不能用前缀匹配。
	/// 否则会误伤业务字段，例如下单时间 created_order_time（前缀命中 created）、
	/// 创建订单号 create_order_no 等。仅认以下典型审计时间列：
	///   create_time / created_at、update_time / updated_at、
	///   modified_time / modified_at（大小写不敏感）。
	/// </summary>
	private static bool IsAuditColumn(string? name)
	{
		if (string.IsNullOrWhiteSpace(name)) return false;

		return name.Equals("create_time", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("created_at", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("created_time", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("update_time", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("updated_at", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("updated_time", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("modified_time", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("modified_at", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 判断当前查询是否属于「明细列表」：非聚合、无聚合指标，且具备明细列表特征（Limit 或 OrderBy）。
	///
	/// 与旧判定的关键差异：不再要求 Dimensions / Filters 为空。
	/// 实战中 LLM（Qwen）对「最近的十个入库单」常顺手返回一个 Dimension（如 status），
	/// 旧判定因此认定其非明细列表，导致明细补列分支被整体跳过，
	/// SELECT 最终只剩「时间字段 + status」这类几乎没有业务价值的列。
	///
	/// 聚合查询（含 SUM/COUNT/AVG/MAX/MIN 指标或聚合字段）永远返回 false，
	/// 因此「按状态统计入库单数量」等聚合场景不会被明细补列污染。
	/// </summary>
	private bool IsDetailListQuery(QueryIntent intent, QueryPlan plan)
	{
		if (intent == null || plan == null) return false;

		// 聚合语义一律排除：任一聚合指标或聚合字段都会让补列语义失真。
		if (intent.Metrics.Any(m => IsAggregation(m.Aggregation))) return false;
		if (plan.Metrics.Any(m => IsAggregation(m.Aggregation))) return false;
		if (plan.Fields.Any(f => IsAggregation(f.Aggregation))) return false;
		if (plan.IsAggregate) return false;

		// 明细列表特征：取前 N 条 / 排序取前 N 条。
		return intent.Limit.HasValue
			|| !string.IsNullOrWhiteSpace(intent.OrderBy)
			|| plan.Limit.HasValue
			|| plan.Orders.Count > 0;
	}

	/// <summary>
	/// 根据表结构返回一个优先展示列的候选列表（按业务价值优先级排序，已去重）。
	///
	/// 优先级：单据号/编码 &gt; 类型/状态 &gt; 外键（仓库/货架等）&gt; 名称
	///       &gt; 数量/金额 &gt; 业务时间 &gt; 审计时间 &gt; 主键 &gt; 其他非内部字段。
	///
	/// 时间字段刻意排在业务属性之后，且审计时间（create_time/update_time）
	/// 排在业务时间（come_time/affirm_time）之后，避免出现
	/// 「SELECT 出来 5 列里 4 个是时间戳」的明细列表。
	/// </summary>
	private IEnumerable<MetadataColumn> GetPreferredDisplayColumns(MetadataTable table)
	{
		if (table == null || table.Columns == null) yield break;

		var columns = table.Columns;
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		List<MetadataColumn> Group(Func<MetadataColumn, bool> predicate)
		{
			var group = new List<MetadataColumn>();

			foreach (var c in columns)
			{
				if (c == null || string.IsNullOrWhiteSpace(c.ColumnName)) continue;

				var name = c.ColumnName;

				if (IsInternalColumn(name)) continue;
				if (!predicate(c)) continue;
				if (!seen.Add(name)) continue;

				group.Add(c);
			}

			return group;
		}

		// 1. 单据号 / 业务编码：code / *_code / *_no / number
		foreach (var c in Group(c =>
			string.Equals(c.ColumnName, "code", StringComparison.OrdinalIgnoreCase)
			|| c.ColumnName!.EndsWith("_code", StringComparison.OrdinalIgnoreCase)
			|| c.ColumnName.EndsWith("_no", StringComparison.OrdinalIgnoreCase)
			|| c.ColumnName.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0
			|| c.ColumnName.IndexOf("number", StringComparison.OrdinalIgnoreCase) >= 0))
		{
			yield return c;
		}

		// 2. 类型 / 状态：type / status / *_type / *_status
		foreach (var c in Group(c =>
			string.Equals(c.ColumnName, "type", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(c.ColumnName, "status", StringComparison.OrdinalIgnoreCase)
			|| c.ColumnName!.EndsWith("_type", StringComparison.OrdinalIgnoreCase)
			|| c.ColumnName.EndsWith("_status", StringComparison.OrdinalIgnoreCase)))
		{
			yield return c;
		}

		// 3. 业务外键：warehouse_id / shelf_id / supplier_id 等（主键 id 由第 7 组处理）
		foreach (var c in Group(c =>
			c.ColumnName!.EndsWith("_id", StringComparison.OrdinalIgnoreCase)))
		{
			yield return c;
		}

		// 4. 名称类（业务名称优先于审计人名称）
		foreach (var c in Group(c =>
				c.ColumnName!.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0)
			.OrderBy(c => IsAuditColumn(c.ColumnName) ? 1 : 0))
		{
			yield return c;
		}

		// 5. 数量 / 金额等业务度量
		foreach (var c in Group(c =>
			c.ColumnName!.IndexOf("material", StringComparison.OrdinalIgnoreCase) >= 0
			|| c.ColumnName.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0
			|| c.ColumnName.IndexOf("quantity", StringComparison.OrdinalIgnoreCase) >= 0
			|| c.ColumnName.IndexOf("qty", StringComparison.OrdinalIgnoreCase) >= 0
			|| c.ColumnName.IndexOf("amount", StringComparison.OrdinalIgnoreCase) >= 0
			|| c.ColumnName.IndexOf("price", StringComparison.OrdinalIgnoreCase) >= 0))
		{
			yield return c;
		}

		// 6. 时间类：业务时间（come_time / affirm_time）优先于审计时间（create_time / update_time）
		foreach (var c in Group(c =>
				!string.IsNullOrWhiteSpace(c.DataType)
				&& (c.DataType.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0
					|| c.DataType.IndexOf("time", StringComparison.OrdinalIgnoreCase) >= 0))
			.OrderBy(c => IsAuditColumn(c.ColumnName) ? 1 : 0))
		{
			yield return c;
		}

		// 7. 主键
		foreach (var c in Group(c =>
			c.IsPrimaryKey == true
			|| string.Equals(c.ColumnName, "id", StringComparison.OrdinalIgnoreCase)))
		{
			yield return c;
		}

		// 8. 兜底：其余非内部字段，复用 Fallback 的排除规则与业务相关性排序
		foreach (var c in GetFallbackDisplayColumns(table, seen))
		{
			yield return c;
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

			// M6 修复：与 SqlQueryBuilder.NormalizeOperator 保持一致，未知操作符默认为 "=" 而非抛异常
			_ => "="
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

			// 去重计数：本项目一等公民聚合（LLM 可能写出多种写法）
			"DISTINCTCOUNT" => true,
			"DISTINCT_COUNT" => true,
			"COUNT_DISTINCT" => true,
			"COUNTDISTINCT" => true,

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
}
