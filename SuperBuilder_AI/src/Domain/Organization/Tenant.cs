using System;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Models.Organization;

/// <summary>
/// 租户实体，表示系统中的租户信息。
/// </summary>
public class Tenant : BaseEntity
{
	/// <summary>租户编码最大长度（M1-02 字段完整性）。</summary>
	public const int MaxCodeLength = 64;
	/// <summary>租户名称最大长度（M1-02 字段完整性）。</summary>
	public const int MaxNameLength = 128;
	/// <summary>停用原因最大长度（M1-02）。</summary>
	public const int MaxDisabledReasonLength = 512;

	/// <summary>
	/// 租户编码，唯一标识租户（用于登录或命名空间划分）。
	/// 规范化为小写并去除首尾空白后存储；创建后默认不可变更。
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

	/// <summary>停用原因（M1-02：停用治理）。</summary>
	public string? DisabledReason { get; set; }

	/// <summary>停用时间（UTC，M1-02）。</summary>
	public DateTime? DisabledAt { get; set; }

	/// <summary>执行停用的操作者用户 Id（M1-02）。</summary>
	public long? DisabledByUserId { get; set; }

	/// <summary>
	/// 该租户下关联的数据源集合。
	/// </summary>
	public ICollection<DataSource> DataSources { get; set; } = new List<DataSource>();

	/// <summary>
	/// 规范化租户编码：去首尾空白并转小写，用于唯一性与查找一致性。
	/// </summary>
	public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToLowerInvariant();
}