using SuperBuilder_AI.Models;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 跨数据源字典表配置：描述某数据源（通常是 PMIS）中码值字典表的结构。
/// 以【角色映射】(CodeColumn / NameColumn / TypeColumn) 表达，而非字面列名，
/// 以支持不同租户异构的字典表形态（如 code/name/type 或 key/val/group）。
/// 严格按 TenantId + DataSourceId 隔离：每个租户各自发现自己的字典结构。
/// </summary>
public class MetadataDictionaryConfig : BaseEntity
{
	/// <summary>所属租户。</summary>
	public long TenantId { get; set; }

	public Tenant? Tenant { get; set; }

	/// <summary>字典所在数据源（如 PMIS 的 DataSourceId）。可不等于查询源（WMS）。</summary>
	public long DataSourceId { get; set; }

	public DataSource? DataSource { get; set; }

	/// <summary>字典表名（在该数据源内）。</summary>
	public string TableName { get; set; } = string.Empty;

	/// <summary>角色：码值列（如 code / dict_code / key）。</summary>
	public string CodeColumn { get; set; } = string.Empty;

	/// <summary>角色：名称列（如 name / dict_name / val）。</summary>
	public string NameColumn { get; set; } = string.Empty;

	/// <summary>角色：分类列（可选，用于区分不同业务字典，如 type / category / group）。</summary>
	public string? TypeColumn { get; set; }

	/// <summary>
	/// 角色：有效行过滤列（可选，如 status / is_deleted / del_flag / enabled）。
	///
	/// <para>
	/// 为空表示不过滤。字典表普遍带软删/停用标记，不过滤会把已删除/停用的码值也译出来。
	/// 实测 PMIS 的 <c>js_sys_dict_data</c>：status 0 正常 2872 行、1 删除 32 行、2 停用 12 行。
	/// </para>
	/// </summary>
	public string? ActiveFilterColumn { get; set; }

	/// <summary>
	/// 有效行过滤值（配合 <see cref="ActiveFilterColumn"/>，默认 "0" 表示「正常」）。
	/// EM 字典表用 'N'/'Y' 时由管理员改为对应值。
	/// </summary>
	public string? ActiveFilterValue { get; set; } = "0";

	/// <summary>是否启用（扫描自动发现默认 true；管理员可关闭）。</summary>
	public bool IsEnabled { get; set; } = true;

	// 注：不使用导航属性持有被译码字段 —— MetadataColumn.DictConfigId 为软引用（无数据库外键），
	// 以避免 DataSources 到 MetadataColumns 的多重级联路径。关联查询按 Id 显式进行。
}
