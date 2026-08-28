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
	/// 根据表结构返回一个优先展示列的候选列表（按优先级排序）。
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
