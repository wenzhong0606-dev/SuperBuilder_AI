using SuperBuilder_AI.Models;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Models.BI.Entity;

/// <summary>
/// 业务实体：面向业务语义的稳定实体定义，不保存 SQL 或物理实现细节。
/// </summary>
public class BusinessEntity : BaseEntity
{
    public long TenantId { get; set; }
    public string BusinessKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? BusinessDomain { get; set; }
    public string? SemanticText { get; set; }
    public string Status { get; set; } = "Active";

    public Tenant? Tenant { get; set; }
    public ICollection<BusinessEntityKey> Keys { get; set; } = new List<BusinessEntityKey>();
    public ICollection<BusinessEntityAttribute> Attributes { get; set; } = new List<BusinessEntityAttribute>();
    public ICollection<BusinessEntityMetric> Metrics { get; set; } = new List<BusinessEntityMetric>();
    public ICollection<BusinessEntityRelationship> SourceRelationships { get; set; } = new List<BusinessEntityRelationship>();
    public ICollection<BusinessEntityRelationship> TargetRelationships { get; set; } = new List<BusinessEntityRelationship>();

    public long? BusinessDomainId { get; set; }
    public BusinessDomain? Domain { get; set; }
}
