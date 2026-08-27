using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.BI.Entity;

/// <summary>业务语义到现有物理 Metadata 的映射。</summary>
public class PhysicalBinding : BaseEntity
{
    public long DataSourceId { get; set; }
    public long MetadataTableId { get; set; }
    public long MetadataColumnId { get; set; }
    public string? PhysicalRole { get; set; }
    public string? BindingType { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;

    // 一个 Binding 只属于一种 Entity Semantic Owner；具体 one-of 约束由 EF Core Check Constraint 强制。
    public long? BusinessEntityKeyId { get; set; }
    public long? BusinessEntityAttributeId { get; set; }
    public long? BusinessEntityMetricId { get; set; }
    public long? BusinessEntityRelationshipId { get; set; }

    public BusinessEntityKey? BusinessEntityKey { get; set; }
    public BusinessEntityAttribute? BusinessEntityAttribute { get; set; }
    public BusinessEntityMetric? BusinessEntityMetric { get; set; }
    public BusinessEntityRelationship? BusinessEntityRelationship { get; set; }
}
