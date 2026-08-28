using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.BI.Entity;

/// <summary>业务实体之间的稳定语义关系定义。</summary>
public class BusinessEntityRelationship : BaseEntity
{
    public long SourceEntityId { get; set; }
    public long TargetEntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? RelationshipType { get; set; }
    public string? Cardinality { get; set; }
    public bool IsRequired { get; set; }
    public BusinessEntity? SourceEntity { get; set; }
    public BusinessEntity? TargetEntity { get; set; }
    public ICollection<PhysicalBinding> PhysicalBindings { get; set; } = new List<PhysicalBinding>();
}
