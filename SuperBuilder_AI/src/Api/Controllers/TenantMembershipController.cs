using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Models.Audit;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Identity;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 多租户成员关系 API（M2-05 / SB-P1-16）。
/// <list type="bullet">
/// <item><c>GET  api/tenant-membership</c>：列出当前用户可切换的租户（需登录）。</item>
/// <item><c>POST api/tenant-membership</c>：将用户加入某可切换租户（需 <c>platform:tenant:manage</c>）。</item>
/// <item><c>DEL  api/tenant-membership?userId=&amp;tenantId=</c>：移除成员关系（需 <c>platform:tenant:manage</c>；主租户不可移除）。</item>
/// <item><c>POST api/tenant-membership/switch</c>：切换到某成员租户，校验成员资格后重签令牌（tid=目标，htid=主租户）。</item>
/// </list>
/// 用户租户切换与平台管理员代管（<see cref="TenantManagementController"/>）是两套独立机制：
/// 切换仅依据 UserTenant 成员资格，不改变任何平台治理授权范围。
/// </summary>
[ApiController]
[Route("api/tenant-membership")]
public sealed class TenantMembershipController : ControllerBase
{
	private readonly SuperBIContext _db;
	private readonly ITenantMembershipService _membership;
	private readonly IIdentityService _identity;
	private readonly ITokenService _token;
	private readonly IAuditLogService _audit;
	private readonly ITenantLanguageService _tenantLanguage;

	public TenantMembershipController(
		SuperBIContext db,
		ITenantMembershipService membership,
		IIdentityService identity,
		ITokenService token,
		IAuditLogService audit,
		ITenantLanguageService tenantLanguage)
	{
		_db = db;
		_membership = membership;
		_identity = identity;
		_token = token;
		_audit = audit;
		_tenantLanguage = tenantLanguage;
	}

	private long CallerId()
	{
		var v = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		return long.TryParse(v, out var id) ? id : 0;
	}

	private long CallerHomeTenantId()
	{
		var htid = User.FindFirst("htid")?.Value;
		if (long.TryParse(htid, out var home) && home > 0) return home;
		var tid = User.FindFirst("tid")?.Value;
		return long.TryParse(tid, out var t) ? t : 0;
	}

	[HttpGet]
	public async Task<IActionResult> ListMine(CancellationToken cancellationToken = default)
	{
		var userId = CallerId();
		if (userId <= 0) return Unauthorized(new { error = "未认证。" });
		var memberships = await _membership.GetMembershipsAsync(userId, cancellationToken);
		return Ok(memberships);
	}

	/// <summary>平台治理面：列出全部显式成员关系（需 <c>platform:tenant:manage</c>）。</summary>
	[HttpGet("admin")]
	public async Task<IActionResult> ListAll(CancellationToken cancellationToken = default)
	{
		if (!User.HasClaim("perm", IdentityPermissions.PlatformTenantManage))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 platform:tenant:manage 权限。" });
		var rows = await _membership.ListAllAsync(cancellationToken);
		return Ok(rows);
	}

	[HttpPost]
	public async Task<IActionResult> AddMember(
		[FromBody] AddTenantMemberRequest request,
		CancellationToken cancellationToken = default)
	{
		if (!User.HasClaim("perm", IdentityPermissions.PlatformTenantManage))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 platform:tenant:manage 权限。" });
		if (request is null || request.UserId <= 0 || request.TenantId <= 0)
			return BadRequest(new { error = "userId 与 tenantId 均须大于 0。" });

		try
		{
			await _membership.AddMemberAsync(request.UserId, request.TenantId, CallerId(), cancellationToken);
		}
		catch (InvalidOperationException ex)
		{
			return BadRequest(new { error = ex.Message });
		}
		return Ok(new { ok = true });
	}

	[HttpDelete]
	public async Task<IActionResult> RemoveMember(
		[FromQuery] long userId,
		[FromQuery] long tenantId,
		CancellationToken cancellationToken = default)
	{
		if (!User.HasClaim("perm", IdentityPermissions.PlatformTenantManage))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 platform:tenant:manage 权限。" });
		if (userId <= 0 || tenantId <= 0)
			return BadRequest(new { error = "userId 与 tenantId 均须大于 0。" });

		try
		{
			await _membership.RemoveMemberAsync(userId, tenantId, cancellationToken);
		}
		catch (InvalidOperationException ex)
		{
			return BadRequest(new { error = ex.Message });
		}
		return Ok(new { ok = true });
	}

	[HttpPost("switch")]
	public async Task<IActionResult> Switch(
		[FromBody] SwitchTenantRequest request,
		CancellationToken cancellationToken = default)
	{
		var callerId = CallerId();
		if (callerId <= 0) return Unauthorized(new { error = "未认证。" });
		if (request is null || request.TenantId <= 0)
			return BadRequest(new { error = "tenantId 须大于 0。" });

		var homeTenantId = CallerHomeTenantId();
		var isMember = await _membership.IsMemberAsync(callerId, request.TenantId, cancellationToken);
		if (!isMember)
		{
			// 非成员切换：跨租户越权，记审计并拒绝。
			await _audit.LogAsync(new AuditLogEntry(
				request.TenantId, "tenant.switch", "UserTenant",
				UserId: callerId, Actor: User.FindFirst(ClaimTypes.Name)?.Value ?? "",
				Result: "denied", Message: $"非成员尝试切换至租户 {request.TenantId}"), cancellationToken);
			return StatusCode(403, new ApiError
			{
				Code = ErrorCodes.TenantIsolated,
				Message = "禁止：您不是该租户的成员，无法切换。"
			});
		}

		// 加载主租户下的用户行（安全戳），并以目标租户的权限重签令牌。
		var user = await _db.Users.AsNoTracking()
			.FirstOrDefaultAsync(u => u.Id == callerId && u.TenantId == homeTenantId, cancellationToken);
		if (user is null)
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：主租户用户不存在。" });

		var perms = await _identity.GetPermissionsAsync(request.TenantId, callerId, cancellationToken);
		var token = _token.Issue(request.TenantId, callerId, user.Username, perms, user.SecurityStamp, homeTenantId);
		var locale = await ResolveTenantLocaleAsync(request.TenantId, cancellationToken);

		await _audit.LogAsync(new AuditLogEntry(
			request.TenantId, "tenant.switch", "UserTenant",
			UserId: callerId, Actor: user.Username,
			Message: $"从租户 {homeTenantId} 切换至租户 {request.TenantId}"), cancellationToken);

		// 观测旁路：标记本次为合法租户切换，供审计中间件捕获。
		TenantDataPlanePolicy.Store(HttpContext, new TenantDataPlaneResolution(
			homeTenantId, request.TenantId, request.TenantId, true));

		return Ok(new TenantSwitchResult(
			token, 3600, request.TenantId, homeTenantId, callerId, user.Username,
			perms, locale.Available, locale.Default));
	}

	private async Task<(List<string> Available, string Default)> ResolveTenantLocaleAsync(long tenantId, CancellationToken ct)
	{
		// M3-01：语言关系取自 TenantUiLanguage（替代 localization:* JSON）。
		var available = await _tenantLanguage.GetAvailableCulturesAsync(tenantId, ct);
		var defaultCulture = await _tenantLanguage.GetDefaultCultureAsync(tenantId, ct);
		return (available, defaultCulture);
	}
}

/// <summary>加入可切换租户请求。</summary>
public sealed record AddTenantMemberRequest(long UserId, long TenantId, bool IsDefault = false);

/// <summary>切换生效租户请求。</summary>
public sealed record SwitchTenantRequest(long TenantId);

/// <summary>切换成功响应（含重签令牌与切换后端租户上下文）。</summary>
public sealed record TenantSwitchResult(
	string Token,
	int ExpiresInSeconds,
	long TenantId,
	long HomeTenantId,
	long UserId,
	string Username,
	IReadOnlyList<string> Permissions,
	IReadOnlyList<string> AvailableCultures,
	string DefaultCulture);
