using SuperBuilder_AI.Models.Metadata;
using System.Data.Common;

namespace SuperBuilder_AI.Models.Organization;

/// <summary>
/// 租户实体，表示系统中的租户信息。
/// </summary>
public class Tenant : BaseEntity
{
	/// <summary>
	/// 租户编码，唯一标识租户（用于登录或命名空间划分）。
	/// </summary>
	public string? TenantCode { get; set; }

	/// <summary>
	/// 租户名称，用于展示。
	/// </summary>
	public string? TenantName { get; set; }

	/// <summary>
	/// 是否启用该租户。
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// 该租户下关联的数据源集合。
	/// </summary>
	public ICollection<DataSource> DataSources { get; set; } = new List<DataSource>();

}