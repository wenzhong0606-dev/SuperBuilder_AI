using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Controllers;

/// <summary>租户内 DataSource 用户/角色显式授权管理。</summary>
[ApiController]
[Route("api/data-source-access")]
public sealed class DataSourceAccessController : ControllerBase
{
	private readonly SuperBIContext _db;
	private readonly IIdentityService _identity;
	private readonly IDataSourceAuthorizationService _authorization;

	public DataSourceAccessController(
		SuperBIContext db,
		IIdentityService identity,
		IDataSourceAuthorizationService authorization)
	{
		_db = db;
		_identity = identity;
		_authorization = authorization;
	}

	[HttpGet]
	public async Task<IActionResult> List([FromQuery] long dataSourceId, CancellationToken cancellationToken)
	{
		var scope = await AuthorizeManagementAsync(cancellationToken);
		if (scope.Error is not null) return scope.Error;
		var grants = await _db.DataSourceAccessGrants.AsNoTracking()
			.Where(x => x.TenantId == scope.TenantId && x.DataSourceId == dataSourceId)
			.OrderBy(x => x.SubjectType).ThenBy(x => x.SubjectId)
			.ToListAsync(cancellationToken);
		return Ok(grants);
	}

	[HttpPost]
	public async Task<IActionResult> Grant([FromBody] DataSourceAccessRequest request, CancellationToken cancellationToken)
	{
		var scope = await AuthorizeManagementAsync(cancellationToken);
		if (scope.Error is not null) return scope.Error;
		if (request is null || request.DataSourceId <= 0 || request.SubjectId <= 0 ||
			!Enum.IsDefined(request.SubjectType))
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "数据源和授权主体参数无效。" });

		try
		{
			await _authorization.GrantAsync(scope.TenantId, request.DataSourceId, request.SubjectType, request.SubjectId, cancellationToken);
			return NoContent();
		}
		catch (InvalidOperationException)
		{
			// 统一 403，避免利用管理接口枚举其他租户的数据源或主体。
			return StatusCode(403, new ApiError { Code = ErrorCodes.DataSourceForbidden, Message = ErrorCodes.Message(ErrorCodes.DataSourceForbidden) });
		}
	}

	[HttpDelete]
	public async Task<IActionResult> Revoke([FromQuery] long dataSourceId, [FromQuery] DataSourceGrantSubjectType subjectType, [FromQuery] long subjectId, CancellationToken cancellationToken)
	{
		var scope = await AuthorizeManagementAsync(cancellationToken);
		if (scope.Error is not null) return scope.Error;
		await _authorization.RevokeAsync(scope.TenantId, dataSourceId, subjectType, subjectId, cancellationToken);
		return NoContent();
	}

	private async Task<(long TenantId, IActionResult? Error)> AuthorizeManagementAsync(CancellationToken cancellationToken)
	{
		var tenantValue = User.FindFirst("tid")?.Value;
		var userValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (!long.TryParse(tenantValue, out var tenantId) || !long.TryParse(userValue, out var userId) || tenantId <= 0 || userId <= 0)
			return (0, Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" }));

		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.IdentityManage, cancellationToken))
			return (tenantId, StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 identity:manage 权限。" }));

		_db.ApplyTenantScope(tenantId);
		return (tenantId, null);
	}
}

public sealed class DataSourceAccessRequest
{
	public long DataSourceId { get; set; }
	public DataSourceGrantSubjectType SubjectType { get; set; }
	public long SubjectId { get; set; }
}
