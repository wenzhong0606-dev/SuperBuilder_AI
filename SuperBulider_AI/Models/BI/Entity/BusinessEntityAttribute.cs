using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.BI.Entity;

/// <summary>业务实体属性定义。</summary>
public class BusinessEntityAttribute : BaseEntity
{
    public long BusinessEntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? SemanticType { get; set; }
    public bool IsNullable { get; set; }
    public bool IsIdentifier { get; set; }
    public BusinessEntity? BusinessEntity { get; set; }
    public ICollection<PhysicalBinding> PhysicalBindings { get; set; } = new List<PhysicalBinding>();
}
