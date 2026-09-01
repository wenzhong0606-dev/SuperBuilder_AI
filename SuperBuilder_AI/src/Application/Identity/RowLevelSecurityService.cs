using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Services.Identity;

public sealed class RowLevelSecurityService : IRowLevelSecurityService
{
	private readonly SuperBIContext _db;
	private readonly IDataSourceExecutionIdentityAccessor _identity;

	public RowLevelSecurityService(SuperBIContext db, IDataSourceExecutionIdentityAccessor identity)
	{
		_db = db;
		_identity = identity;
	}

	public async Task<string> GetPolicyFingerprintAsync(long tenantId, long userId, CancellationToken ct = default)
	{
		var roleIds = await RoleIdsAsync(tenantId, userId, ct);
		var policies = await _db.RowLevelSecurityPolicies.AsNoTracking()
			.Where(x => x.TenantId == tenantId && x.Enabled)
			.OrderBy(x => x.Id)
			.Select(x => new { x.Id, x.Version, x.UpdatedTime, x.SubjectType, x.SubjectId, x.SubjectKey, x.SubjectValue })
			.ToListAsync(ct);
		var attributes = _identity.Current?.Attributes ?? new Dictionary<string, string>();
		var material = string.Join("|", policies.Select(x => $"{x.Id}:{x.Version}:{x.UpdatedTime.Ticks}:{(int)x.SubjectType}:{x.SubjectId}:{x.SubjectKey}:{x.SubjectValue}"))
			+ "|roles:" + string.Join(',', roleIds.OrderBy(x => x))
			+ "|attrs:" + string.Join(',', attributes.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"));
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
	}

	public async Task ApplyAsync(QueryPlan plan, long tenantId, long userId, CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(plan);
		if (tenantId <= 0 || userId <= 0)
			throw SuperBuilderException.FromCode(ErrorCodes.RowPolicyForbidden, 403);

		var tableIds = plan.Tables.Select(x => x.MetadataTableId).Where(x => x > 0).Distinct().ToArray();
		var policies = await _db.RowLevelSecurityPolicies.AsNoTracking()
			.Where(x => x.TenantId == tenantId && x.DataSourceId == plan.DataSourceId && x.Enabled && tableIds.Contains(x.MetadataTableId))
			.OrderBy(x => x.Id).ToListAsync(ct);
		var roleIds = await RoleIdsAsync(tenantId, userId, ct);
		var attributes = _identity.Current?.Attributes ?? new Dictionary<string, string>();

		plan.MandatoryRowFilters.Clear();
		foreach (var table in plan.Tables)
		{
			var governed = policies.Where(x => x.MetadataTableId == table.MetadataTableId).ToList();
			if (governed.Count == 0) continue;
			var applicable = governed.Where(x => Matches(x, userId, roleIds, attributes)).ToList();
			if (!applicable.Any(x => x.Effect == RowPolicyEffect.Allow))
				throw SuperBuilderException.FromCode(ErrorCodes.RowPolicyForbidden, 403);

			var columnIds = applicable.Select(x => x.MetadataColumnId).Distinct().ToArray();
			var columns = await _db.MetadataColumns.AsNoTracking()
				.Include(x => x.MetadataTable)
				.Where(x => columnIds.Contains(x.Id) && x.MetadataTableId == table.MetadataTableId &&
					x.MetadataTable != null && x.MetadataTable.TenantId == tenantId && x.MetadataTable.DataSourceId == plan.DataSourceId)
				.ToDictionaryAsync(x => x.Id, ct);
			if (columns.Count != columnIds.Length)
				throw SuperBuilderException.FromCode(ErrorCodes.RowPolicyForbidden, 403);

			foreach (var policy in applicable)
			{
				var column = columns[policy.MetadataColumnId];
				plan.MandatoryRowFilters.Add(new MandatoryRowFilter
				{
					PolicyId = policy.Id,
					MetadataTableId = table.MetadataTableId,
					MetadataColumnId = policy.MetadataColumnId,
					TableName = table.TableName ?? column.MetadataTable?.TableName ?? string.Empty,
					Field = column.ColumnName ?? string.Empty,
					DataType = column.DataType,
					Operator = policy.Operator,
					Value = policy.Value,
					Deny = policy.Effect == RowPolicyEffect.Deny
				});
			}
		}

		plan.DataPolicyFingerprint = await GetPolicyFingerprintAsync(tenantId, userId, ct);
	}

	private async Task<HashSet<long>> RoleIdsAsync(long tenantId, long userId, CancellationToken ct) =>
		(await _db.UserRoles.AsNoTracking().Where(x => x.TenantId == tenantId && x.UserId == userId).Select(x => x.RoleId).ToListAsync(ct)).ToHashSet();

	private static bool Matches(RowLevelSecurityPolicy policy, long userId, HashSet<long> roleIds, IReadOnlyDictionary<string, string> attributes) =>
		policy.SubjectType switch
		{
			RowPolicySubjectType.Everyone => true,
			RowPolicySubjectType.User => policy.SubjectId == userId,
			RowPolicySubjectType.Role => policy.SubjectId.HasValue && roleIds.Contains(policy.SubjectId.Value),
			RowPolicySubjectType.Attribute => !string.IsNullOrWhiteSpace(policy.SubjectKey) &&
				attributes.TryGetValue(policy.SubjectKey, out var value) && string.Equals(value, policy.SubjectValue, StringComparison.Ordinal),
			_ => false
		};
}
