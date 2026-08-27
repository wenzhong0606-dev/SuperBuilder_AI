using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.BI.Entity;

/// <summary>业务实体指标定义。</summary>
public class BusinessEntityMetric : BaseEntity
{
    public long BusinessEntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? SemanticType { get; set; }
    public string? Aggregation { get; set; }
    public bool IsCalculated { get; set; }
    public BusinessEntity? BusinessEntity { get; set; }
    public ICollection<PhysicalBinding> PhysicalBindings { get; set; } = new List<PhysicalBinding>();
}
