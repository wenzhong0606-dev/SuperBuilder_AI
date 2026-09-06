using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 列级安全默认策略（M5-05）：将未授权受限列从 QueryPlan 中剔除。
///
/// 不进入 Plan → SQL Builder 不会生成该列 → 结果自然不含该列，
/// 满足验收"未授权 / 脱敏字段不进入 Plan、SQL、结果"。
/// </summary>
public sealed class ColumnSecurityPolicy : IColumnSecurityPolicy
{
	private readonly IColumnSensitivityClassifier _classifier;

	/// <summary>创建列级安全策略。</summary>
	public ColumnSecurityPolicy(IColumnSensitivityClassifier classifier)
	{
		_classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
	}

	/// <inheritdoc />
	public async Task<ColumnSecurityResult> ApplyAsync(
		QueryPlan plan,
		ColumnSecurityContext ctx,
		QueryPlanValidationContext? validationContext,
		CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(plan);
		ArgumentNullException.ThrowIfNull(ctx);

		var result = new ColumnSecurityResult();
		var nameToId = BuildNameToId(validationContext);

		// Fields（明确列 Id）
		var blockedFields = new List<QueryField>();
		foreach (var f in plan.Fields)
			if (await RestrictedAsync(f.MetadataColumnId, f.ColumnName, ctx, ct))
				blockedFields.Add(f);
		foreach (var f in blockedFields)
		{
			plan.Fields.Remove(f);
			result.Blocked.Add(new BlockedColumnReference
			{
				Collection = nameof(QueryField),
				ColumnId = f.MetadataColumnId,
				ColumnName = f.ColumnName,
				Reason = "未授权的受限列，已从投影字段中剔除"
			});
		}

		// Dimensions（主列 + Key/Label 列任一受限即整维度拦截）
		var blockedDimensions = new List<QueryDimension>();
		foreach (var d in plan.Dimensions)
		{
			var ids = new[] { d.MetadataColumnId, d.DimensionKeyColumnId, d.DimensionLabelColumnId }
				.Where(x => x is > 0)
				.Select(x => x!.Value)
				.Distinct();
			var blocked = false;
			foreach (var id in ids)
			{
				if (await RestrictedAsync(id, d.ColumnName, ctx, ct)) { blocked = true; break; }
			}
			if (blocked) blockedDimensions.Add(d);
		}
		foreach (var d in blockedDimensions)
		{
			plan.Dimensions.Remove(d);
			result.Blocked.Add(new BlockedColumnReference
			{
				Collection = nameof(QueryDimension),
				ColumnId = d.MetadataColumnId,
				ColumnName = d.ColumnName,
				Reason = "未授权的受限列，已从维度中剔除"
			});
		}

		// Orders（指标排序无列，跳过）
		var blockedOrders = new List<QueryOrder>();
		foreach (var o in plan.Orders)
		{
			if (o.IsMetric) continue;
			if (await RestrictedAsync(o.MetadataColumnId, o.Field, ctx, ct))
				blockedOrders.Add(o);
		}
		foreach (var o in blockedOrders)
		{
			plan.Orders.Remove(o);
			result.Blocked.Add(new BlockedColumnReference
			{
				Collection = nameof(QueryOrder),
				ColumnId = o.MetadataColumnId,
				ColumnName = o.Field,
				Reason = "未授权的受限列，已从排序中剔除"
			});
		}

		// Joins（左/右列任一受限即整 join 拦截，断 join 优于留半截）
		var blockedJoins = new List<QueryJoin>();
		foreach (var j in plan.Joins)
		{
			var ids = new[] { j.LeftColumnId, j.RightColumnId }
				.Where(x => x is > 0).Distinct();
			var blocked = false;
			foreach (var id in ids)
			{
				if (await RestrictedAsync(id, null, ctx, ct)) { blocked = true; break; }
			}
			if (blocked) blockedJoins.Add(j);
		}
		foreach (var j in blockedJoins)
		{
			plan.Joins.Remove(j);
			result.Blocked.Add(new BlockedColumnReference
			{
				Collection = nameof(QueryJoin),
				ColumnId = j.LeftColumnId,
				ColumnName = j.LeftColumnName,
				TableName = j.LeftTableName,
				Reason = "未授权的受限列，已从表连接中剔除"
			});
		}

		// Filters（纯列名 → 反查 Id）
		var blockedFilters = new List<QueryFilter>();
		foreach (var f in plan.Filters)
			if (await RestrictedByNameAsync(f.Field, ctx, nameToId, ct))
				blockedFilters.Add(f);
		foreach (var f in blockedFilters)
		{
			plan.Filters.Remove(f);
			result.Blocked.Add(new BlockedColumnReference
			{
				Collection = nameof(QueryFilter),
				ColumnName = f.Field,
				Reason = "未授权的受限列，已从过滤条件中剔除"
			});
		}

		// Metrics（纯列名 → 反查 Id）
		var blockedMetrics = new List<QueryMetric>();
		foreach (var m in plan.Metrics)
			if (await RestrictedByNameAsync(m.Field, ctx, nameToId, ct))
				blockedMetrics.Add(m);
		foreach (var m in blockedMetrics)
		{
			plan.Metrics.Remove(m);
			result.Blocked.Add(new BlockedColumnReference
			{
				Collection = nameof(QueryMetric),
				ColumnName = m.Field,
				Reason = "未授权的受限列，已从指标中剔除"
			});
		}

		// 主投影全部清空 → 无法执行，交由管线拒绝
		if (plan.Fields.Count == 0 && plan.Metrics.Count == 0 && plan.Dimensions.Count == 0)
			result.RequiresRejection = result.Blocked.Count > 0;

		return result;
	}

	private async Task<bool> RestrictedAsync(long columnId, string? columnName, ColumnSecurityContext ctx, CancellationToken ct)
	{
		var restricted = await _classifier.IsRestrictedAsync(columnId, columnName, null, ctx, ct);
		return restricted && !ctx.AuthorizedColumnIds.Contains(columnId);
	}

	private async Task<bool> RestrictedByNameAsync(string field, ColumnSecurityContext ctx, Dictionary<string, long> nameToId, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(field)) return false;
		if (nameToId.TryGetValue(field, out var id) && id > 0)
			return await RestrictedAsync(id, field, ctx, ct);
		// 反查不到（表达式 / 指标别名）→ 保守不拦截
		return false;
	}

	private static Dictionary<string, long> BuildNameToId(QueryPlanValidationContext? validationContext)
	{
		var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		if (validationContext is null) return map;
		foreach (var kvp in validationContext.TableColumns)
			foreach (var col in kvp.Value)
				if (col.Id > 0 && !string.IsNullOrWhiteSpace(col.ColumnName) && !map.ContainsKey(col.ColumnName!))
					map[col.ColumnName!] = col.Id;
		return map;
	}
}
