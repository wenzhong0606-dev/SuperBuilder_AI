using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Localization;
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
	private readonly IConfiguration _config;
	private readonly ITenantLanguageService _tenantLanguage;
	private readonly IRefreshTokenStore _refreshStore;

	public AuthController(SuperBIContext db, IIdentityService identity, ITokenService token, IPasswordHasher hasher, IConfiguration configuration, ITenantLanguageService tenantLanguage, IRefreshTokenStore refreshStore)
	{
		_db = db;
		_identity = identity;
		_token = token;
		_hasher = hasher;
		_config = configuration;
		_tenantLanguage = tenantLanguage;
		_refreshStore = refreshStore;
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

		// M1-02：停用治理——停用租户禁止登录。
		var tenant = await _db.Tenants.AsNoTracking()
			.FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);
		if (tenant is null || !tenant.Enabled)
		{
			SecurityAuditContext.Reject(HttpContext, ErrorCodes.AuthInvalidCredential, "tenant-disabled", request.TenantId);
			return Unauthorized(new { error = "租户已停用或不存在。" });
		}

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
		var clientIp = HttpContext?.Connection?.RemoteIpAddress?.ToString();
		var userAgent = HttpContext?.Request?.Headers?.UserAgent.ToString();

		// Phase 2：签发 access + refresh 对；refresh 明文仅此一次返回，哈希交由 store 持久化。
		var (accessToken, refreshPlain) = _token.IssuePair(request.TenantId, user.Id, user.Username, perms, user.SecurityStamp);
		var refreshLifetimeDays = _config.GetValue("Auth:RefreshTokenLifetimeDays", 14.0);
		await _refreshStore.CreateAsync(
			user.Id, request.TenantId, user.SecurityStamp, clientIp, userAgent,
			DateTimeOffset.UtcNow.AddDays(refreshLifetimeDays), cancellationToken);

		var locale = await ResolveTenantLocaleAsync(request.TenantId, cancellationToken);

		return Ok(new AuthResult
		{
			Token = accessToken,
			RefreshToken = refreshPlain,
			ExpiresInSeconds = (int)_token.Lifetime.TotalSeconds,
			TenantId = request.TenantId,
			UserId = user.Id,
			Username = user.Username,
			Permissions = perms,
			AvailableCultures = locale.Available,
			DefaultCulture = locale.Default,
		});
	}

	/// <summary>用刷新令牌换取新访问令牌（Phase 2）。[AllowAnonymous]，需在匿名白名单放行。</summary>
	[HttpPost("refresh")]
	[AllowAnonymous]
	public async Task<IActionResult> Refresh(
		[FromBody] RefreshRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
			return BadRequest(new { error = "refreshToken 必填。" });

		var outcome = await _refreshStore.RedeemAsync(
			request.RefreshToken,
			HttpContext?.Connection?.RemoteIpAddress?.ToString(),
			HttpContext?.Request?.Headers?.UserAgent.ToString(),
			cancellationToken);

		switch (outcome.Status)
		{
			case RefreshRedeemStatus.Success:
				var perms = await _identity.GetPermissionsAsync(outcome.TenantId, outcome.UserId, cancellationToken);
				var accessToken = _token.Issue(outcome.TenantId, outcome.UserId, outcome.Username ?? string.Empty, perms, outcome.SecurityStamp);
				var locale = await ResolveTenantLocaleAsync(outcome.TenantId, cancellationToken);
				return Ok(new AuthResult
				{
					Token = accessToken,
					RefreshToken = outcome.NewRefreshToken,
					ExpiresInSeconds = (int)_token.Lifetime.TotalSeconds,
					TenantId = outcome.TenantId,
					UserId = outcome.UserId,
					Username = outcome.Username ?? string.Empty,
					Permissions = perms,
					AvailableCultures = locale.Available,
					DefaultCulture = locale.Default,
				});

			case RefreshRedeemStatus.Expired:
				return Unauthorized(new { error = "刷新令牌已过期，请重新登录。" });
			case RefreshRedeemStatus.Revoked:
				return Unauthorized(new { error = "刷新令牌已失效，请重新登录。" });
			case RefreshRedeemStatus.ReuseDetected:
				return Unauthorized(new { error = "检测到刷新令牌复用，已吊销会话，请重新登录。" });
			case RefreshRedeemStatus.StampMismatch:
				return Unauthorized(new { error = "凭据已变更（口令/角色），请重新登录。" });
			case RefreshRedeemStatus.ConcurrencyFailure:
				return Unauthorized(new { error = "刷新令牌已被使用，请重新登录。" });
			case RefreshRedeemStatus.Unknown:
			default:
				return Unauthorized(new { error = "无效的刷新令牌。" });
		}
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
		// M3-01：语言关系取自 TenantUiLanguage（替代 localization:* JSON）。
		var available = await _tenantLanguage.GetAvailableCulturesAsync(tenantId, ct);
		var defaultCulture = await _tenantLanguage.GetDefaultCultureAsync(tenantId, ct);
		return (available, defaultCulture);
	}

	[HttpGet("login-options")]
	[AllowAnonymous]
	public async Task<IActionResult> LoginOptions([FromQuery] string? q = null, CancellationToken cancellationToken = default)
	{
		// M0-08：部署级隐藏策略（默认隐藏），避免未认证用户枚举全部租户代码/名称目录
		var showDirectory = _config.GetValue<bool>("Auth:ShowTenantDirectory");
		if (!showDirectory)
			return Ok(Array.Empty<object>());

		var query = _db.Tenants.IgnoreQueryFilters().AsNoTracking()
			.Where(x => x.Enabled && x.TenantCode != "platform");
		if (!string.IsNullOrWhiteSpace(q))
			query = query.Where(x => x.TenantName.Contains(q) || x.TenantCode.Contains(q));
		var tenants = await query.OrderBy(x => x.TenantName)
			.Select(x => new { x.Id, x.TenantCode, x.TenantName }).ToListAsync(cancellationToken);

		var ids = tenants.Select(x => x.Id).ToList();
		var langMap = await _tenantLanguage.GetLanguagesForTenantsAsync(ids, cancellationToken);
		return Ok(tenants.Select(x =>
		{
			var infos = langMap.TryGetValue(x.Id, out var l) ? l : new List<TenantLanguageInfo>();
			var cultures = infos.Where(i => i.Enabled).Select(i => i.Culture).ToList();
			if (cultures.Count == 0) cultures.Add("zh-CN");
			var defaultCulture = infos.FirstOrDefault(i => i.IsDefault && i.Enabled)?.Culture ?? cultures[0];
			return new { x.Id, x.TenantCode, Name = x.TenantName, AvailableCultures = cultures, DefaultCulture = defaultCulture };
		}));
	}

	/// <summary>
	/// 按已知租户编码解析租户（M6 登录兜底）：不枚举目录，仅解析调用方已掌握的编码；
	/// 排除 platform 租户与已停用租户。用于 <c>Auth:ShowTenantDirectory</c> 关闭（M0-08 默认隐藏）时，
	/// 登录页以「手动输入租户编码」方式仍可登录，避免无目录即无法选租户的死局。
	/// </summary>
	[HttpGet("tenant-by-code")]
	[AllowAnonymous]
	public async Task<IActionResult> TenantByCode([FromQuery] string? code = null, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(code))
			return BadRequest(new { error = "code 必填。" });

		var tenant = await _db.Tenants.AsNoTracking()
			.FirstOrDefaultAsync(t => t.TenantCode == code && t.Enabled && t.TenantCode != "platform", cancellationToken);
		if (tenant is null)
			return NotFound(new { error = "租户编码不存在或已停用。" });

		return Ok(new { Id = tenant.Id, TenantCode = tenant.TenantCode, Name = tenant.TenantName });
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

/// <summary>刷新令牌赎回请求（Phase 2）。</summary>
public sealed class RefreshRequest
{
	/// <summary>刷新令牌明文（登录/上次刷新响应返回）。</summary>
	public string? RefreshToken { get; set; }
}

/// <summary>登录/当前用户响应。</summary>
public sealed class AuthResult
{
	public string? Token { get; set; }
	public int ExpiresInSeconds { get; set; }
	/// <summary>刷新令牌明文（Phase 2）。仅登录与刷新响应中返回一次；API 仅持久化其哈希。</summary>
	public string? RefreshToken { get; set; }
	public long TenantId { get; set; }
	public long UserId { get; set; }
	public string Username { get; set; } = string.Empty;
	public System.Collections.Generic.IReadOnlyList<string> Permissions { get; set; }
		= System.Array.Empty<string>();
	public System.Collections.Generic.IReadOnlyList<string> AvailableCultures { get; set; } = new[] { "zh-CN" };
	public string DefaultCulture { get; set; } = "zh-CN";
}
