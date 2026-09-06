namespace SuperBuilder_AI.Models.Dashboard;

/// <summary>仪表盘状态常量（P6）。</summary>
public static class DashboardStatuses
{
	/// <summary>草稿：可编辑，不对外发布。</summary>
	public const string Draft = "draft";

	/// <summary>已发布：可被渲染与分享。</summary>
	public const string Published = "published";

	/// <summary>已归档：保留数据但不可编辑渲染。</summary>
	public const string Archived = "archived";

	public static IReadOnlyList<string> Supported { get; } =
		new[] { Draft, Published, Archived };
}

/// <summary>
/// 仪表盘持久化实体（P6 Low-code BI Engine）。
///
/// <para>
/// <strong>持久化策略：DSL 文档整体存储</strong>。整个 <see cref="DashboardDsl"/> 序列化为
/// <see cref="DslJson"/> 存于本表，而非拆成 Dashboard/Page/Widget 多张关系表。理由：
/// <list type="bullet">
/// <item>DSL 本质是一份<em>文档</em>：组件结构随版本演进，拆表会导致每次 DSL 扩展都要迁移。</item>
/// <item>读写模式是<strong>整体读写</strong>（编辑器保存整份、渲染器加载整份），
///       拆表带来的"按组件查询"能力在本阶段并无需求。</item>
/// <item>与 Metabase / Superset 等主流 BI 的做法一致。</item>
/// </list>
/// 领域模型仍是完整强类型的（<see cref="DashboardDsl"/> 及其组件体系），
/// 类型安全由 C# 侧保障，序列化与校验由 P6.2 的 <c>DashboardDslSerializer</c> 负责。
/// </para>
///
/// <para>
/// 高频检索字段（<see cref="Code"/> / <see cref="Title"/> / <see cref="Status"/>）做<strong>冗余列</strong>，
/// 避免为了列表页去解析 JSON。
/// </para>
/// </summary>
public class Dashboard : BaseEntity
{
	/// <summary>
	/// 所属租户 Id；<c>0</c> 表示全局模板（对所有租户可见）。
	/// 使用非可空 <c>long</c>，规避 P4.3 中 <c>long?</c> 参与 <c>HasQueryFilter</c>
	/// 产生提升 <c>bool?</c> 的陷阱。
	/// </summary>
	public long TenantId { get; set; }

	/// <summary>业务编码（同租户内唯一，便于 API 定位与 AI 引用）。</summary>
	public string Code { get; set; } = string.Empty;

	/// <summary>仪表盘标题（冗余自 DSL，供列表页展示）。</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>仪表盘描述（冗余自 DSL）。</summary>
	public string? Description { get; set; }

	/// <summary>状态，取值见 <see cref="DashboardStatuses"/>。</summary>
	public string Status { get; set; } = DashboardStatuses.Draft;

	/// <summary>DSL 版本号（冗余自 DSL，便于按版本做兼容处理）。</summary>
	public string DslVersion { get; set; } = DslVersions.Current;

	/// <summary>
	/// DSL 文档（JSON 序列化后的 <see cref="DashboardDsl"/>）。
	/// <strong>只允许结构化 DSL，绝不存裸 HTML</strong>。
	/// </summary>
	public string DslJson { get; set; } = string.Empty;

	/// <summary>主题键（冗余自 DSL，P7 消费）。</summary>
	public string? ThemeKey { get; set; }

	/// <summary>
	/// 已发布的 DSL 快照（JSON）。为 null 表示从未发布（仅草稿）。
	/// 与 <see cref="DslJson"/>（草稿工作副本）物理隔离：编辑草稿不影响线上渲染，
	/// 只有 <c>Publish</c> 才把草稿复制到此处。
	/// </summary>
	public string? PublishedDslJson { get; set; }

	/// <summary>
	/// 当前发布版本号；<c>0</c> 表示从未发布。每次 <c>Publish</c> 或 <c>Rollback</c> 自增 1，
	/// 与 <see cref="DashboardVersion.Version"/> 对应，用于前端展示「当前 vN」与回滚定位。
	/// </summary>
	public int PublishedVersion { get; set; }

	/// <summary>最近一次发布时间（UTC）；未发布为 null。</summary>
	public DateTime? PublishedAt { get; set; }

	/// <summary>最近一次发布者标识；未发布为 null。</summary>
	public string? PublishedBy { get; set; }

	// 刻意不建立指向 Tenant 的外键与导航属性：
	// TenantId == 0 表示"全局模板"，而 Tenant 表中并无 Id=0 的行，加外键会在写入全局模板时
	// 直接触发 FOREIGN KEY 约束失败。故与 P5.2 的 SemanticLabel 保持一致——
	// TenantId 仅作为作用域列（配合索引与全局查询过滤），引用完整性由应用层保证。
}
