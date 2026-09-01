namespace SuperBuilder_AI.Models.Identity;

public enum RowPolicySubjectType { Everyone = 0, User = 1, Role = 2, Attribute = 3 }
public enum RowPolicyEffect { Allow = 1, Deny = 2 }

/// <summary>Tenant-owned mandatory row filter bound to one physical metadata column.</summary>
public sealed class RowLevelSecurityPolicy
{
	public long Id { get; set; }
	public long TenantId { get; set; }
	public long DataSourceId { get; set; }
	public long MetadataTableId { get; set; }
	public long MetadataColumnId { get; set; }
	public RowPolicySubjectType SubjectType { get; set; }
	public long? SubjectId { get; set; }
	public string? SubjectKey { get; set; }
	public string? SubjectValue { get; set; }
	public RowPolicyEffect Effect { get; set; } = RowPolicyEffect.Allow;
	public string Operator { get; set; } = "=";
	public string Value { get; set; } = string.Empty;
	public bool Enabled { get; set; } = true;
	public long Version { get; set; } = 1;
	public DateTime UpdatedTime { get; set; } = DateTime.UtcNow;
}
