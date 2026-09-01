using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Middleware;
using SuperBuilder_AI.Models.Audit;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// SB-P0-02B 回归：Observability / Audit 不再以 query <c>tenantId</c> 或 header <c>X-Tenant-Id</c>
/// 作为「实际租户」的事实源。数据面记录 Authenticated / Requested / Effective / TenantSwitchAuthorized；
/// 治理角色管理具体租户 B 时另记 ManagementTargetTenantId，且不记为 TenantSwitch。
/// </summary>
public class TenantContextAuditTests
{
	private sealed class FakeAudit : IAuditLogService
	{
		public AuditLogEntry? Captured;

		public Task<long> LogAsync(AuditLogEntry entry, CancellationToken ct = default)
		{
			Captured = entry;
			return Task.FromResult(1L);
		}

		public Task<IReadOnlyList<AuditLog>> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
	}

	private sealed class CapturingLogger<T> : ILogger<T>
	{
		public readonly List<string> Messages = new();

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
			=> Messages.Add(formatter(state, exception));
	}

	private static ClaimsPrincipal Principal(long tenantId, long userId = 1, params string[] perms)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, userId.ToString()),
			new(ClaimTypes.Name, "tester"),
			new("tid", tenantId.ToString()),
		};
		foreach (var p in perms) claims.Add(new Claim("perm", p));
		return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
	}

	private static long? JsonLong(string? json, string prop)
	{
		using var doc = JsonDocument.Parse(json!);
		return doc.RootElement.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
			? v.GetInt64()
			: null;
	}

	private static bool? JsonBool(string? json, string prop)
	{
		using var doc = JsonDocument.Parse(json!);
		return doc.RootElement.TryGetProperty(prop, out var v) &&
			   (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
			? v.GetBoolean()
			: null;
	}

	private static string? JsonString(string? json, string prop)
	{
		using var doc = JsonDocument.Parse(json!);
		return doc.RootElement.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
			? v.GetString()
			: null;
	}

	/// <summary>① 伪造 header 不得污染审计中的实际执行租户，只作为「请求值」记录。</summary>
	[Fact]
	public async Task Audit_ForgedHeader_DoesNotBecomeEffectiveTenant()
	{
		var audit = new FakeAudit();
		var mw = new AuditMiddleware(_ => Task.CompletedTask);

		var ctx = new DefaultHttpContext { Request = { Path = "/api/ask" } };
		ctx.User = Principal(7);
		ctx.Request.Headers["X-Tenant-Id"] = "9";
		ctx.Response.StatusCode = StatusCodes.Status200OK;

		await mw.InvokeAsync(ctx, audit);

		var e = Assert.IsType<AuditLogEntry>(audit.Captured);
		// 审计行的租户 = 真实操作者租户（认证租户 7），而非伪造的 9
		Assert.Equal(7, e.TenantId);
		Assert.Equal(1, e.UserId);
		Assert.Equal(7, JsonLong(e.AfterJson, "authenticatedTenantId"));
		Assert.Equal(9, JsonLong(e.AfterJson, "requestedTenantId"));
		Assert.Equal(7, JsonLong(e.AfterJson, "effectiveTenantId"));
		Assert.False(JsonBool(e.AfterJson, "tenantSwitchAuthorized"));
	}

	/// <summary>② 数据面越权意图与结果入账：跨租户请求被拒时，审计仍记录完整双上下文。</summary>
	[Fact]
	public async Task Audit_DataPlaneCrossTenantAttempt_RecordsIntentAndResult()
	{
		var audit = new FakeAudit();
		var mw = new AuditMiddleware(_ => Task.CompletedTask);

		var ctx = new DefaultHttpContext { Request = { Path = "/api/ask" } };
		ctx.User = Principal(7);
		ctx.Request.Headers["X-Tenant-Id"] = "9";

		// 模拟 AuthMiddleware 在数据面写盘的解析结果（跨租户 → 未授权）
		var resolution = TenantDataPlanePolicy.Resolve(ctx.User, 9);
		Assert.False(resolution.Authorized);
		TenantDataPlanePolicy.Store(ctx, resolution);
		ctx.Response.StatusCode = StatusCodes.Status403Forbidden;

		await mw.InvokeAsync(ctx, audit);

		var e = Assert.IsType<AuditLogEntry>(audit.Captured);
		Assert.Equal(7, e.TenantId);
		Assert.Equal("failure", e.Result);
		Assert.Equal(7, JsonLong(e.AfterJson, "authenticatedTenantId"));
		Assert.Equal(9, JsonLong(e.AfterJson, "requestedTenantId"));
		Assert.Equal(7, JsonLong(e.AfterJson, "effectiveTenantId"));
		Assert.False(JsonBool(e.AfterJson, "tenantSwitchAuthorized"));
	}

	/// <summary>
	/// ③ 治理角色管理具体租户 B：同时记录平台租户身份与目标 B，但不记录为 TenantSwitch。
	/// </summary>
	[Fact]
	public async Task Audit_ManagementTarget_RecordedWithoutTenantSwitch()
	{
		var audit = new FakeAudit();
		var mw = new AuditMiddleware(_ => Task.CompletedTask);

		// 平台租户 tid=1 的治理用户，对租户 42 执行启用
		var ctx = new DefaultHttpContext { Request = { Path = "/api/tenant-management/42/enable" } };
		ctx.User = Principal(1, 5, "platform:tenant:manage");

		TenantDataPlanePolicy.StoreManagementTarget(ctx, 42, "tenant.enable", true);
		ctx.Response.StatusCode = StatusCodes.Status200OK;

		await mw.InvokeAsync(ctx, audit);

		var e = Assert.IsType<AuditLogEntry>(audit.Captured);
		// 操作者身份仍是平台租户 1
		Assert.Equal(1, e.TenantId);
		Assert.Equal(1, JsonLong(e.AfterJson, "authenticatedTenantId"));
		// 生效租户未被改成管理目标
		Assert.Equal(1, JsonLong(e.AfterJson, "effectiveTenantId"));
		// 管理目标 B 单独记录
		Assert.Equal(42, JsonLong(e.AfterJson, "managementTargetTenantId"));
		Assert.Equal("tenant.enable", JsonString(e.AfterJson, "managementAction"));
		Assert.True(JsonBool(e.AfterJson, "managementActionAuthorized"));
		// 关键：不记为 TenantSwitch
		Assert.False(JsonBool(e.AfterJson, "tenantSwitchAuthorized"));
	}

	/// <summary>
	/// ④ 治理角色经平台控制器越界操作他租户业务数据（如读取租户 42 的仪表盘）时：
	/// 策略判定未授权（约束 4：治理身份不得读取租户业务数据），但管理目标与拒绝结果仍结构化入账。
	/// </summary>
	[Fact]
	public async Task Audit_GovernanceOutOfScope_RecordsManagementTargetAndDenial()
	{
		var audit = new FakeAudit();
		var mw = new AuditMiddleware(_ => Task.CompletedTask);

		var ctx = new DefaultHttpContext { Request = { Path = "/api/dashboards" } };
		ctx.User = Principal(1, 5, "platform:tenant:manage");

		var resolution = TenantDataPlanePolicy.ResolvePlatformScope(ctx.User, 42);
		// 治理身份读取他租户业务数据 → 未授权（由控制器抛 403）
		Assert.False(resolution.Authorized);
		TenantDataPlanePolicy.StorePlatformScope(ctx, resolution, "Dashboard");
		ctx.Response.StatusCode = StatusCodes.Status403Forbidden;

		await mw.InvokeAsync(ctx, audit);

		var e = Assert.IsType<AuditLogEntry>(audit.Captured);
		Assert.Equal(1, e.TenantId);
		// 目标租户 42 与动作入账，且授权状态如实记录为 false（管理拒绝也有审计）
		Assert.Equal(42, JsonLong(e.AfterJson, "managementTargetTenantId"));
		Assert.Equal("Dashboard", JsonString(e.AfterJson, "managementAction"));
		Assert.False(JsonBool(e.AfterJson, "managementActionAuthorized"));
		// 拒绝不是租户切换
		Assert.False(JsonBool(e.AfterJson, "tenantSwitchAuthorized"));
		Assert.Equal("failure", e.Result);
	}

	/// <summary>治理角色操作自身租户（非「管理 B」）时不产生管理目标记录。</summary>
	[Fact]
	public async Task Audit_GovernanceOwnTenant_HasNoManagementTarget()
	{
		var audit = new FakeAudit();
		var mw = new AuditMiddleware(_ => Task.CompletedTask);

		var ctx = new DefaultHttpContext { Request = { Path = "/api/quota" } };
		ctx.User = Principal(1, 5, "platform:quota:view");

		var resolution = TenantDataPlanePolicy.ResolvePlatformScope(ctx.User, 1);
		TenantDataPlanePolicy.StorePlatformScope(ctx, resolution, "Quota");
		ctx.Response.StatusCode = StatusCodes.Status200OK;

		await mw.InvokeAsync(ctx, audit);

		var e = Assert.IsType<AuditLogEntry>(audit.Captured);
		Assert.Equal(1, e.TenantId);
		Assert.Null(JsonLong(e.AfterJson, "managementTargetTenantId"));
	}

	/// <summary>普通（非治理）租户请求自身租户时，不应产生管理目标记录。</summary>
	[Fact]
	public async Task Audit_RegularTenantOwnScope_HasNoManagementTarget()
	{
		var audit = new FakeAudit();
		var mw = new AuditMiddleware(_ => Task.CompletedTask);

		var ctx = new DefaultHttpContext { Request = { Path = "/api/dashboards" } };
		ctx.User = Principal(7);

		var resolution = TenantDataPlanePolicy.ResolvePlatformScope(ctx.User, 7);
		TenantDataPlanePolicy.StorePlatformScope(ctx, resolution, "Dashboard");
		ctx.Response.StatusCode = StatusCodes.Status200OK;

		await mw.InvokeAsync(ctx, audit);

		var e = Assert.IsType<AuditLogEntry>(audit.Captured);
		Assert.Equal(7, e.TenantId);
		Assert.Null(JsonLong(e.AfterJson, "managementTargetTenantId"));
		Assert.False(JsonBool(e.AfterJson, "tenantSwitchAuthorized"));
	}

	/// <summary>可观测性日志：生效/认证租户取自令牌，伪造 header 仅作为请求值打标。</summary>
	[Fact]
	public async Task Observability_LogsAuthoritativeTenant_NotForgedHeader()
	{
		var logger = new CapturingLogger<ObservabilityMiddleware>();
		var mw = new ObservabilityMiddleware(_ => Task.CompletedTask);

		var ctx = new DefaultHttpContext { Request = { Path = "/api/ask" } };
		ctx.User = Principal(7);
		ctx.Request.Headers["X-Tenant-Id"] = "9";

		await mw.InvokeAsync(ctx, logger);

		// 中间件同时输出 REQ（入口）与 RES（出口）两条日志，这里断言入口那条
		var msg = Assert.Single(logger.Messages, m => m.StartsWith("REQ"));
		Assert.Contains("authTenant=7", msg);
		Assert.Contains("effTenant=7", msg);
		Assert.Contains("reqTenant=9", msg);
		Assert.Contains("switchAuth=False", msg);
	}
}
