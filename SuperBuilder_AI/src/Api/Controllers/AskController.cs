using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 自然语言问数端点（P11.0 旗舰 Ask BI 前置）。
///
/// <para>
/// <c>POST /api/ask</c>：接收自然语言问题，经 <see cref="IBIConversationService"/> 走完整
/// 查询理解 → 计划 → 校验 → 修复 → SQL → 执行 → 结果理解 链路，返回
/// <see cref="SuperBuilder_AI.Models.BI.BIResponse"/>。
/// </para>
///
/// <para>
/// 鉴权：需有效令牌（<see cref="AuthMiddleware"/> 保证 <c>HttpContext.User</c> 已设置）；
/// 且需 <see cref="IdentityPermissions.DashboardView"/> 权限（读 BI 能力，deny-by-default 与 P10 一致）。
/// 租户隔离由令牌中的 <c>tid</c> 声明驱动，透传给 <c>AskAsync</c> 的租户作用域。
/// 本控制器属 BI 查询面，不触碰 Golden 依赖文件，不影响 Golden 18/18 行为契约。
/// </para>
/// </summary>
[ApiController]
[Route("api/ask")]
public sealed class AskController : ControllerBase
{
	private readonly IBIConversationService _bi;
	private readonly IIdentityService _identity;

	public AskController(IBIConversationService bi, IIdentityService identity)
	{
		_bi = bi;
		_identity = identity;
	}

	/// <summary>提交一个自然语言问题并执行 BI 查询。</summary>
	[HttpPost]
	public async Task<IActionResult> Ask(
		[FromBody] AskRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });
		if (string.IsNullOrWhiteSpace(request.Question))
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "question 必填。" });

		if (User?.Identity is not { IsAuthenticated: true })
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：缺少访问令牌。" });

		var tenantId = ResolveTenantId();
		var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0)
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });

		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.DashboardView, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 dashboard:view 权限。" });

		var response = await _bi.AskAsync(request.Question, tenantId);
		return Ok(response);
	}

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

/// <summary>Ask 请求。</summary>
public sealed class AskRequest
{
	public string? Question { get; set; }
	public long DataSourceId { get; set; }
}
