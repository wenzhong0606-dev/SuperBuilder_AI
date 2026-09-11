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

    /// <summary>
    /// M12 增量：计算口径表达式（仅 <see cref="IsCalculated"/> 为 true 时有意义）。
    /// 以实体内其他指标名 / 维度名 / 常量与四则运算描述（如 <c>sum(amount) / count(orders)</c>）。
    /// 本系统不在此处做表达式求值，只登记口径供治理与生成层消费。
    /// </summary>
    public string? Expression { get; set; }

    /// <summary>M12 增量：结果数据类型（如 decimal / int / string / date）。</summary>
    public string? DataType { get; set; }

    public BusinessEntity? BusinessEntity { get; set; }
    public ICollection<PhysicalBinding> PhysicalBindings { get; set; } = new List<PhysicalBinding>();
}
