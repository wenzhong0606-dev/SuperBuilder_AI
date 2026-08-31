using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.Auth;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 认证端点（P11.0 安全轨道）。
///
/// <para>
/// <list type="bullet">
/// <item><c>POST /api/auth/login</c>：以 <c>username + tenantId</c> 换取无状态访问令牌（匿名白名单，无需令牌）。</item>
/// <item><c>GET /api/auth/me</c>：返回当前已认证主体信息（需有效令牌）。</item>
/// </list>
/// </para>
///
/// <para>
/// 注：本阶段登录不校验口令（<see cref="User.PasswordHash"/> 已建模但认证流程在后续阶段补齐，见 P13 BYO），
/// 仅做「用户存在且启用」校验后签发令牌。生产环境必须配合口令/外部 IdP 校验与 TLS。
/// </para>
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
	private readonly SuperBIContext _db;
	private readonly IIdentityService _identity;
	private readonly ITokenService _token;

	public AuthController(SuperBIContext db, IIdentityService identity, ITokenService token)
	{
		_db = db;
		_identity = identity;
		_token = token;
	}

	/// <summary>登录并签发访问令牌。</summary>
	[HttpPost("login")]
	[AllowAnonymous]
	public async Task<IActionResult> Login(
		[FromBody] LoginRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest(new { error = "请求体不能为空。" });
		if (request.TenantId <= 0) return BadRequest(new { error = "tenantId 必须大于 0。" });
		if (string.IsNullOrWhiteSpace(request.Username)) return BadRequest(new { error = "username 必填。" });

		var user = await _db.Users
			.AsNoTracking()
			.FirstOrDefaultAsync(
				u => u.TenantId == request.TenantId && u.Username == request.Username,
				cancellationToken);

		if (user is null || user.Status != UserStatus.Active)
			return Unauthorized(new { error = "用户不存在或已禁用。" });

		var perms = await _identity.GetPermissionsAsync(request.TenantId, user.Id, cancellationToken);
		var token = _token.Issue(request.TenantId, user.Id, user.Username, perms);

		return Ok(new AuthResult
		{
			Token = token,
			ExpiresInSeconds = 3600,
			TenantId = request.TenantId,
			UserId = user.Id,
			Username = user.Username,
			Permissions = perms,
		});
	}

	/// <summary>返回当前已认证主体。</summary>
	[HttpGet("me")]
	public IActionResult Me()
	{
		if (User?.Identity is not { IsAuthenticated: true })
			return Unauthorized(new { error = "未认证。" });

		var tenantId = User.FindFirst("tid")?.Value ?? "0";
		var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0";
		var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
		var perms = System.Linq.Enumerable
			.Where(User.Claims, c => c.Type == "perm")
			.Select(c => c.Value)
			.ToList();

		return Ok(new AuthResult
		{
			Token = null,
			ExpiresInSeconds = 0,
			TenantId = long.TryParse(tenantId, out var t) ? t : 0,
			UserId = long.TryParse(userId, out var u) ? u : 0,
			Username = username,
			Permissions = perms,
		});
	}
}

/// <summary>登录请求。</summary>
public sealed class LoginRequest
{
	public string? Username { get; set; }
	public long TenantId { get; set; }
}

/// <summary>登录/当前用户响应。</summary>
public sealed class AuthResult
{
	public string? Token { get; set; }
	public int ExpiresInSeconds { get; set; }
	public long TenantId { get; set; }
	public long UserId { get; set; }
	public string Username { get; set; } = string.Empty;
	public System.Collections.Generic.IReadOnlyList<string> Permissions { get; set; }
		= System.Array.Empty<string>();
}
