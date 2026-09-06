namespace SuperBuilder_AI.Models.Dashboard;

/// <summary>
/// 仪表盘发布版本快照（M7-01 草稿/发布隔离 + 可追踪回滚）。
///
/// <para>
/// 每次 <c>Publish</c> 都会把当前草稿 DSL 固化为一条不可变快照；<c>Rollback</c> 把某条历史
/// 快照恢复为「当前发布态」并再固化为一条新版本（<see cref="RolledBackFromVersion"/> 记录其来源，
/// 保证全程可审计、可追溯）。草稿（<see cref="Dashboard.DslJson"/>）与发布态
/// （<see cref="Dashboard.PublishedDslJson"/>）物理隔离，编辑草稿不影响线上渲染。
/// </para>
///
/// <para>与 <see cref="Dashboard"/> 一致：<see cref="TenantId"/> 仅作作用域列，不建指向 Tenant 的外键
/// （TenantId=0 表示全局模板，Tenant 表无 Id=0 行）。</para>
/// </summary>
public class DashboardVersion : BaseEntity
{
	/// <summary>所属仪表盘 Id（指向 <see cref="Dashboard.Id"/>，删除仪表盘级联删除其全部版本）。</summary>
	public long DashboardId { get; set; }

	/// <summary>作用域租户 Id（冗余自仪表盘，便于按租户隔离列出版本）。</summary>
	public long TenantId { get; set; }

	/// <summary>
	/// 本次快照对应的发布版本号（从 1 自增）；同一仪表盘内唯一。
	/// 回滚产生的新版本沿用全局自增序号，不会复用被回滚版本的号。
	/// </summary>
	public int Version { get; set; }

	/// <summary>业务编码快照（冗余自发布时刻的仪表盘）。</summary>
	public string Code { get; set; } = string.Empty;

	/// <summary>标题快照。</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>描述快照。</summary>
	public string? Description { get; set; }

	/// <summary>主题键快照。</summary>
	public string? ThemeKey { get; set; }

	/// <summary>DSL 版本号快照（冗余自 DSL，便于按版本做兼容处理）。</summary>
	public string DslVersion { get; set; } = DslVersions.Current;

	/// <summary>发布时刻固化的 DSL 文档（JSON 序列化后的 <see cref="DashboardDsl"/>），只读快照。</summary>
	public string DslJson { get; set; } = string.Empty;

	/// <summary>发布时间（UTC）。</summary>
	public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

	/// <summary>发布者标识（发起 Publish/Rollback 的用户或系统）。</summary>
	public string? PublishedBy { get; set; }

	/// <summary>
	/// 若该版本由回滚产生，记录被恢复的历史版本号；普通发布为 null。
	/// 用于审计「当前发布态来自哪次历史版本」。
	/// </summary>
	public int? RolledBackFromVersion { get; set; }
}
