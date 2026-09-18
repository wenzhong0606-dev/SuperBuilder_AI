using SuperBuilder_AI.Models;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 业务数据库连接
/// </summary>
public class DataSource : BaseEntity
{

	public long TenantId { get; set; }


	public Tenant? Tenant { get; set; }


	/// <summary>
	/// 名称（展示用，保留原始大小写）。
	/// </summary>
	public string Name { get; set; } = string.Empty;


	/// <summary>
	/// 规范化名称（小写去空白），作为租户内唯一键。
	/// </summary>
	public string NormalizedName { get; set; } = string.Empty;


	/// <summary>
	/// MYSQL / SQLSERVER / POSTGRESQL（连接器目录稳定 code，由方言 Code 返回）。
	/// </summary>
	public string DbType { get; set; } = string.Empty;


	/// <summary>
	/// 数据库连接字符串（敏感，禁止日志记录与接口返回）。
	/// </summary>
	public string ConnectionString { get; set; } = string.Empty;


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


	/// <summary>
	/// 当前对 Ask 生效的元数据版本号（§L 版本生命周期）。
	/// 扫描写入 staging 行使用各自的 BatchVersion，激活时仅翻此指针，Ask 仅读取 == 此值的行。
	/// </summary>
	public int ActiveMetadataVersion { get; set; }


	/// <summary>
	/// 单调版本计数器，用于原子分配新批次号（§L.1）。
	/// 建扫描任务时在事务内以 UPDATE … OUTPUT 自增，不依赖任何元数据行存活，避免重号。
	/// </summary>
	public int NextMetadataVersion { get; set; } = 1;


	/// <summary>
	/// 本数据源存量向量是否已补齐 metadata_version payload（§10.4，按数据源判定）。
	/// 正常重扫不重置；仅检测到缺 payload 旧 point 或 collection 重建后才置 false 并重跑回填。
	/// </summary>
	public bool VectorsBackfilled { get; set; }


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
