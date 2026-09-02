using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 当前用户可访问的数据源枚举（供前端 Ask 等场景的数据源下拉选择使用）。
///
/// <para>
/// 仅返回该用户经显式授权（用户/角色授权）且已启用、属于本租户的数据源摘要
/// （<see cref="DataSourceSummaryDto"/>：id + 名称 + 类型）。无授权时返回空数组（HTTP 200），
/// 避免前端在尚未配置授权时误判为错误而闪烁。
/// </para>
///
/// <para>
/// 只读 GET 端点，不触碰 Golden 依赖文件，不影响 Golden 18/18 行为契约。租户隔离由令牌
/// <c>tid</c> 声明驱动；查询复用与 <see cref="DataSourceAuthorizationService"/> 相同的授权判定逻辑。
/// </para>
/// </summary>
[ApiController]
[Route("api/data-sources")]
public sealed class DataSourcesController : ControllerBase
{
	private readonly SuperBIContext _db;
	private readonly IIdentityService _identity;

	public DataSourcesController(SuperBIContext db, IIdentityService identity)
	{
		_db = db;
		_identity = identity;
	}

	/// <summary>列出当前用户已授权且启用的数据源（摘要）。</summary>
	[HttpGet]
	public async Task<IActionResult> List(CancellationToken cancellationToken)
	{
		var tenantId = ResolveTenantId();
		var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0)
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });

		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.DashboardView, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 dashboard:view 权限。" });

		var roleIds = await _db.UserRoles.AsNoTracking()
			.Where(x => x.TenantId == tenantId && x.UserId == userId)
			.Select(x => x.RoleId).ToListAsync(cancellationToken);

		var raw = await (
			from grant in _db.DataSourceAccessGrants.AsNoTracking()
			join source in _db.DataSources.AsNoTracking() on grant.DataSourceId equals source.Id
			where grant.TenantId == tenantId && source.TenantId == tenantId && source.Enabled == true &&
				  ((grant.SubjectType == DataSourceGrantSubjectType.User && grant.SubjectId == userId) ||
				   (grant.SubjectType == DataSourceGrantSubjectType.Role && roleIds.Contains(grant.SubjectId)))
			select new { source.Id, Name = source.Name, DbType = source.DbType }
		).Distinct().OrderBy(x => x.Id).ToListAsync(cancellationToken);

		var summaries = raw
			.Select(x => new DataSourceSummaryDto(x.Id, x.Name ?? ("数据源 " + x.Id), x.DbType ?? ""))
			.ToList();
		return Ok(summaries);
	}

	private sealed record DataSourceSummaryDto(long Id, string Name, string DbType);

	private long ResolveTenantId()
	{
		var v = User.FindFirst("tid")?.Value;
		return long.TryParse(v, out var t) ? t : 0;
	}

	private long ResolveUserId()
	{
		var v = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		return long.TryParse(v, out var u) ? u : 0;
	}
}
