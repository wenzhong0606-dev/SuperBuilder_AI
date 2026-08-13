using SuperBulider_AI.Models;
using SuperBulider_AI.Models.Organization;

namespace SuperBulider_AI.Models.Metadata;


/// <summary>
/// 业务数据库连接
/// </summary>
public class DataSource : BaseEntity
{

	public long? TenantId { get; set; }


	public Tenant? Tenant { get; set; }


	/// <summary>
	/// 名称
	/// </summary>
	public string? Name { get; set; }


	/// <summary>
	/// MYSQL SQLSERVER POSTGRESQL
	/// </summary>
	public string? DbType { get; set; }


	/// <summary>
	/// 数据库连接字符串
	/// </summary>
	public string? ConnectionString { get; set; }



	public bool? Enabled { get; set; }
		= true;



	public ICollection<MetadataTable>? Tables { get; set; }

		= new List<MetadataTable>();

}