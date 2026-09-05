using SuperBuilder_AI.Models;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 业务数据库连接
/// </summary>
public class DataSource : BaseEntity
{

	public long? TenantId { get; set; }


	public Tenant? Tenant { get; set; }


	/// <summary>
	/// 名称（展示用，保留原始大小写）。
	/// </summary>
	public string? Name { get; set; }


	/// <summary>
	/// 规范化名称（小写去空白），作为租户内唯一键。
	/// </summary>
	public string? NormalizedName { get; set; }


	/// <summary>
	/// MYSQL / SQLSERVER / POSTGRESQL（连接器目录稳定 code，由方言 Code 返回）。
	/// </summary>
	public string? DbType { get; set; }


	/// <summary>
	/// 数据库连接字符串（敏感，禁止日志记录与接口返回）。
	/// </summary>
	public string? ConnectionString { get; set; }


	/// <summary>
	/// 是否启用。
	/// </summary>
	public bool Enabled { get; set; }
		= true;


	/// <summary>
	/// 最近一次连接测试状态：Ok / Failed / Unknown。
	/// </summary>
	public string? LastTestStatus { get; set; }


	/// <summary>
	/// 最近一次连接测试时间(UTC)。
	/// </summary>
	public DateTime? LastTestTime { get; set; }


	/// <summary>
	/// 最近一次连接测试错误码（仅异常类型名，脱敏，不含连接细节/凭据）。
	/// </summary>
	public string? LastErrorCode { get; set; }


	/// <summary>
	/// 最近一次元数据扫描完成时间(UTC)；null 表示从未扫描。
	/// </summary>
	public DateTime? LastScanAt { get; set; }


	public ICollection<MetadataTable>? Tables { get; set; }

		= new List<MetadataTable>();


	/// <summary>
	/// 连接器目录稳定 code（与 MySqlDialect/SqlServerDialect/PostgreSqlDialect.Code 对齐）。
	/// </summary>
	public static IReadOnlySet<string> SupportedDbTypes { get; } =
		new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MYSQL", "SQLSERVER", "POSTGRESQL" };


	/// <summary>
	/// 名称规范化：去空白 + 小写，用于租户内唯一比对。
	/// </summary>
	public static string NormalizeName(string? name) =>
		(name ?? string.Empty).Trim().ToLowerInvariant();


	/// <summary>
	/// 校验数据库类型是否在支持目录内（大小写不敏感）。
	/// </summary>
	public static bool IsSupportedDbType(string? dbType) =>
		!string.IsNullOrWhiteSpace(dbType) && SupportedDbTypes.Contains(dbType);

}
