using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.BI.Entity;

/// <summary>业务实体的稳定业务键定义。</summary>
public class BusinessEntityKey : BaseEntity
{
    public long BusinessEntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool IsPrimary { get; set; }
    public string? KeyType { get; set; }
    public BusinessEntity? BusinessEntity { get; set; }
    public ICollection<PhysicalBinding> PhysicalBindings { get; set; } = new List<PhysicalBinding>();
}
