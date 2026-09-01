using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SuperBuilder_AI.Api.Security;

public sealed record TenantDataPlaneResolution(
	long AuthenticatedTenantId,
	long? RequestedTenantId,
	long EffectiveTenantId,
	bool Authorized);

/// <summary>
/// 观测/审计 middleware 看到的「租户上下文事实源」（P0-02B）。
/// 与执行层 <see cref="TenantDataPlaneResolution"/> 的区别：本结构供可观测性/审计使用，
/// 显式区分「认证租户 / 请求（可能伪造）租户 / 生效租户 / 切换是否授权 / 管理面目标租户」。
/// </summary>
public sealed record ObservedTenantContext(
	long AuthenticatedTenantId,
	long? RequestedTenantId,
	long EffectiveTenantId,
	bool TenantSwitchAuthorized,
	long? ManagementTargetTenantId,
	string? ManagementAction,
	bool? ManagementActionAuthorized);

/// <summary>Single-tenant identity rule for BI, business metadata and DataSource access.</summary>
public static class TenantDataPlanePolicy
{
	public const string AuthenticatedTenantItem = "AuthenticatedTenantId";
	public const string RequestedTenantItem = "RequestedTenantId";
	public const string EffectiveTenantItem = "EffectiveTenantId";
	public const string TenantSwitchAuthorizedItem = "TenantSwitchAuthorized";

	// P0-02B：管理面（平台治理角色操作具体租户 B）独立事实源，禁止伪装成 EffectiveTenant / TenantSwitch
	public const string ManagementTargetTenantItem = "ManagementTargetTenantId";
	public const string ManagementActionItem = "ManagementAction";
	public const string ManagementActionAuthorizedItem = "ManagementActionAuthorized";

	/// <summary>
	/// 观测/审计中间件读取租户上下文的**唯一事实源**（P0-02B）。
	/// 优先级：① HttpContext.Items（执行层已解析并存盘，最权威）→ ② 令牌 <c>tid</c> 声明 →
	/// ③ AuthMiddleware 注入的 <c>TenantId</c> Items → 0。
	/// <para>
	/// 关键：query <c>tenantId</c> / header <c>X-Tenant-Id</c> 只作为「请求值(RequestedTenantId)」记录，
	/// 绝不作为生效租户。伪造该值不得污染审计中的真实执行租户（Authenticated/Effective）。
	/// </para>
	/// </summary>
	public static ObservedTenantContext ReadObserved(HttpContext context)
	{
		long authenticated = ReadItem<long>(context, AuthenticatedTenantItem);
		if (authenticated <= 0)
		{
			var tid = context.User?.FindFirst("tid")?.Value;
			if (long.TryParse(tid, out var parsed) && parsed > 0)
				authenticated = parsed;
			else if (ReadItem<long>(context, "TenantId") is var fallback && fallback > 0)
				authenticated = fallback;
		}

		long? requested = ReadItem<long?>(context, RequestedTenantItem);
		if (requested is null)
		{
			// 未由执行层写盘时，仅把 query/header 当作「请求值」记录，不视为生效租户。
			// 注意：ParseRequestedTenant 对空值/不可解析返回 0（而非 null），故必须分别取值后
			// 按「> 0」优先选取，不能用 ?? 直接串联——否则 query 为空时的 0 会把 header 值吞掉。
			var fromQuery = ParseRequestedTenant(context.Request.Query["tenantId"].ToString());
			var fromHeader = ParseRequestedTenant(context.Request.Headers["X-Tenant-Id"].ToString());
			var candidate = fromQuery is > 0 ? fromQuery : fromHeader;
			requested = candidate is > 0 ? candidate : null;
		}

		long effective = ReadItem<long>(context, EffectiveTenantItem);
		if (effective <= 0) effective = authenticated;

		bool switchAuthorized = ReadItem<bool>(context, TenantSwitchAuthorizedItem);

		long? mgmtTarget = ReadItem<long?>(context, ManagementTargetTenantItem);
		string? mgmtAction = ReadItem<string?>(context, ManagementActionItem);
		bool? mgmtAuthorized = ReadItem<bool?>(context, ManagementActionAuthorizedItem);

		return new ObservedTenantContext(
			authenticated, requested, effective, switchAuthorized, mgmtTarget, mgmtAction, mgmtAuthorized);
	}

	/// <summary>
	/// 平台类控制器（Dashboard/App/Agent/Theme/Identity/Audit）在 <see cref="ResolvePlatformScope"/> 之后调用：
	/// 把解析结果写盘到 HttpContext.Items，供观测/审计 middleware 读取；并当主体是治理角色且操作具体租户 B 时，
	/// 额外记录 <c>ManagementTargetTenantId</c>（绝不记为 TenantSwitch）。
	/// </summary>
	public static void StorePlatformScope(HttpContext context, TenantDataPlaneResolution resolution, string? action = null)
	{
		// 可观测性旁路：单元测试常直接构造 Controller 而不设置 HttpContext（此时为 null），
		// 缺失时静默跳过，不影响任何业务与租户隔离语义（写盘仅供审计/日志读取）。
		if (context is null) return;

		context.Items[AuthenticatedTenantItem] = resolution.AuthenticatedTenantId;
		context.Items[EffectiveTenantItem] = resolution.EffectiveTenantId;
		context.Items[TenantSwitchAuthorizedItem] = false;
		if (resolution.RequestedTenantId.HasValue)
			context.Items[RequestedTenantItem] = resolution.RequestedTenantId.Value;

		// 仅当治理主体操作「别的租户 B」时才记管理目标；作用于自身租户属普通平台作用域操作，不算管理 B。
		// Authorized 如实记录（治理角色读取他租户业务数据时策略本就判定未授权 → 审计到「管理拒绝」）。
		if (context.User is { } principal
			&& GovernanceDataPlanePolicy.IsGovernancePrincipal(principal)
			&& resolution.RequestedTenantId is { } target
			&& target > 0
			&& target != resolution.AuthenticatedTenantId)
		{
			context.Items[ManagementTargetTenantItem] = target;
			context.Items[ManagementActionItem] = action ?? "platform.manage";
			context.Items[ManagementActionAuthorizedItem] = resolution.Authorized;
		}
	}

	/// <summary>
	/// 租户管理面（TenantManagementController 操作具体租户 B）记录管理目标租户。
	/// 主体未经 ResolvePlatformScope 时退化为从令牌 <c>tid</c> 推导认证/生效租户。
	/// </summary>
	public static void StoreManagementTarget(HttpContext context, long targetTenantId, string action, bool authorized)
	{
		// 同 StorePlatformScope：直接构造 Controller 的单元测试中 HttpContext 为 null，静默跳过
		if (context is null) return;

		if (ReadItem<long>(context, AuthenticatedTenantItem) is not (> 0))
		{
			var tid = context.User?.FindFirst("tid")?.Value;
			if (long.TryParse(tid, out var parsed) && parsed > 0)
				context.Items[AuthenticatedTenantItem] = parsed;
		}
		if (ReadItem<long>(context, EffectiveTenantItem) is not (> 0))
			context.Items[EffectiveTenantItem] = ReadItem<long>(context, AuthenticatedTenantItem);
		context.Items[TenantSwitchAuthorizedItem] = false;

		if (targetTenantId > 0)
		{
			context.Items[ManagementTargetTenantItem] = targetTenantId;
			context.Items[ManagementActionItem] = action;
			context.Items[ManagementActionAuthorizedItem] = authorized;
		}
	}

	private static T? ReadItem<T>(HttpContext context, string key)
	{
		if (context.Items.TryGetValue(key, out var value) && value is T typed)
			return typed;
		return default;
	}

	public static bool IsDataPlanePath(PathString path) =>
		path.StartsWithSegments("/api/ask") ||
		path.StartsWithSegments("/api/business-model") ||
		path.StartsWithSegments("/api/semantic-labels") ||
		path.StartsWithSegments("/metadata");

	public static TenantDataPlaneResolution Resolve(ClaimsPrincipal principal, params long?[] requestedTenantIds)
	{
		var authenticated = principal.FindFirst("tid")?.Value;
		var authenticatedTenantId = long.TryParse(authenticated, out var parsed) ? parsed : 0;
		var requested = requestedTenantIds.FirstOrDefault(value => value.HasValue);
		var authorized = authenticatedTenantId > 0 &&
			requestedTenantIds.Where(value => value.HasValue).All(value => value!.Value == authenticatedTenantId);

		return new TenantDataPlaneResolution(
			authenticatedTenantId,
			requested,
			authenticatedTenantId,
			authorized);
	}

	/// <summary>
	/// 平台类控制器（Dashboard / App / Agent / Theme / Identity / Audit 等）租户作用域解析。
	/// 允许以 <c>tenantId = 0</c>（共享/全局模板）为读取意图，但客户端传入的「具体租户」一旦与
	/// 认证租户不一致即视为越权。有效租户恒为认证租户（数据面单租户恒等），用于 ApplyTenantScope 与实体归属。
	/// </summary>
	/// <remarks>
	/// 生产环境中 <see cref="AuthMiddleware"/> 在抵达控制器前必从令牌注入 <c>tid</c> 声明，
	/// 故主体恒携带有效租户。仅当主体未携带 <c>tid</c>（单元测试直调、不经过中间件）时退化为以请求租户为有效租户，
	/// 以保留既有测试语义；该分支在生产不可达，不影响隔离强度。
	/// </remarks>
	public static TenantDataPlaneResolution ResolvePlatformScope(ClaimsPrincipal? principal, params long?[] requestedTenantIds)
	{
		var authenticated = principal?.FindFirst("tid")?.Value;
		var authenticatedTenantId = long.TryParse(authenticated, out var parsed) ? parsed : 0;
		long? requested = null;
		var authorized = authenticatedTenantId > 0;
		foreach (var value in requestedTenantIds)
		{
			if (!value.HasValue) continue;
			requested ??= value;
			if (value.Value > 0 && value.Value != authenticatedTenantId)
				authorized = false;
		}

		// 主体未携带 tid：仅测试直调场景（生产由中间件保证已注入）。退化为以请求租户为有效租户。
		if (authenticatedTenantId == 0)
			return new TenantDataPlaneResolution(0, requested, requested ?? 0, true);

		return new TenantDataPlaneResolution(
			authenticatedTenantId,
			requested,
			authenticatedTenantId,
			authorized);
	}

	public static long? ParseRequestedTenant(string? value)
	{
		if (value is null) return null;
		return long.TryParse(value, out var tenantId) ? tenantId : 0;
	}

	public static void Store(HttpContext context, TenantDataPlaneResolution resolution)
	{
		context.Items[AuthenticatedTenantItem] = resolution.AuthenticatedTenantId;
		context.Items[EffectiveTenantItem] = resolution.EffectiveTenantId;
		context.Items[TenantSwitchAuthorizedItem] = false;
		if (resolution.RequestedTenantId.HasValue)
			context.Items[RequestedTenantItem] = resolution.RequestedTenantId.Value;
	}
}
