using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Services.Identity;

public sealed class DataSourceExecutionIdentityAccessor : IDataSourceExecutionIdentityAccessor
{
	public DataSourceExecutionIdentity? Current { get; set; }
}

public sealed class DataSourceAuthorizationService : IDataSourceAuthorizationService
{
	private readonly SuperBIContext _db;

	public DataSourceAuthorizationService(SuperBIContext db) => _db = db;

	public async Task<IReadOnlyList<long>> GetAuthorizedDataSourceIdsAsync(long tenantId, long userId, CancellationToken ct = default)
	{
		if (tenantId <= 0 || userId <= 0) return Array.Empty<long>();
		var roleIds = await _db.UserRoles.AsNoTracking()
			.Where(x => x.TenantId == tenantId && x.UserId == userId)
			.Select(x => x.RoleId).ToListAsync(ct);

		return await (
			from grant in _db.DataSourceAccessGrants.AsNoTracking()
			join source in _db.DataSources.AsNoTracking() on grant.DataSourceId equals source.Id
			where grant.TenantId == tenantId && source.TenantId == tenantId && source.Enabled == true &&
				((grant.SubjectType == DataSourceGrantSubjectType.User && grant.SubjectId == userId) ||
				 (grant.SubjectType == DataSourceGrantSubjectType.Role && roleIds.Contains(grant.SubjectId)))
			select source.Id).Distinct().OrderBy(id => id).ToListAsync(ct);
	}

	public async Task<bool> IsAuthorizedAsync(long tenantId, long userId, long dataSourceId, CancellationToken ct = default) =>
		(await GetAuthorizedDataSourceIdsAsync(tenantId, userId, ct)).Contains(dataSourceId);

	public async Task GrantAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default)
	{
		if (!await SubjectAndSourceExist(tenantId, dataSourceId, subjectType, subjectId, ct))
			throw new InvalidOperationException("DataSource 或授权主体不存在，或不属于目标租户。");
		if (await _db.DataSourceAccessGrants.AnyAsync(x => x.TenantId == tenantId && x.DataSourceId == dataSourceId && x.SubjectType == subjectType && x.SubjectId == subjectId, ct)) return;
		_db.DataSourceAccessGrants.Add(new DataSourceAccessGrant { TenantId = tenantId, DataSourceId = dataSourceId, SubjectType = subjectType, SubjectId = subjectId });
		await _db.SaveChangesAsync(ct);
	}

	public async Task RevokeAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default)
	{
		var grants = await _db.DataSourceAccessGrants.Where(x => x.TenantId == tenantId && x.DataSourceId == dataSourceId && x.SubjectType == subjectType && x.SubjectId == subjectId).ToListAsync(ct);
		_db.DataSourceAccessGrants.RemoveRange(grants);
		await _db.SaveChangesAsync(ct);
	}

	private async Task<bool> SubjectAndSourceExist(long tenantId, long dataSourceId, DataSourceGrantSubjectType type, long subjectId, CancellationToken ct)
	{
		var sourceExists = await _db.DataSources.IgnoreQueryFilters().AnyAsync(x => x.Id == dataSourceId && x.TenantId == tenantId, ct);
		if (!sourceExists) return false;
		return type == DataSourceGrantSubjectType.User
			? await _db.Users.IgnoreQueryFilters().AnyAsync(x => x.Id == subjectId && x.TenantId == tenantId, ct)
			: await _db.Roles.IgnoreQueryFilters().AnyAsync(x => x.Id == subjectId && (x.TenantId == tenantId || x.TenantId == 0), ct);
	}
}
