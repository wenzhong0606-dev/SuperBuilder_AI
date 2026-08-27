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

    public BusinessEntityKey? BusinessEntityKey { get; set; }
    public BusinessEntityAttribute? BusinessEntityAttribute { get; set; }
    public BusinessEntityMetric? BusinessEntityMetric { get; set; }
    public BusinessEntityRelationship? BusinessEntityRelationship { get; set; }
}
