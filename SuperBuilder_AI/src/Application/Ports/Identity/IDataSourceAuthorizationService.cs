using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Interfaces.Identity;

public interface IDataSourceAuthorizationService
{
	Task<IReadOnlyList<long>> GetAuthorizedDataSourceIdsAsync(long tenantId, long userId, CancellationToken ct = default);
	Task<bool> IsAuthorizedAsync(long tenantId, long userId, long dataSourceId, CancellationToken ct = default);
	Task GrantAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default);
	Task RevokeAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default);
	/// <summary>撤销某个主体（User/Role）在租户内的全部 DataSource 授权（不限定具体 DataSource）。用于删除 User/Role 时级联清理。</summary>
	Task RevokeBySubjectAsync(long tenantId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default);
	/// <summary>巡检孤儿授权：返回引用了已删除 User/Role 或已删除 DataSource 的授权记录。</summary>
	Task<IReadOnlyList<DataSourceAccessGrant>> DetectOrphanGrantsAsync(long tenantId, CancellationToken ct = default);
}

public sealed record DataSourceExecutionIdentity(
	long TenantId,
	long UserId,
	IReadOnlyDictionary<string, string>? Attributes = null);

public interface IDataSourceExecutionIdentityAccessor
{
	DataSourceExecutionIdentity? Current { get; set; }
}
