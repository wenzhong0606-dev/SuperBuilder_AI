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

    public BusinessDomain? Domain { get; set; }
}
