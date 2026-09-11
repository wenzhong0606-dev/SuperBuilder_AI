using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.BI.Entity;

/// <summary>
/// 业务实体维度：业务域下的可分组/可切片维度定义（如"物料"、"仓库"、"入库日期"）。
///
/// P3 批次2 新增值对象。只描述业务语义，不绑定物理列。
/// </summary>
public class BusinessEntityDimension : BaseEntity
{
    public long TenantId { get; set; }
    public long BusinessDomainId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>M12 增量：维度表达式（可分组字段或时间粒度表达式，如 <c>date_trunc('month', created_at)</c>）。</summary>
    public string? Expression { get; set; }

    /// <summary>M12 增量：维度数据类型 / 时间粒度（如 string / date / month）。</summary>
    public string? DataType { get; set; }

    public BusinessDomain? Domain { get; set; }
}
