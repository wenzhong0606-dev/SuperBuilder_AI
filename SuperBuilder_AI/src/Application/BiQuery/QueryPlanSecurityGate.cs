using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 对已经完成语义规划及行级策略注入的 QueryPlan 做不可修复的最终复核。
/// 任一安全归属或策略快照不一致时，必须在 SQL 生成前拒绝。
/// </summary>
public sealed class QueryPlanSecurityGate : IQueryPlanSecurityGate
{
	private readonly SuperBIContext _db;
	private readonly IDataSourceAuthorizationService _authorization;
	private readonly IRowLevelSecurityService _rowSecurity;
	private readonly IDataSourceExecutionIdentityAccessor _identity;

	public QueryPlanSecurityGate(
		SuperBIContext db,
		IDataSourceAuthorizationService authorization,
		IRowLevelSecurityService rowSecurity,
		IDataSourceExecutionIdentityAccessor identity)
	{
		_db = db;
		_authorization = authorization;
		_rowSecurity = rowSecurity;
		_identity = identity;
	}

	public async Task ValidateAsync(QueryPlan plan, long tenantId, long userId, CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(plan);
		var caller = _identity.Current;
		RejectIf(tenantId <= 0 || userId <= 0 || caller is null || caller.TenantId != tenantId || caller.UserId != userId);
		RejectIf(plan.EffectiveTenantId != tenantId || plan.DataSourceId <= 0 || plan.Tables.Count == 0);

		var sourceValid = await _db.DataSources.AsNoTracking().AnyAsync(
			x => x.Id == plan.DataSourceId && x.TenantId == tenantId && x.Enabled == true, ct);
		RejectIf(!sourceValid || !await _authorization.IsAuthorizedAsync(tenantId, userId, plan.DataSourceId, ct));

		var tableIds = plan.Tables.Select(x => x.MetadataTableId).ToArray();
		RejectIf(tableIds.Any(x => x <= 0) || tableIds.Distinct().Count() != tableIds.Length ||
			plan.Tables.Any(x => x.DataSourceId != plan.DataSourceId));

		var metadataTables = await _db.MetadataTables.AsNoTracking()
			.Where(x => tableIds.Contains(x.Id) && x.TenantId == tenantId && x.DataSourceId == plan.DataSourceId)
			.ToDictionaryAsync(x => x.Id, ct);
		RejectIf(metadataTables.Count != tableIds.Length || plan.Tables.Any(x =>
			!metadataTables.TryGetValue(x.MetadataTableId, out var table) || !Same(x.TableName, table.TableName)));

		var columns = await _db.MetadataColumns.AsNoTracking()
			.Where(x => tableIds.Contains(x.MetadataTableId))
			.ToListAsync(ct);
		var byId = columns.ToDictionary(x => x.Id);
		var columnNames = columns.Select(x => x.ColumnName ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);

		foreach (var field in plan.Fields)
			RejectIf(!ColumnMatches(byId, field.MetadataColumnId, field.ColumnName));
		foreach (var dimension in plan.Dimensions)
		{
			RejectIf(!ColumnMatches(byId, dimension.MetadataColumnId, dimension.ColumnName));
			if (dimension.DimensionKeyColumnId.HasValue)
				RejectIf(!ColumnMatches(byId, dimension.DimensionKeyColumnId.Value, dimension.DimensionKeyColumnName));
			if (dimension.DimensionLabelColumnId.HasValue)
				RejectIf(!ColumnMatches(byId, dimension.DimensionLabelColumnId.Value, dimension.DimensionLabelColumnName));
		}
		foreach (var filter in plan.Filters)
			RejectIf(!columnNames.Contains(filter.Field));
		foreach (var metric in plan.Metrics)
			RejectIf(!columnNames.Contains(metric.Field));
		foreach (var order in plan.Orders)
		{
			if (order.MetadataColumnId > 0)
				RejectIf(!ColumnMatches(byId, order.MetadataColumnId, order.Field));
			else
				RejectIf(!columnNames.Contains(order.Field) && !plan.Metrics.Any(x =>
					Same(order.MetricName ?? order.Field, x.Name) || Same(order.MetricName ?? order.Field, x.Alias)));
		}

		foreach (var join in plan.Joins)
		{
			RejectIf(!metadataTables.TryGetValue(join.LeftTableId, out var leftTable) ||
				!metadataTables.TryGetValue(join.RightTableId, out var rightTable) ||
				!Same(join.LeftTableName, leftTable.TableName) || !Same(join.RightTableName, rightTable.TableName));
			RejectIf(!ColumnBelongs(byId, join.LeftColumnId, join.LeftTableId, join.LeftColumnName) ||
				!ColumnBelongs(byId, join.RightColumnId, join.RightTableId, join.RightColumnName));
		}

		var expected = new QueryPlan
		{
			EffectiveTenantId = tenantId,
			DataSourceId = plan.DataSourceId,
			Tables = plan.Tables.Select(x => new QueryTable
			{
				MetadataTableId = x.MetadataTableId,
				DataSourceId = x.DataSourceId,
				TableName = x.TableName
			}).ToList()
		};
		await _rowSecurity.ApplyAsync(expected, tenantId, userId, ct);
		RejectIf(!Same(plan.DataPolicyFingerprint, expected.DataPolicyFingerprint) ||
			!Canonical(plan.MandatoryRowFilters).SequenceEqual(Canonical(expected.MandatoryRowFilters), StringComparer.Ordinal));
	}

	private static bool ColumnMatches(IReadOnlyDictionary<long, Models.Metadata.MetadataColumn> columns, long id, string? name) =>
		id > 0 && columns.TryGetValue(id, out var column) && Same(name, column.ColumnName);

	private static bool ColumnBelongs(IReadOnlyDictionary<long, Models.Metadata.MetadataColumn> columns, long id, long tableId, string? name) =>
		ColumnMatches(columns, id, name) && columns[id].MetadataTableId == tableId;

	private static IEnumerable<string> Canonical(IEnumerable<MandatoryRowFilter> filters) => filters
		.Select(x => string.Join('\u001f', x.PolicyId, x.MetadataTableId, x.MetadataColumnId, Normalize(x.TableName),
			Normalize(x.Field), Normalize(x.DataType), Normalize(x.Operator), x.Value, x.Deny))
		.OrderBy(x => x, StringComparer.Ordinal);

	private static bool Same(string? left, string? right) =>
		string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

	private static string Normalize(string? value) => value?.Trim() ?? string.Empty;

	private static void RejectIf(bool rejected)
	{
		if (rejected) throw SuperBuilderException.FromCode(ErrorCodes.QueryPlanSecurityRejected, 403);
	}
}
