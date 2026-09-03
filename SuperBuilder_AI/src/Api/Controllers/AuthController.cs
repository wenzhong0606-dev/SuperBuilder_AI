using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 认证端点（P11.0 安全轨道）。
///
/// <para>
/// <list type="bullet">
/// <item><c>POST /api/auth/login</c>：以 <c>username + tenantId + password</c> 换取无状态访问令牌（匿名白名单，无需令牌）。</item>
/// <item><c>GET /api/auth/me</c>：返回当前已认证主体信息（需有效令牌）。</item>
/// </list>
/// </para>
///
/// <para>
/// P0-04A 正式口令认证：<see cref="User.PasswordHash"/> 已设置的账号必须校验口令（PBKDF2，见 <see cref="IPasswordHasher"/>）；
/// <see cref="User.PasswordHash"/> 为空的账号视为「尚未初始化口令」，登录被拒绝并提示初始化（fail-closed，避免无口令账号被直接登录）。
/// 校验通过后签发携带用户 <see cref="User.SecurityStamp"/> 的令牌，供 P0-04B 吊销校验。
/// 生产环境必须配合 TLS；外部 IdP/SSO 作为后续扩展点保留。
/// </para>
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
	private readonly SuperBIContext _db;
	private readonly IIdentityService _identity;
	private readonly ITokenService _token;
	private readonly IPasswordHasher _hasher;

	public AuthController(SuperBIContext db, IIdentityService identity, ITokenService token, IPasswordHasher hasher)
	{
		_db = db;
		_identity = identity;
		_token = token;
		_hasher = hasher;
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
		{
			SecurityAuditContext.Reject(HttpContext, ErrorCodes.AuthInvalidCredential, "invalid-credential", request.TenantId);
			return Unauthorized(new { error = "用户不存在或已禁用。" });
		}

		// P0-04A：正式口令认证。
		if (string.IsNullOrEmpty(user.PasswordHash))
		{
			SecurityAuditContext.Reject(HttpContext, ErrorCodes.AuthInvalidCredential, "password-not-initialized", request.TenantId, user.Id);
			return Unauthorized(new { error = "账户尚未设置口令，请联系管理员初始化后再登录。" });
		}
		if (string.IsNullOrEmpty(request.Password) || !_hasher.Verify(request.Password, user.PasswordHash))
		{
			SecurityAuditContext.Reject(HttpContext, ErrorCodes.AuthInvalidCredential, "invalid-credential", request.TenantId, user.Id);
			return Unauthorized(new { error = "用户名或口令错误。" });
		}

		var perms = await _identity.GetPermissionsAsync(request.TenantId, user.Id, cancellationToken);
		var token = _token.Issue(request.TenantId, user.Id, user.Username, perms, user.SecurityStamp);
		var locale = await ResolveTenantLocaleAsync(request.TenantId, cancellationToken);

		return Ok(new AuthResult
		{
			Token = token,
			ExpiresInSeconds = 3600,
			TenantId = request.TenantId,
			UserId = user.Id,
			Username = user.Username,
			Permissions = perms,
			AvailableCultures = locale.Available,
			DefaultCulture = locale.Default,
		});
	}

	/// <summary>返回当前已认证主体。</summary>
	[HttpGet("me")]
	public async Task<IActionResult> Me(CancellationToken cancellationToken)
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

		var resolvedTenantId = long.TryParse(tenantId, out var t) ? t : 0;
		var locale = await ResolveTenantLocaleAsync(resolvedTenantId, cancellationToken);
		return Ok(new AuthResult
		{
			Token = null,
			ExpiresInSeconds = 0,
			TenantId = resolvedTenantId,
			UserId = long.TryParse(userId, out var u) ? u : 0,
			Username = username,
			Permissions = perms,
			AvailableCultures = locale.Available,
			DefaultCulture = locale.Default,
		});
	}

	private async Task<(List<string> Available, string Default)> ResolveTenantLocaleAsync(long tenantId, CancellationToken ct)
	{
		var settings = await _db.TenantSettings.AsNoTracking()
			.Where(s => s.TenantId == tenantId && (s.Key == "localization:availableCultures" || s.Key == "localization:defaultCulture"))
			.ToDictionaryAsync(s => s.Key, s => s.Value, ct);
		List<string> available;
		try { available = System.Text.Json.JsonSerializer.Deserialize<List<string>>(settings.GetValueOrDefault("localization:availableCultures") ?? "[]") ?? new(); }
		catch { available = new(); }
		available = available.Where(x => x is "zh-CN" or "en-US").Distinct().ToList();
		if (available.Count == 0) available.Add("zh-CN");
		var defaultCulture = settings.GetValueOrDefault("localization:defaultCulture") ?? available[0];
		if (!available.Contains(defaultCulture)) defaultCulture = available[0];
		return (available, defaultCulture);
	}

	[HttpGet("login-options")]
	[AllowAnonymous]
	public async Task<IActionResult> LoginOptions(CancellationToken cancellationToken)
	{
		var tenants = await _db.Tenants.IgnoreQueryFilters().AsNoTracking().Where(x => x.Enabled).OrderBy(x => x.TenantName)
			.Select(x => new { x.Id, x.TenantCode, x.TenantName }).ToListAsync(cancellationToken);
		var ids = tenants.Select(x => x.Id).ToArray();
		var settings = await _db.TenantSettings.IgnoreQueryFilters().AsNoTracking()
			.Where(x => ids.Contains(x.TenantId) && (x.Key == "localization:availableCultures" || x.Key == "localization:defaultCulture"))
			.ToListAsync(cancellationToken);
		var platformCultures = await _db.UiLanguages.AsNoTracking().Where(x => x.Enabled).OrderBy(x => x.SortOrder).Select(x => x.Culture).ToListAsync(cancellationToken);
		return Ok(tenants.Select(x =>
		{
			var own = settings.Where(s => s.TenantId == x.Id).ToDictionary(s => s.Key, s => s.Value);
			List<string> cultures;
			try { cultures = System.Text.Json.JsonSerializer.Deserialize<List<string>>(own.GetValueOrDefault("localization:availableCultures") ?? "[]") ?? new(); } catch { cultures = new(); }
			if (x.TenantCode == "platform" && platformCultures.Count > 0) cultures = platformCultures;
			if (cultures.Count == 0) cultures.Add("zh-CN");
			var defaultCulture = own.GetValueOrDefault("localization:defaultCulture") ?? cultures[0];
			return new { x.Id, x.TenantCode, Name = x.TenantCode == "platform" ? "平台管理" : x.TenantName, AvailableCultures = cultures, DefaultCulture = defaultCulture };
		}));
	}
}

/// <summary>登录请求。</summary>
public sealed class LoginRequest
{
	public string? Username { get; set; }
	public long TenantId { get; set; }
	/// <summary>明文口令（P0-04A 起必填；空值或错误口令将被拒绝）。</summary>
	public string? Password { get; set; }
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
	public System.Collections.Generic.IReadOnlyList<string> AvailableCultures { get; set; } = new[] { "zh-CN" };
	public string DefaultCulture { get; set; } = "zh-CN";
}
