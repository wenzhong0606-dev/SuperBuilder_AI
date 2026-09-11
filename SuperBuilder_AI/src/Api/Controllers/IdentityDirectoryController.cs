using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Identity 组织目录端点（M12-17）：租户内的组织 / 部门 / 用户组治理，并与 M2 已建 RBAC 关联。
///
/// <para>
/// <list type="bullet">
/// <item><c>GET/POST /api/identity-directory/organizations</c>：列举 / 创建组织。</item>
/// <item><c>GET/POST /api/identity-directory/departments</c>：列举 / 创建部门（须属本租户组织）。</item>
/// <item><c>GET/POST /api/identity-directory/user-groups</c>：列举 / 创建用户组（可同时承载角色）。</item>
/// <item><c>PUT /api/identity-directory/user-groups/{id}/roles</c>：全量替换用户组承载的角色（RBAC 关联）。</item>
/// <item><c>POST /api/identity-directory/user-groups/{id}/members</c>：添加组成员。</item>
/// <item><c>DELETE /api/identity-directory/user-groups/{id}/members/{userId}</c>：移除组成员。</item>
/// <item><c>PUT /api/identity-directory/users/{id}/department</c>：设置用户的部门归属（null 清除）。</item>
/// </list>
/// </para>
///
/// <para>
/// 安全：所有端点同时要求（1）令牌携带 <c>identity:manage</c> 权限码；（2）目标租户与认证租户一致
/// （数据面单租户恒等，跨租户 403）。读写经 <see cref="IIdentityDirectoryService"/>，确定性、不调 LLM，
/// 且不触碰 BI 查询链路与 Golden 契约数据。
/// </para>
/// </summary>
[ApiController]
[Route("api/identity-directory")]
public sealed class IdentityDirectoryController : ControllerBase
{
	private readonly IIdentityDirectoryService _directory;

	public IdentityDirectoryController(IIdentityDirectoryService directory)
	{
		_directory = directory;
	}

	/// <summary>身份治理权限门禁：缺少 <c>identity:manage</c> 直接 403（服务端强制，非仅前端隐藏）。</summary>
	private IActionResult? RequireIdentityManage()
	{
		if (!User.HasClaim("perm", IdentityPermissions.IdentityManage))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = $"禁止：缺少 {IdentityPermissions.IdentityManage} 权限。" });
		return null;
	}

	/// <summary>在当前请求作用域内开启租户隔离，返回解析出的租户 Id（跨租户显式请求直接拒绝）。</summary>
	private long ScopeTo(long requestedTenantId)
	{
		var resolution = TenantDataPlanePolicy.ResolvePlatformScope(User, requestedTenantId);
		TenantDataPlanePolicy.StorePlatformScope(HttpContext, resolution, "IdentityDirectory");
		if (!resolution.Authorized)
			throw new SuperBuilderException(
				ErrorCodes.TenantIsolated,
				"禁止：租户作用域请求只能访问认证租户的数据，跨租户访问被拒绝。",
				403);
		return resolution.EffectiveTenantId;
	}

	// ---------------- 组织 ----------------

	/// <summary>列举租户内组织（支持分页；pageSize &lt;= 0 表示不分页，返回全量）。</summary>
	[HttpGet("organizations")]
	public async Task<IActionResult> ListOrganizations(
		[FromQuery] long tenantId = 0,
		[FromQuery] int page = 0,
		[FromQuery] int pageSize = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		var tid = ScopeTo(tenantId);
		return Ok(await _directory.ListOrganizationsAsync(tid, page, pageSize, cancellationToken));
	}

	/// <summary>创建组织。</summary>
	[HttpPost("organizations")]
	public async Task<IActionResult> CreateOrganization(
		[FromBody] CreateOrganizationRequest request,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		if (request is null) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });
		if (request.TenantId <= 0) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "tenantId 必须 > 0。" });

		var tid = ScopeTo(request.TenantId);
		var result = await _directory.CreateOrganizationAsync(tid, request.Code, request.Name, request.Description, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return StatusCode(201, new { id = result.Id });
	}

	/// <summary>M12 增量：重命名 / 修改组织描述。</summary>
	[HttpPut("organizations/{id:long}")]
	public async Task<IActionResult> UpdateOrganization(
		long id,
		[FromBody] UpdateOrganizationRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		if (request is null) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });

		var tid = ScopeTo(tenantId);
		var result = await _directory.UpdateOrganizationAsync(tid, id, request.Name, request.Description, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return Ok(new { id });
	}

	/// <summary>M12 增量：启用 / 停用组织。</summary>
	[HttpPut("organizations/{id:long}/enabled")]
	public async Task<IActionResult> SetOrganizationEnabled(
		long id,
		[FromBody] SetEnabledRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		if (request is null) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });

		var tid = ScopeTo(tenantId);
		var result = await _directory.SetOrganizationEnabledAsync(tid, id, request.Enabled, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return Ok(new { id, enabled = request.Enabled });
	}

	/// <summary>M12 增量：删除组织（其下仍有部门时拒绝）。</summary>
	[HttpDelete("organizations/{id:long}")]
	public async Task<IActionResult> DeleteOrganization(
		long id,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;

		var tid = ScopeTo(tenantId);
		var result = await _directory.DeleteOrganizationAsync(tid, id, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return NoContent();
	}

	// ---------------- 部门 ----------------

	/// <summary>列举租户内部门（支持分页；pageSize &lt;= 0 表示不分页）。</summary>
	[HttpGet("departments")]
	public async Task<IActionResult> ListDepartments(
		[FromQuery] long tenantId = 0,
		[FromQuery] int page = 0,
		[FromQuery] int pageSize = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		var tid = ScopeTo(tenantId);
		return Ok(await _directory.ListDepartmentsAsync(tid, page, pageSize, cancellationToken));
	}

	/// <summary>创建部门（须隶属同租户组织）。</summary>
	[HttpPost("departments")]
	public async Task<IActionResult> CreateDepartment(
		[FromBody] CreateDepartmentRequest request,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		if (request is null) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });
		if (request.TenantId <= 0) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "tenantId 必须 > 0。" });

		var tid = ScopeTo(request.TenantId);
		var result = await _directory.CreateDepartmentAsync(
			tid, request.OrganizationId, request.ParentId, request.Code, request.Name, request.Description, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return StatusCode(201, new { id = result.Id });
	}

	/// <summary>M12 增量： renaming / 修改部门描述，并可改挂组织或上级部门。</summary>
	[HttpPut("departments/{id:long}")]
	public async Task<IActionResult> UpdateDepartment(
		long id,
		[FromBody] UpdateDepartmentRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		if (request is null) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });

		var tid = ScopeTo(tenantId);
		var result = await _directory.UpdateDepartmentAsync(
			tid, id, request.Name, request.Description, request.OrganizationId, request.ParentId, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return Ok(new { id });
	}

	/// <summary>M12 增量：启用 / 停用部门。</summary>
	[HttpPut("departments/{id:long}/enabled")]
	public async Task<IActionResult> SetDepartmentEnabled(
		long id,
		[FromBody] SetEnabledRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		if (request is null) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });

		var tid = ScopeTo(tenantId);
		var result = await _directory.SetDepartmentEnabledAsync(tid, id, request.Enabled, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return Ok(new { id, enabled = request.Enabled });
	}

	/// <summary>M12 增量：删除部门（仍有成员或子部门时拒绝）。</summary>
	[HttpDelete("departments/{id:long}")]
	public async Task<IActionResult> DeleteDepartment(
		long id,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;

		var tid = ScopeTo(tenantId);
		var result = await _directory.DeleteDepartmentAsync(tid, id, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return NoContent();
	}

	// ---------------- 用户组 ----------------

	/// <summary>列举租户内用户组（支持分页；pageSize &lt;= 0 表示不分页）。</summary>
	[HttpGet("user-groups")]
	public async Task<IActionResult> ListUserGroups(
		[FromQuery] long tenantId = 0,
		[FromQuery] int page = 0,
		[FromQuery] int pageSize = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		var tid = ScopeTo(tenantId);
		return Ok(await _directory.ListUserGroupsAsync(tid, page, pageSize, cancellationToken));
	}

	/// <summary>创建用户组，可同时承载角色（角色可为全局）。</summary>
	[HttpPost("user-groups")]
	public async Task<IActionResult> CreateUserGroup(
		[FromBody] CreateUserGroupRequest request,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		if (request is null) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });
		if (request.TenantId <= 0) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "tenantId 必须 > 0。" });

		var tid = ScopeTo(request.TenantId);
		var result = await _directory.CreateUserGroupAsync(
			tid, request.Code, request.Name, request.Description, request.RoleCodes, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return StatusCode(201, new { id = result.Id });
	}

	/// <summary>M12 增量：重命名 / 修改用户组描述。</summary>
	[HttpPut("user-groups/{id:long}")]
	public async Task<IActionResult> UpdateUserGroup(
		long id,
		[FromBody] UpdateUserGroupRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		if (request is null) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });

		var tid = ScopeTo(tenantId);
		var result = await _directory.UpdateUserGroupAsync(tid, id, request.Name, request.Description, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return Ok(new { id });
	}

	/// <summary>M12 增量：启用 / 停用用户组（停用后其承载角色不再计入成员有效权限）。</summary>
	[HttpPut("user-groups/{id:long}/enabled")]
	public async Task<IActionResult> SetUserGroupEnabled(
		long id,
		[FromBody] SetEnabledRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		if (request is null) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });

		var tid = ScopeTo(tenantId);
		var result = await _directory.SetUserGroupEnabledAsync(tid, id, request.Enabled, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return Ok(new { id, enabled = request.Enabled });
	}

	/// <summary>M12 增量：删除用户组（级联清理成员与角色关联）。</summary>
	[HttpDelete("user-groups/{id:long}")]
	public async Task<IActionResult> DeleteUserGroup(
		long id,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;

		var tid = ScopeTo(tenantId);
		var result = await _directory.DeleteUserGroupAsync(tid, id, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return NoContent();
	}

	/// <summary>全量替换用户组承载的角色集（RBAC 关联）。</summary>
	[HttpPut("user-groups/{id:long}/roles")]
	public async Task<IActionResult> SetUserGroupRoles(
		long id,
		[FromBody] SetUserGroupRolesRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		if (request is null) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });

		var tid = ScopeTo(tenantId);
		var result = await _directory.SetUserGroupRolesAsync(tid, id, request.RoleCodes, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return Ok(new { userGroupId = id, roles = request.RoleCodes ?? Array.Empty<string>() });
	}

	/// <summary>添加组成员（幂等）。</summary>
	[HttpPost("user-groups/{id:long}/members")]
	public async Task<IActionResult> AddUserGroupMember(
		long id,
		[FromBody] AddGroupMemberRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		if (request is null) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });
		if (request.UserId <= 0) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "userId 必填。" });

		var tid = ScopeTo(tenantId);
		var result = await _directory.AddUserGroupMemberAsync(tid, id, request.UserId, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return Ok(new { userGroupId = id, userId = request.UserId, added = true });
	}

	/// <summary>移除组成员（幂等）。</summary>
	[HttpDelete("user-groups/{id:long}/members/{userId:long}")]
	public async Task<IActionResult> RemoveUserGroupMember(
		long id,
		long userId,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;

		var tid = ScopeTo(tenantId);
		var result = await _directory.RemoveUserGroupMemberAsync(tid, id, userId, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return NoContent();
	}

	// ---------------- 用户归属 ----------------

	/// <summary>设置用户的部门归属（departmentId 为 null 时清除归属）。</summary>
	[HttpPut("users/{id:long}/department")]
	public async Task<IActionResult> SetUserDepartment(
		long id,
		[FromBody] SetUserDepartmentRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (RequireIdentityManage() is { } denied) return denied;
		if (request is null) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });

		var tid = ScopeTo(tenantId);
		var result = await _directory.SetUserDepartmentAsync(tid, id, request.DepartmentId, cancellationToken);
		if (!result.Success) return BadRequest(new { errors = result.Errors });
		return Ok(new { userId = id, departmentId = request.DepartmentId });
	}

	#region Request DTOs
	/// <summary>创建组织请求体。</summary>
	public sealed record CreateOrganizationRequest(long TenantId, string Code, string? Name = null, string? Description = null);

	/// <summary>创建部门请求体。</summary>
	public sealed record CreateDepartmentRequest(
		long TenantId, long OrganizationId, long? ParentId, string Code, string? Name = null, string? Description = null);

	/// <summary>创建用户组请求体（可同时承载角色）。</summary>
	public sealed record CreateUserGroupRequest(
		long TenantId, string Code, string? Name = null, string? Description = null, string[]? RoleCodes = null);

	/// <summary>替换用户组角色集请求体。</summary>
	public sealed record SetUserGroupRolesRequest(string[]? RoleCodes = null);

	/// <summary>添加组成员请求体。</summary>
	public sealed record AddGroupMemberRequest(long UserId);

	/// <summary>设置用户部门归属请求体。</summary>
	public sealed record SetUserDepartmentRequest(long? DepartmentId);

	/// <summary>M12 增量：重命名 / 修改组织描述请求体（编码不可变）。</summary>
	public sealed record UpdateOrganizationRequest(string? Name = null, string? Description = null);

	/// <summary>M12 增量：重命名 / 修改部门描述请求体（可改挂组织与上级部门）。</summary>
	public sealed record UpdateDepartmentRequest(
		string? Name = null, string? Description = null, long? OrganizationId = null, long? ParentId = null);

	/// <summary>M12 增量：重命名 / 修改用户组描述请求体。</summary>
	public sealed record UpdateUserGroupRequest(string? Name = null, string? Description = null);

	/// <summary>M12 增量：启用 / 停用请求体。</summary>
	public sealed record SetEnabledRequest(bool Enabled);
	#endregion
}
