namespace SuperBuilder_AI.Models.Organization;

/// <summary>
/// 租户运行时上下文 —— P4 Multi-Tenant Platform Core 的第一块基石。
///
/// 把散落在各处的 <c>long tenantId</c> 收敛为带有明确语义的运行时上下文对象，
/// 作为后续平台级隔离（数据 / 元数据 / 语义 / 仪表盘 / AI Memory）的统一载体。
///
/// 增量建设：当前仅承载租户标识；User / Workspace / Locale / Theme 由
/// <see cref="PlatformContext"/> 后续扩展（P5/P7/P10 填充）。
/// </summary>
public sealed record TenantContext
{
	/// <summary>租户主键，对应 <c>Organization.Tenant.Id</c>。</summary>
	public long TenantId { get; init; }

	/// <summary>租户编码，对应 <c>Tenant.TenantCode</c>（可选，便于日志/可观测）。</summary>
	public string? TenantCode { get; init; }

	/// <summary>
	/// 是否处于明确的租户作用域内。
	/// 为 <c>false</c> 时代表"系统/全局上下文"（如 Golden 回归运行时），不应施加租户隔离。
	/// </summary>
	public bool IsScoped { get; init; }

	private TenantContext(long tenantId, string? tenantCode, bool isScoped)
	{
		TenantId = tenantId;
		TenantCode = tenantCode;
		IsScoped = isScoped;
	}

	/// <summary>构造一个明确作用域内的租户上下文。</summary>
	public static TenantContext Scoped(long tenantId, string? tenantCode = null)
		=> new(tenantId, tenantCode, true);

	/// <summary>
	/// 系统/全局上下文（无租户隔离）。
	/// Golden 回归运行时、后台作业等未与具体租户绑定的场景使用。
	/// </summary>
	public static TenantContext System { get; } = new(0, null, false);
}
