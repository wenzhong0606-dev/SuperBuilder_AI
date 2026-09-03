using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.Identity;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Identity / RBAC 端点（P10.2 Enterprise / SaaS）。
///
/// <para>
/// 提供租户作用域的用户、角色、权限管理，以及当前用户权限解析：
/// <list type="bullet">
/// <item><c>POST /api/identity/users</c>：创建租户用户并可同时指派角色（默认路径，确定性、不调 LLM）。</item>
/// <item><c>GET /api/identity/users</c>：按租户作用域列出用户（租户用户不跨租户可见）。</item>
/// <item><c>GET /api/identity/users/{id}</c>：获取单个用户（跨租户不可见 → 404）。</item>
/// <item><c>POST /api/identity/users/{id}/roles</c>：为用户指派角色（可引用全局角色）。</item>
/// <item><c>DELETE /api/identity/users/{id}/roles/{roleCode}</c>：撤销用户角色。</item>
/// <item><c>GET /api/identity/users/{id}/permissions</c>：解析用户经角色聚合后的全部权限码。</item>
/// <item><c>GET /api/identity/roles</c>：角色目录（含全局 TenantId=0 + 本租户角色）。</item>
/// <item><c>GET /api/identity/roles/{code}</c>：获取角色详情（含权限码）。</item>
/// <item><c>POST /api/identity/roles</c>：创建租户角色（仅 TenantId&gt;0）。</item>
/// <item><c>PUT /api/identity/roles/{code}/permissions</c>：替换租户角色的权限集（全局角色不可改）。</item>
/// <item><c>GET /api/identity/permissions</c>：权限目录（全局权限码）。</item>
/// </list>
/// </para>
///
/// <para>
/// 用户与角色指派/撤销/权限解析统一委托给 <see cref="IIdentityService"/>（P10.1，确定性、不调 LLM）；
/// 控制器仅负责租户作用域隔离、请求校验与 DTO 映射。全局角色/权限（TenantId=0）对所有租户可见但不可被
/// 租户创建/修改/删除。角色码在"同租户 + 全局"范围内唯一。
/// </para>
///
/// <para>本控制器属平台管理面，不触碰 BI 查询链路与 Golden 契约数据，不影响 Golden 18/18 行为契约。</para>
/// </summary>
[ApiController]
[Route("api/identity")]
public sealed class IdentityController : ControllerBase
{
	private readonly SuperBIContext _db;
	private readonly IIdentityService _identity;

	public IdentityController(SuperBIContext db, IIdentityService identity)
	{
		_db = db;
		_identity = identity;
	}

	/// <summary>在当前请求作用域内开启租户隔离，返回解析出的租户 Id。</summary>
	/// <summary>在当前请求作用域内开启租户隔离：有效租户恒为认证租户，跨租户显式请求直接拒绝。</summary>
	private long ScopeTo(long requestedTenantId)
	{
		var resolution = TenantDataPlanePolicy.ResolvePlatformScope(User, requestedTenantId);
		// P0-02B：把解析出的租户上下文写盘，供审计/可观测中间件读取；治理角色管理他租户时另记管理目标
		TenantDataPlanePolicy.StorePlatformScope(HttpContext, resolution, "Identity");
		if (!resolution.Authorized)
			throw new SuperBuilder_AI.Api.Errors.SuperBuilderException(
				SuperBuilder_AI.Api.Errors.ErrorCodes.TenantIsolated,
				"禁止：租户作用域请求只能访问认证租户的数据，跨租户访问被拒绝。",
				403);
		_db.ApplyTenantScope(resolution.EffectiveTenantId);
		return resolution.EffectiveTenantId;
	}

	/// <summary>创建租户用户（默认路径，确定性、不调 LLM），可选同时指派角色。</summary>
	[HttpPost("users")]
	public async Task<IActionResult> CreateUser(
		[FromBody] CreateUserRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (request.TenantId <= 0) return BadRequest("不能通过 API 创建全局/内置用户（TenantId 必须 > 0）。");
		if (string.IsNullOrWhiteSpace(request.Username)) return BadRequest("username 必填。");

		var tenantId = ScopeTo(request.TenantId);
		var result = await _identity.CreateUserAsync(tenantId, request.Username, request.DisplayName, request.Email, request.RoleCodes, cancellationToken);
		if (!result.Success || !result.Id.HasValue)
			return BadRequest(new { errors = result.Errors });

		var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == result.Id.Value, cancellationToken);
		if (user is null) return BadRequest(new { errors = new[] { "用户创建后无法读取。" } });
		return CreatedAtAction(nameof(GetUser), new { id = user.Id, tenantId }, ToUserSummary(user));
	}

	/// <summary>列表：按租户作用域返回用户（租户用户不跨租户可见）。</summary>
	[HttpGet("users")]
	public async Task<IActionResult> ListUsers(
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		ScopeTo(tenantId);
		var items = await _db.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync(cancellationToken);
		return Ok(items.Select(ToUserSummary).ToList());
	}

	/// <summary>获取单个用户（跨租户不可见 → 404）。</summary>
	[HttpGet("users/{id}")]
	public async Task<IActionResult> GetUser(
		long id,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		ScopeTo(tenantId);
		var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
		if (user is null) return NotFound();
		return Ok(ToUserSummary(user));
	}

	/// <summary>为用户指派角色（角色可为全局或本租户）。</summary>
	[HttpPost("users/{id}/roles")]
	public async Task<IActionResult> AssignRole(
		long id,
		[FromBody] AssignRoleRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (string.IsNullOrWhiteSpace(request.RoleCode)) return BadRequest("roleCode 必填。");

		tenantId = ScopeTo(tenantId);
		var result = await _identity.AssignRoleAsync(tenantId, id, request.RoleCode, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return Ok(new { userId = id, roleCode = request.RoleCode, assigned = true });
	}

	/// <summary>撤销用户角色。</summary>
	[HttpDelete("users/{id}/roles/{roleCode}")]
	public async Task<IActionResult> RevokeRole(
		long id,
		string roleCode,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(roleCode)) return BadRequest("roleCode 必填。");

		tenantId = ScopeTo(tenantId);
		var result = await _identity.RevokeRoleAsync(tenantId, id, roleCode, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return NoContent();
	}

	/// <summary>解析用户经角色聚合后的全部权限码（去重）。</summary>
	[HttpGet("users/{id}/permissions")]
	public async Task<IActionResult> GetPermissions(
		long id,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		tenantId = ScopeTo(tenantId);

		var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
		if (user is null) return NotFound();

		var perms = await _identity.GetPermissionsAsync(tenantId, id, cancellationToken);
		return Ok(new UserPermissionsResponse(id, perms.ToList()));
	}

	/// <summary>角色目录：返回全局 TenantId=0 角色 + 本租户角色（按租户在前、编码排序）。</summary>
	[HttpGet("roles")]
	public async Task<IActionResult> ListRoles(
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		ScopeTo(tenantId);
		var items = await _db.Roles.AsNoTracking()
			.Where(r => r.Code != IdentityRoles.PlatformAdmin)
			.OrderBy(r => r.TenantId) // 全局(0) 在前，租户自有在后
			.ThenBy(r => r.Code)
			.ToListAsync(cancellationToken);
		return Ok(items.Select(r => new RoleSummary(r.Id, r.TenantId, r.Code, r.Name, r.Description)).ToList());
	}

	/// <summary>获取角色详情（含权限码）。</summary>
	[HttpGet("roles/{code}")]
	public async Task<IActionResult> GetRole(
		string code,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(code)) return BadRequest("角色编码不能为空。");
		if (code == IdentityRoles.PlatformAdmin) return NotFound();
		ScopeTo(tenantId);

		var role = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
		if (role is null) return NotFound();
		return Ok(await ToRoleDetail(role.Id, cancellationToken));
	}

	/// <summary>创建租户角色（仅 TenantId&gt;0；全局角色由种子负责，不可经 API 创建）。</summary>
	[HttpPost("roles")]
	public async Task<IActionResult> CreateRole(
		[FromBody] CreateRoleRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (request.TenantId <= 0) return BadRequest("不能创建全局/内置角色（TenantId 必须 > 0）。");
		if (string.IsNullOrWhiteSpace(request.Code)) return BadRequest("角色编码不能为空。");

		var tenantId = ScopeTo(request.TenantId);
		var conflict = await _db.Roles.IgnoreQueryFilters()
			.AnyAsync(r => r.Code == request.Code && (r.TenantId == tenantId || r.TenantId == 0), cancellationToken);
		if (conflict) return Conflict(new { errors = new[] { $"角色编码已存在：{request.Code}。" } });

		var role = new Role
		{
			TenantId = tenantId,
			Code = request.Code,
			Name = request.Name ?? request.Code,
			Description = request.Description ?? string.Empty,
		};
		_db.Roles.Add(role);
		await _db.SaveChangesAsync(cancellationToken);

		if (request.Permissions is { Length: > 0 })
		{
			var permIds = await _db.Permissions
				.Where(p => !p.Code.StartsWith("platform:") && (p.TenantId == tenantId || p.TenantId == 0) && request.Permissions.Contains(p.Code))
				.Select(p => p.Id).Distinct().ToListAsync(cancellationToken);
			foreach (var permId in permIds)
				_db.RolePermissions.Add(new RolePermission { TenantId = tenantId, RoleId = role.Id, PermissionId = permId });
			await _db.SaveChangesAsync(cancellationToken);
		}

		return CreatedAtAction(nameof(GetRole), new { code = role.Code, tenantId }, await ToRoleDetail(role.Id, cancellationToken));
	}

	/// <summary>替换租户角色的权限集（全局角色不可改；跨租户不可见 → 404）。</summary>
	[HttpPut("roles/{code}/permissions")]
	public async Task<IActionResult> UpdateRolePermissions(
		string code,
		[FromBody] UpdateRolePermissionsRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (string.IsNullOrWhiteSpace(code)) return BadRequest("角色编码不能为空。");

		var tid = ScopeTo(tenantId);
		var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
		if (role is null) return NotFound();
		if (role.TenantId == 0)
			return BadRequest(new { errors = new[] { "全局/内置角色不可修改。" } });
		if (role.TenantId != tid) return NotFound();

		var permIds = await _db.Permissions
			.Where(p => !p.Code.StartsWith("platform:") && (p.TenantId == tid || p.TenantId == 0) && (request.Permissions ?? new string[0]).Contains(p.Code))
			.Select(p => p.Id).Distinct().ToListAsync(cancellationToken);

		var existing = await _db.RolePermissions.Where(rp => rp.RoleId == role.Id).ToListAsync(cancellationToken);
		_db.RolePermissions.RemoveRange(existing);
		foreach (var permId in permIds)
			_db.RolePermissions.Add(new RolePermission { TenantId = tid, RoleId = role.Id, PermissionId = permId });
		await _db.SaveChangesAsync(cancellationToken);

		return Ok(await ToRoleDetail(role.Id, cancellationToken));
	}

	/// <summary>权限目录：返回全局权限码（对所有租户可见）。</summary>
	[HttpGet("permissions")]
	public async Task<IActionResult> ListPermissions(
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		ScopeTo(tenantId);
		var items = await _db.Permissions.AsNoTracking()
			.Where(p => !p.Code.StartsWith("platform:"))
			.OrderBy(p => p.TenantId).ThenBy(p => p.Category).ThenBy(p => p.Code)
			.ToListAsync(cancellationToken);
		return Ok(items.Select(p => new PermissionSummary(p.Id, p.TenantId, p.Code, p.Name, p.Category, p.Description)).ToList());
	}

	private async Task<RoleDetail> ToRoleDetail(long roleId, CancellationToken ct)
	{
		var role = await _db.Roles.IgnoreQueryFilters().FirstAsync(r => r.Id == roleId, ct);
		var permCodes = await (
			from rp in _db.RolePermissions.Where(rp => rp.RoleId == roleId)
			join p in _db.Permissions.IgnoreQueryFilters() on rp.PermissionId equals p.Id
			select p.Code).Distinct().ToListAsync(ct);
		return new RoleDetail(role.Id, role.TenantId, role.Code, role.Name, role.Description, permCodes);
	}

	private static UserSummary ToUserSummary(User u) =>
		new(u.Id, u.TenantId, u.Username, u.DisplayName, u.Email, u.Status.ToString());

	#region Request / Response DTOs
	/// <summary>创建用户请求体（默认路径，确定性、不调 LLM）。</summary>
	public sealed record CreateUserRequest(
		long TenantId,
		string Username,
		string? DisplayName = null,
		string? Email = null,
		string[]? RoleCodes = null);

	/// <summary>指派角色请求体。</summary>
	public sealed record AssignRoleRequest(string RoleCode);

	/// <summary>创建角色请求体（仅租户，TenantId&gt;0）。</summary>
	public sealed record CreateRoleRequest(
		long TenantId,
		string Code,
		string? Name = null,
		string? Description = null,
		string[]? Permissions = null);

	/// <summary>替换角色权限集请求体。</summary>
	public sealed record UpdateRolePermissionsRequest(string[]? Permissions = null);

	/// <summary>用户摘要 DTO。</summary>
	public sealed record UserSummary(
		long Id,
		long TenantId,
		string Username,
		string DisplayName,
		string Email,
		string Status);

	/// <summary>角色摘要 DTO。</summary>
	public sealed record RoleSummary(
		long Id,
		long TenantId,
		string Code,
		string Name,
		string Description);

	/// <summary>角色详情 DTO（含权限码）。</summary>
	public sealed record RoleDetail(
		long Id,
		long TenantId,
		string Code,
		string Name,
		string Description,
		IReadOnlyList<string> Permissions);

	/// <summary>权限摘要 DTO。</summary>
	public sealed record PermissionSummary(
		long Id,
		long TenantId,
		string Code,
		string Name,
		string Category,
		string Description);

	/// <summary>用户权限解析响应 DTO。</summary>
	public sealed record UserPermissionsResponse(
		long UserId,
		IReadOnlyList<string> Permissions);
	#endregion
}
