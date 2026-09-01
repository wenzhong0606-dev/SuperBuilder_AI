using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Interfaces.Identity;

public interface IDataSourceAuthorizationService
{
	Task<IReadOnlyList<long>> GetAuthorizedDataSourceIdsAsync(long tenantId, long userId, CancellationToken ct = default);
	Task<bool> IsAuthorizedAsync(long tenantId, long userId, long dataSourceId, CancellationToken ct = default);
	Task GrantAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default);
	Task RevokeAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default);
}

public sealed record DataSourceExecutionIdentity(
	long TenantId,
	long UserId,
	IReadOnlyDictionary<string, string>? Attributes = null);

public interface IDataSourceExecutionIdentityAccessor
{
	DataSourceExecutionIdentity? Current { get; set; }
}
