namespace SuperBuilder_AI.Models.AppBuilder;

/// <summary>应用状态常量（P8 AI App Builder）。</summary>
public static class AppStatuses
{
	/// <summary>草稿：可编辑，不对外发布。</summary>
	public const string Draft = "draft";

	/// <summary>已发布：可被访问与分享。</summary>
	public const string Published = "published";

	/// <summary>已归档：保留数据但不可编辑。</summary>
	public const string Archived = "archived";

	public static IReadOnlyList<string> Supported { get; } =
		new[] { Draft, Published, Archived };
}

/// <summary>应用 DSL 版本号常量（P8）。</summary>
public static class AppDslVersions
{
	/// <summary>首个正式版本。</summary>
	public const string V1 = "1.0";

	/// <summary>当前版本（新增 DSL 时以此为默认）。</summary>
	public const string Current = V1;

	/// <summary>受支持的版本集合（反序列化白名单）。</summary>
	public static IReadOnlyList<string> Supported { get; } = new[] { V1 };
}

/// <summary>
/// 应用持久化实体（P8 AI App Builder）。
///
/// <para>
/// <strong>持久化策略：DSL 文档整体存储</strong>（同 P6 Dashboard）。整个 <see cref="AppDsl"/>
/// 序列化为 <see cref="DslJson"/> 存于本表，而非拆成 App/Page/Component 多张关系表。理由与
/// Dashboard 一致：DSL 本质是一份<em>文档</em>，整体读写，拆表无收益且每次 DSL 扩展都要迁移。
/// 领域模型仍是完整强类型的（<see cref="AppDsl"/> 及其组件体系），类型安全由 C# 侧保障。
/// </para>
///
/// <para>高频检索字段（<see cref="Code"/> / <see cref="Name"/> / <see cref="Status"/>）做冗余列，避免列表页解析 JSON。</para>
/// </summary>
public class AppPlan : BaseEntity
{
	/// <summary>
	/// 所属租户 Id；<c>0</c> 表示全局模板（对所有租户可见）。
	/// 使用非可空 <c>long</c>，规避 P4.3 中 <c>long?</c> 参与 <c>HasQueryFilter</c> 产生提升 <c>bool?</c> 的陷阱。
	/// </summary>
	public long TenantId { get; set; }

	/// <summary>业务编码（同租户内唯一，便于 API 定位与 AI 引用）。</summary>
	public string Code { get; set; } = string.Empty;

	/// <summary>应用名称（冗余自 DSL，供列表页展示）。</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>应用描述（冗余自 DSL）。</summary>
	public string? Description { get; set; }

	/// <summary>状态，取值见 <see cref="AppStatuses"/>。</summary>
	public string Status { get; set; } = AppStatuses.Draft;

	/// <summary>DSL 版本号（冗余自 DSL，便于按版本做兼容处理）。</summary>
	public string DslVersion { get; set; } = AppDslVersions.Current;

	/// <summary>
	/// DSL 文档（JSON 序列化后的 <see cref="AppDsl"/>）。
	/// <strong>只允许结构化 DSL，绝不存裸 HTML</strong>。
	/// </summary>
	public string DslJson { get; set; } = string.Empty;

	/// <summary>主题键（冗余自 DSL，P7 主题消费）。</summary>
	public string? ThemeKey { get; set; }

	/// <summary>
	/// 发布态 DSL 快照（M7-02）：<c>Publish</c> 时从草稿 <see cref="DslJson"/> 固化而来，
	/// 只读。草稿与发布态物理隔离，编辑草稿（<see cref="DslJson"/>）不会直接覆盖线上版本。
	/// </summary>
	public string? PublishedDslJson { get; set; }

	/// <summary>当前发布态对应的版本号（<see cref="AppVersion.Version"/>）；未发布为 0。</summary>
	public int PublishedVersion { get; set; }

	/// <summary>最近一次发布时间（UTC）；未发布为 null。</summary>
	public DateTime? PublishedAt { get; set; }

	/// <summary>最近一次发布者标识；未发布为 null。</summary>
	public string? PublishedBy { get; set; }

	/// <summary>
	/// 草稿修订乐观并发令牌（M7-11 契约 §7/§10.7/§10.9）：每次 <c>PUT</c> 编辑草稿自增 1；
	/// 发布携 <c>ExpectedDraftRevision</c> 校验，不匹配即 409 <c>SB_APP_DRAFT_CHANGED</c>。
	/// 新应用初始为 1。
	/// </summary>
	public int DraftRevision { get; set; } = 1;
}
