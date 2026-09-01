namespace SuperBuilder_AI.Models.Identity;

public enum DataSourceGrantSubjectType
{
	User = 1,
	Role = 2
}

/// <summary>Explicit query permission from a user or role to one tenant-owned DataSource.</summary>
public sealed class DataSourceAccessGrant
{
	public long Id { get; set; }
	public long TenantId { get; set; }
	public long DataSourceId { get; set; }
	public DataSourceGrantSubjectType SubjectType { get; set; }
	public long SubjectId { get; set; }
	public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
}
