using SuperBuilder_AI.Models;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Models.BI.Entity;

/// <summary>
/// 业务域：一组业务实体的语义分组（如"销售域"、"仓储管理"）。
///
/// P3 批次2 新增聚合根。只描述业务语义，不保存 SQL 或物理实现细节。
/// </summary>
public class BusinessDomain : BaseEntity
{
    public long TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<BusinessEntity> Entities { get; set; } = new List<BusinessEntity>();
    public ICollection<BusinessEntityDimension> Dimensions { get; set; } = new List<BusinessEntityDimension>();
}
