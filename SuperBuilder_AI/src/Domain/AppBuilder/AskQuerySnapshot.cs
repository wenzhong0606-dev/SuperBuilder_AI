namespace SuperBuilder_AI.Models.AppBuilder;

/// <summary>
/// M7-11：Ask 成功查询的快照。
///
/// <para>
/// 在 <see cref="SuperBuilder_AI.Services.BI.BIConversationService"/> 执行链路中，
/// <see cref="QueryPlan"/> 于 RLS 注入<strong>之前</strong>深拷贝其允许查询的语义
/// （数据源、实体、字段、指标、维度、筛选、排序、Limit、聚合方式、查询模式），
/// 仅当实际查询成功后才落库；拒绝 / 执行失败 / Decision Gate 阻断均不产生快照。
/// </para>
///
/// <para>
/// 用途：作为"Ask 真实结果 → 可运行应用"的服务端查询上下文引用。
/// <c>turnId</c> 即稳定引用标识，由 api/ask 返回；<c>from-ask</c> 凭其反查并导出
/// <see cref="AppDataSourceBinding"/>。快照仅创建者本人可读（跨用户/跨租户访问即 403）。
/// 不固化发布者的 RLS 条件、身份参数或敏感结果摘要。
/// </para>
/// </summary>
public sealed class AskQuerySnapshot
{
	/// <summary>服务端查询引用标识（PK，GUID 字符串）。由 api/ask 返回。</summary>
	public string TurnId { get; set; } = string.Empty;

	/// <summary>所属租户。</summary>
	public long TenantId { get; set; }

	/// <summary>快照创建者用户 Id（仅其本人可凭 turnId 生成为应用）。</summary>
	public long UserId { get; set; }

	/// <summary>查询解析出的数据源 Id（运行时硬约束）。</summary>
	public long DataSourceId { get; set; }

	/// <summary>主表业务实体语义名（如 sales_order）。</summary>
	public string? EntityCode { get; set; }

	/// <summary>
	/// 允许查询的 QueryPlan 语义（JSON）。在 RLS 注入前截取，不含发布者行级条件。
	/// 由运行时重新应用当前访问者策略。
	/// </summary>
	public string QueryPlanJson { get; set; } = string.Empty;

	/// <summary>
	/// 请求摘要哈希（tenantId + turnId 来源查询的稳定指纹），用于创建同键不同内容冲突检测（409）。
	/// </summary>
	public string RequestHash { get; set; } = string.Empty;

	/// <summary>过期时间（UTC）；默认创建后 24 小时。过期后 turnId 不可再用。</summary>
	public DateTime ExpiresAt { get; set; }

	/// <summary>创建时间（UTC）。</summary>
	public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
}
