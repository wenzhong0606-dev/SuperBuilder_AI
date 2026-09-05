using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Identity;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 租户管理 API（P4 Multi-Tenant Platform Core 生产端点）。
///
/// 提供租户的列举 / 查询 / 创建 / 启用停用。
/// 本控制器只读与写 <c>Organization.Tenant</c>，不触碰任何查询链路与 Golden 契约数据；
/// 属平台级管理面，不影响 Golden 18/18 行为契约。
/// </summary>
[ApiController]
[Route("api/tenant-management")]
	public sealed class TenantManagementController : ControllerBase
	{
		private readonly SuperBIContext _db;
		private readonly SuperBuilder_AI.Interfaces.Identity.IIdentityService _identity;
		private readonly IPlatformAdminScopeService _scope;

		public TenantManagementController(SuperBIContext db, SuperBuilder_AI.Interfaces.Identity.IIdentityService identity, IPlatformAdminScopeService scope)
		{
			_db = db;
			_identity = identity;
			_scope = scope;
		}

		/// <summary>
		/// SB-P0-02A 平台治理面权限门禁：租户生命周期操作属平台级管理面，调用者令牌须携带
		/// 对应的 <c>platform:tenant:*</c> 权限码（由 SB-P0-11 的治理角色授予），否则 403。
		/// 本控制器只读写为 <c>Organization.Tenant</c> / <c>TenantSettings</c> 全局表，不触碰任何
		/// 租户业务数据与 Golden 契约，不影响 Golden 18/18 行为契约。
		/// </summary>
		private IActionResult? RequirePlatformPermission(string permission)
		{
			if (!User.HasClaim("perm", permission))
				return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = $"禁止：缺少 {permission} 权限。" });
			return null;
		}

		/// <summary>从令牌解析当前治理管理员用户 Id（用于租户范围强制校验）。</summary>
		private long CallerId()
		{
			var v = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			return long.TryParse(v, out var id) ? id : 0;
		}

		/// <summary>
		/// M2-02 租户范围强制校验：调用者必须是持有对应 platform:tenant:* 权限的治理主体，
		/// 且其被授权的管理范围须包含目标租户。越权（范围外）返回 403，且不会产生任何管理动作。
		/// 管理员在范围表中无记录 = 默认管理全部租户。
		/// </summary>
		private async Task<IActionResult?> RequireInScopeAsync(long targetTenantId, string permission, CancellationToken cancellationToken)
		{
			var denied = RequirePlatformPermission(permission);
			if (denied is not null) return denied;
			var callerId = CallerId();
			if (callerId > 0 && !await _scope.CanManageAsync(callerId, targetTenantId, cancellationToken))
				return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = $"禁止：目标租户 {targetTenantId} 不在您的授权管理范围内。" });
			return null;
		}

	[HttpGet]
	public async Task<IActionResult> List(CancellationToken cancellationToken = default)
	{
		if (RequirePlatformPermission(IdentityPermissions.PlatformTenantView) is { } denied) return denied;
		// M2-02：限定范围的管理员仅见其授权租户；无范围记录（默认全部）不受限。
		var scopedIds = await _scope.GetScopedTenantIdsAsync(CallerId(), cancellationToken);
		var query = _db.Tenants.AsNoTracking();
		if (scopedIds.Count > 0)
			query = query.Where(t => scopedIds.Contains(t.Id));
		var tenants = await query
			.OrderBy(t => t.Id)
			.Select(t => new TenantSummary(t.Id, t.TenantCode, t.TenantName, t.Enabled,
				_db.TenantSettings.Where(s => s.TenantId == t.Id && s.Key == "localization:availableCultures").Select(s => s.Value).FirstOrDefault(),
				_db.TenantSettings.Where(s => s.TenantId == t.Id && s.Key == "localization:defaultCulture").Select(s => s.Value).FirstOrDefault()))
			.ToListAsync(cancellationToken);
		return Ok(tenants);
	}

	[HttpGet("{id:long}")]
	public async Task<IActionResult> Get(long id, CancellationToken cancellationToken = default)
	{
		if (await RequireInScopeAsync(id, IdentityPermissions.PlatformTenantView, cancellationToken) is { } denied) return denied;
		// P0-02B：治理角色读取具体租户 B，记录管理目标（不记为 TenantSwitch）
		TenantDataPlanePolicy.StoreManagementTarget(HttpContext, id, "tenant.read", true);
		var t = await _db.Tenants
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (t is null) return NotFound();
		return Ok(new TenantSummary(t.Id, t.TenantCode, t.TenantName, t.Enabled));
	}

	[HttpPost]
	public async Task<IActionResult> Create(
		[FromBody] CreateTenantRequest request,
		CancellationToken cancellationToken = default)
	{
		if (RequirePlatformPermission(IdentityPermissions.PlatformTenantManage) is { } denied) return denied;
		// M1-02：规范化（去空白 + 小写）并校验长度/必填；TenantCode 创建后不可变。
		var code = Tenant.NormalizeCode(request.TenantCode);
		if (code.Length == 0 || code.Length > Tenant.MaxCodeLength)
			return BadRequest($"TenantCode 必填且长度不超过 {Tenant.MaxCodeLength}。");
		var tenantName = (request.TenantName ?? string.Empty).Trim();
		if (tenantName.Length == 0 || tenantName.Length > Tenant.MaxNameLength)
			return BadRequest($"TenantName 必填且长度不超过 {Tenant.MaxNameLength}。");
		var adminUsername = (request.AdminUsername ?? string.Empty).Trim();
		if (adminUsername.Length == 0 || string.IsNullOrWhiteSpace(request.AdminPassword) || request.AdminPassword.Length < 8)
			return BadRequest("首位租户管理员用户名必填，初始口令至少 8 位。");

		if (await _db.Tenants.AnyAsync(t => t.TenantCode == code, cancellationToken))
			return Conflict($"租户编码 {code} 已存在。");

		var tenant = new Tenant
		{
			TenantCode = code,
			TenantName = tenantName,
			Enabled = true
		};
		await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
		_db.Tenants.Add(tenant);
		await _db.SaveChangesAsync(cancellationToken);
		var cultures = (request.AvailableCultures ?? new[] { "zh-CN" })
			.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		var supportedCultures = await _db.UiLanguages.AsNoTracking().Where(x => x.Enabled).Select(x => x.Culture).ToListAsync(cancellationToken);
		if (supportedCultures.Count > 0)
			cultures = cultures.Where(x => supportedCultures.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
		if (cultures.Length == 0) cultures = new[] { "zh-CN" };
		var defaultCulture = cultures.Contains(request.DefaultCulture ?? "") ? request.DefaultCulture! : cultures[0];
		_db.TenantSettings.AddRange(
			new TenantSetting { TenantId = tenant.Id, Key = "localization:availableCultures", Value = System.Text.Json.JsonSerializer.Serialize(cultures), DataType = "json", IsLocked = true },
			new TenantSetting { TenantId = tenant.Id, Key = "localization:defaultCulture", Value = defaultCulture, DataType = "string", IsLocked = true });
		await _db.SaveChangesAsync(cancellationToken);
		var created = await _identity.CreateUserAsync(
			tenant.Id, adminUsername, request.AdminDisplayName ?? adminUsername,
			request.AdminEmail ?? string.Empty, new[] { IdentityRoles.TenantAdmin }, cancellationToken);
		if (!created.Success || created.Id is null)
		{
			await transaction.RollbackAsync(cancellationToken);
			return Conflict(created.Errors.FirstOrDefault() ?? "租户管理员创建失败。");
		}
		var passwordSet = await _identity.SetPasswordAsync(tenant.Id, created.Id.Value, request.AdminPassword, cancellationToken);
		if (!passwordSet.Success)
		{
			await transaction.RollbackAsync(cancellationToken);
			return BadRequest(passwordSet.Errors.FirstOrDefault() ?? "租户管理员口令设置失败。");
		}
		await transaction.CommitAsync(cancellationToken);
		return Ok(new CreateTenantResult(
			new TenantSummary(tenant.Id, tenant.TenantCode, tenant.TenantName, tenant.Enabled),
			created.Id.Value, adminUsername));
	}

	[HttpPatch("{id:long}/enable")]
	public async Task<IActionResult> Enable(long id, [FromBody] SetEnabledRequest? body, CancellationToken cancellationToken = default)
		=> await SetEnabledAsync(id, true, body, cancellationToken);

	[HttpPatch("{id:long}/disable")]
	public async Task<IActionResult> Disable(long id, [FromBody] SetEnabledRequest? body, CancellationToken cancellationToken = default)
		=> await SetEnabledAsync(id, false, body, cancellationToken);

	[HttpPut("{id:long}")]
	public async Task<IActionResult> Update(long id, [FromBody] UpdateTenantRequest request, CancellationToken cancellationToken = default)
	{
		if (await RequireInScopeAsync(id, IdentityPermissions.PlatformTenantManage, cancellationToken) is { } denied) return denied;
		TenantDataPlanePolicy.StoreManagementTarget(HttpContext, id, "tenant.update", true);
		var tenant = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (tenant is null) return NotFound();
		var cultures = (request.AvailableCultures ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		var supported = await _db.UiLanguages.AsNoTracking().Where(x => x.Enabled).Select(x => x.Culture).ToListAsync(cancellationToken);
		cultures = cultures.Where(x => supported.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
		if (cultures.Length == 0) return BadRequest("至少选择一种平台已启用的语言。");
		var defaultCulture = cultures.Contains(request.DefaultCulture ?? "", StringComparer.OrdinalIgnoreCase) ? request.DefaultCulture! : cultures[0];
		// M1-02：TenantName 必填且长度受控；TenantCode 创建后不可变（UpdateTenantRequest 不含该字段，结构即保证）。
		var tenantName = (request.TenantName ?? tenant.TenantName ?? string.Empty).Trim();
		if (tenantName.Length == 0 || tenantName.Length > Tenant.MaxNameLength)
			return BadRequest($"TenantName 必填且长度不超过 {Tenant.MaxNameLength}。");
		tenant.TenantName = tenantName;
		await UpsertInternalSetting(id, "localization:availableCultures", System.Text.Json.JsonSerializer.Serialize(cultures), "json", cancellationToken);
		await UpsertInternalSetting(id, "localization:defaultCulture", defaultCulture, "string", cancellationToken);
		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new TenantSummary(tenant.Id, tenant.TenantCode, tenant.TenantName, tenant.Enabled, System.Text.Json.JsonSerializer.Serialize(cultures), defaultCulture));
	}

	private async Task UpsertInternalSetting(long tenantId, string key, string value, string dataType, CancellationToken ct)
	{
		// M1-02：平台内部写入——仅校验 Key 前缀（允许写入锁定键），并校验 DataType 与值。
		if (!TenantSettingPolicy.ValidatePlatformWrite(key, out var perr))
			throw new InvalidOperationException(perr);
		if (!TenantSettingPolicy.TryValidateValue(dataType, value, out var verr))
			throw new InvalidOperationException(verr);
		var row = await _db.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Key == key, ct);
		if (row is null) { row = new TenantSetting { TenantId = tenantId, Key = key }; _db.TenantSettings.Add(row); }
		row.Value = value; row.DataType = dataType;
	}

	private async Task<IActionResult> SetEnabledAsync(long id, bool enabled, SetEnabledRequest? body, CancellationToken cancellationToken)
	{
		if (await RequireInScopeAsync(id, IdentityPermissions.PlatformTenantManage, cancellationToken) is { } denied) return denied;
		// P0-02B：治理角色启用/停用具体租户 B，记录管理目标与动作（不记为 TenantSwitch）
		TenantDataPlanePolicy.StoreManagementTarget(
			HttpContext, id, enabled ? "tenant.enable" : "tenant.disable", true);
		var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (t is null) return NotFound();
		t.Enabled = enabled;
		// M1-02：停用治理——记录原因/时间/操作者；启用时清空。
		if (!enabled)
		{
			t.DisabledAt = DateTime.UtcNow;
			t.DisabledReason = (body?.Reason ?? string.Empty).Trim();
			t.DisabledByUserId = long.TryParse(
				User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var oid) ? oid : null;
		}
		else
		{
			t.DisabledAt = null;
			t.DisabledReason = null;
			t.DisabledByUserId = null;
		}
		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new TenantSummary(t.Id, t.TenantCode, t.TenantName, t.Enabled));
	}

	[HttpGet("{id:long}/settings")]
	public async Task<IActionResult> ListSettings(long id, CancellationToken cancellationToken = default)
	{
		if (await RequireInScopeAsync(id, IdentityPermissions.PlatformTenantView, cancellationToken) is { } denied) return denied;
		// P0-02B：治理角色读取具体租户 B 的配置，记录管理目标（不记为 TenantSwitch）
		TenantDataPlanePolicy.StoreManagementTarget(HttpContext, id, "tenant.settings.read", true);
		if (!await _db.Tenants.AnyAsync(t => t.Id == id, cancellationToken))
			return NotFound($"租户 {id} 不存在。");

		var settings = await _db.TenantSettings
			.AsNoTracking()
			.Where(s => s.TenantId == id)
			.OrderBy(s => s.Key)
			.Select(s => new TenantSettingSummary(s.Id, s.Key, s.Value, s.DataType))
			.ToListAsync(cancellationToken);
		return Ok(settings);
	}

	[HttpPost("{id:long}/settings")]
	public async Task<IActionResult> UpsertSetting(
		long id,
		[FromBody] UpsertTenantSettingRequest request,
		CancellationToken cancellationToken = default)
	{
		if (await RequireInScopeAsync(id, IdentityPermissions.PlatformTenantManage, cancellationToken) is { } denied) return denied;
		// P0-02B：治理角色写入具体租户 B 的配置，记录管理目标与动作（不记为 TenantSwitch）
		TenantDataPlanePolicy.StoreManagementTarget(HttpContext, id, "tenant.settings.upsert", true);
		var key = (request.Key ?? string.Empty).Trim();
		if (key.Length == 0)
			return BadRequest("Key 不能为空。");
		// M1-02：Key 必须属于允许目录且非锁定安全配置；DataType 须为白名单且值可解析。
		if (!TenantSettingPolicy.IsKnownKey(key))
			return BadRequest($"Key 不在允许目录中：{key}。");
		if (TenantSettingPolicy.IsLockedKey(key))
			return StatusCode(StatusCodes.Status409Conflict, $"配置 {key} 为锁定安全配置，租户不可覆盖。");
		var dataType = (request.DataType ?? "string").Trim();
		if (!TenantSettingPolicy.TryValidateValue(dataType, request.Value, out var valError))
			return BadRequest(valError);

		if (!await _db.Tenants.AnyAsync(t => t.Id == id, cancellationToken))
			return NotFound($"租户 {id} 不存在。");

		var existing = await _db.TenantSettings
			.FirstOrDefaultAsync(s => s.TenantId == id && s.Key == key, cancellationToken);
		if (existing is null)
		{
			existing = new TenantSetting { TenantId = id, Key = key };
			_db.TenantSettings.Add(existing);
		}
		existing.Value = request.Value;
		existing.DataType = dataType;
		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new TenantSettingSummary(existing.Id, existing.Key, existing.Value, existing.DataType));
	}
}

/// <summary>租户摘要 DTO。</summary>
public sealed record TenantSummary(long Id, string? TenantCode, string? TenantName, bool Enabled, string? AvailableCultures = null, string? DefaultCulture = null);

/// <summary>创建租户请求。</summary>
public sealed record CreateTenantRequest(
	string? TenantCode,
	string? TenantName,
	string? AdminUsername,
	string? AdminPassword,
	string? AdminDisplayName = null,
	string? AdminEmail = null,
	string[]? AvailableCultures = null,
	string? DefaultCulture = null);

	public sealed record CreateTenantResult(TenantSummary Tenant, long AdminUserId, string AdminUsername);
public sealed record UpdateTenantRequest(string? TenantName, string[]? AvailableCultures, string? DefaultCulture);

/// <summary>启用/停用租户请求（M1-02：停用可附原因）。</summary>
public sealed record SetEnabledRequest(string? Reason = null);

/// <summary>租户配置项 DTO。</summary>
public sealed record TenantSettingSummary(long Id, string Key, string? Value, string? DataType);

/// <summary>写入租户配置项请求。</summary>
public sealed record UpsertTenantSettingRequest(string? Key, string? Value, string? DataType);
