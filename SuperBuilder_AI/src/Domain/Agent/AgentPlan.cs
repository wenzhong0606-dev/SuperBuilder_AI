namespace SuperBuilder_AI.Models.Agent;

/// <summary>
/// Agent 计划持久化实体（P9 AI Agent / Copilot）。
///
/// <para>
/// <strong>持久化策略：DSL 文档整体存储</strong>（同 P6 Dashboard / P8 App）。
/// 整个 <see cref="AgentDsl"/> 序列化为 <see cref="DslJson"/> 存于本表，而非拆成 Tool/Step 多张关系表。
/// 理由一致：DSL 本质是一份<em>文档</em>，整体读写，拆表无收益且每次 DSL 扩展都要迁移。
/// 领域模型仍是完整强类型的（<see cref="AgentDsl"/> 及其组件体系），类型安全由 C# 侧保障。
/// </para>
///
/// <para>高频检索字段（<see cref="Code"/> / <see cref="Name"/> / <see cref="Status"/>）做冗余列，避免列表页解析 JSON。</para>
/// </summary>
public class AgentPlan : BaseEntity
{
	/// <summary>
	/// 所属租户 Id；<c>0</c> 表示全局模板（对所有租户可见）。
	/// 使用非可空 <c>long</c>，规避 P4.3 中 <c>long?</c> 参与 <c>HasQueryFilter</c> 产生提升 <c>bool?</c> 的陷阱。
	/// </summary>
	public long TenantId { get; set; }

	/// <summary>业务编码（同租户内唯一，便于 API 定位与 AI 引用）。</summary>
	public string Code { get; set; } = string.Empty;

	/// <summary>计划名称（冗余自 DSL，供列表页展示）。</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>计划描述（冗余自 DSL）。</summary>
	public string? Description { get; set; }

	/// <summary>状态，取值见 <see cref="AgentStatuses"/>。</summary>
	public string Status { get; set; } = AgentStatuses.Draft;

	/// <summary>DSL 版本号（冗余自 DSL，便于按版本做兼容处理）。</summary>
	public string DslVersion { get; set; } = AgentDslVersions.Current;

	/// <summary>
	/// DSL 文档（JSON 序列化后的 <see cref="AgentDsl"/>）。
	/// <strong>只允许结构化 DSL，绝不存裸 HTML</strong>。
	/// </summary>
	public string DslJson { get; set; } = string.Empty;
}
