using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 明细查询在进入语义验证前的确定性投影策略。
/// 防止模型只选择状态/时间字段，导致业务明细缺少单据号、来源、仓库等关键列。
/// </summary>
public static class DetailQueryProjectionPolicy
{
	private const int TargetColumnCount = 10;

	public static void Apply(QueryPlan plan, QueryPlanValidationContext context, string? question)
	{
		ArgumentNullException.ThrowIfNull(plan);
		ArgumentNullException.ThrowIfNull(context);

		if (!IsDetailList(plan)) return;

		var main = plan.Tables.FirstOrDefault();
		if (main is null || !context.TableColumns.TryGetValue(main.MetadataTableId, out var columns)) return;

		var byId = columns.Where(c => c.Id > 0).ToDictionary(c => c.Id);
		var selectedColumns = plan.Fields
			.Select(f => byId.GetValueOrDefault(f.MetadataColumnId)
				?? columns.FirstOrDefault(c => string.Equals(c.ColumnName, f.ColumnName, StringComparison.OrdinalIgnoreCase)))
			.Where(c => c is not null)
			.Cast<MetadataColumn>()
			.ToList();

		var lowInformationProjection = selectedColumns.Count == 0
			|| selectedColumns.All(c => IsTemporal(c) || IsStatusOrType(c));

		if (lowInformationProjection)
		{
			// 业务时间保留；未被问题明确要求的创建/更新时间不占用首屏关键字段位置。
			plan.Fields.RemoveAll(f => IsAuditTime(f.ColumnName) && !QuestionMentions(question, f.ColumnName));
		}

		if (lowInformationProjection || plan.Fields.Count < TargetColumnCount)
		{
			var selected = new HashSet<string>(
				plan.Fields.Where(f => !string.IsNullOrWhiteSpace(f.ColumnName)).Select(f => f.ColumnName!),
				StringComparer.OrdinalIgnoreCase);

			foreach (var column in columns
				.Where(IsDisplayColumn)
				.OrderBy(DisplayPriority)
				.ThenBy(c => c.Id))
			{
				if (plan.Fields.Count >= TargetColumnCount) break;
				if (string.IsNullOrWhiteSpace(column.ColumnName) || !selected.Add(column.ColumnName)) continue;

				plan.Fields.Add(new QueryField
				{
					MetadataColumnId = column.Id,
					ColumnName = column.ColumnName,
					DataType = column.DataType,
					Aggregation = "NONE"
				});
			}
		}

		ApplySoftDelete(plan, columns);
	}

	private static bool IsDetailList(QueryPlan plan)
	{
		if (plan.IsAggregate) return false;
		if (plan.Metrics.Any(m => IsAggregation(m.Aggregation))) return false;
		if (plan.Fields.Any(f => IsAggregation(f.Aggregation))) return false;
		return plan.Limit.HasValue || plan.Orders.Count > 0;
	}

	private static bool IsAggregation(string? value)
		=> (value ?? string.Empty).Trim().ToUpperInvariant() is "SUM" or "COUNT" or "AVG" or "MIN" or "MAX";

	private static bool IsDisplayColumn(MetadataColumn column)
	{
		var name = column.ColumnName ?? string.Empty;
		var type = column.DataType ?? string.Empty;
		if (string.IsNullOrWhiteSpace(name)) return false;
		if (name.Equals("del_flag", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("is_deleted", StringComparison.OrdinalIgnoreCase)
			|| name.EndsWith("_by", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("create_by", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("update_by", StringComparison.OrdinalIgnoreCase)) return false;
		return !type.Contains("blob", StringComparison.OrdinalIgnoreCase)
			&& !type.Contains("clob", StringComparison.OrdinalIgnoreCase)
			&& !type.Equals("text", StringComparison.OrdinalIgnoreCase);
	}

	private static int DisplayPriority(MetadataColumn column)
	{
		var name = column.ColumnName ?? string.Empty;
		if (name.Equals("code", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_no", StringComparison.OrdinalIgnoreCase)) return 0;
		if (name.Contains("code", StringComparison.OrdinalIgnoreCase)) return 1;
		if (IsStatusOrType(column)) return 2;
		if (name.EndsWith("_id", StringComparison.OrdinalIgnoreCase) && !name.Equals("id", StringComparison.OrdinalIgnoreCase)) return 3;
		if (name.Contains("name", StringComparison.OrdinalIgnoreCase)) return 4;
		if (name.Contains("quantity", StringComparison.OrdinalIgnoreCase) || name.Contains("amount", StringComparison.OrdinalIgnoreCase)) return 5;
		if (IsTemporal(column) && !IsAuditTime(name)) return 6;
		if (column.IsPrimaryKey == true || name.Equals("id", StringComparison.OrdinalIgnoreCase)) return 8;
		if (IsAuditTime(name)) return 9;
		return 7;
	}

	private static bool IsTemporal(MetadataColumn column)
	{
		var type = column.DataType ?? string.Empty;
		var name = column.ColumnName ?? string.Empty;
		return type.Contains("date", StringComparison.OrdinalIgnoreCase)
			|| type.Contains("time", StringComparison.OrdinalIgnoreCase)
			|| name.EndsWith("_time", StringComparison.OrdinalIgnoreCase)
			|| name.EndsWith("_date", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsStatusOrType(MetadataColumn column)
	{
		var name = column.ColumnName ?? string.Empty;
		return name.Equals("status", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("type", StringComparison.OrdinalIgnoreCase)
			|| name.EndsWith("_status", StringComparison.OrdinalIgnoreCase)
			|| name.EndsWith("_type", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsAuditTime(string? name)
		=> name is not null && (name.Equals("create_time", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("created_at", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("update_time", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("updated_at", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("modified_time", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("modified_at", StringComparison.OrdinalIgnoreCase));

	private static bool QuestionMentions(string? question, string? field)
		=> !string.IsNullOrWhiteSpace(question)
			&& !string.IsNullOrWhiteSpace(field)
			&& question.Contains(field, StringComparison.OrdinalIgnoreCase);

	private static void ApplySoftDelete(QueryPlan plan, IReadOnlyCollection<MetadataColumn> columns)
	{
		if (plan.Filters.Any(f => f.Field is not null
			&& (f.Field.Equals("del_flag", StringComparison.OrdinalIgnoreCase)
				|| f.Field.Equals("is_deleted", StringComparison.OrdinalIgnoreCase)))) return;

		var column = columns.FirstOrDefault(c => c.ColumnName is not null
			&& (c.ColumnName.Equals("del_flag", StringComparison.OrdinalIgnoreCase)
				|| c.ColumnName.Equals("is_deleted", StringComparison.OrdinalIgnoreCase)));
		if (column?.ColumnName is null) return;

		plan.Filters.Add(new QueryFilter
		{
			SemanticText = "未删除",
			Field = column.ColumnName,
			DataType = column.DataType,
			Operator = "=",
			Value = IsBoolean(column.DataType) ? "false" : "0"
		});
	}

	private static bool IsBoolean(string? dataType)
		=> dataType is not null && (dataType.Contains("bool", StringComparison.OrdinalIgnoreCase)
			|| dataType.Equals("bit", StringComparison.OrdinalIgnoreCase));
}
