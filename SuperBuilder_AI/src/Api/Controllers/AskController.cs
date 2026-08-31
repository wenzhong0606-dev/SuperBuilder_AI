using System;
using System.Collections.Generic;
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

	/// <summary>
	/// 多轮语义调整（P11.3）。
	///
	/// <para>
	/// 在「原始问题 + 历史轮次 + 本轮细化指令」的基础上合成一条独立可理解的自然语言问题，
	/// 再复用既有的完整 BI 链路（理解 → 计划 → 校验 → 修复 → 置信度 → 闸门 → SQL → 执行 → 结果理解）。
	/// 指代与省略的补全交由 QueryUnderstanding 阶段的 LLM 完成（如「只看华东地区」作用于上一轮问题）。
	/// </para>
	///
	/// <para>
	/// <b>门控隔离（零回归保证）</b>：仅在客户端显式调用 <c>POST api/ask/refine</c> 时启用；
	/// 默认 <c>POST api/ask</c> 路径逐字节不变，不新增任何全局中间件或管道分支，
	/// 不触碰 Golden 依赖文件，Golden 18/18 行为契约不受影响。
	/// </para>
	/// </summary>
	[HttpPost("refine")]
	public async Task<IActionResult> Refine(
		[FromBody] AskRefineRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null)
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "请求体不能为空。" });

		if (string.IsNullOrWhiteSpace(request.Instruction))
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "instruction 必填：请提供本轮细化指令（如「只看华东地区」）。" });

		if (User?.Identity is not { IsAuthenticated: true })
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：缺少访问令牌。" });

		var tenantId = ResolveTenantId();
		var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0)
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });

		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.DashboardView, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 dashboard:view 权限。" });

		var composed = ComposeRefinedQuestion(request);
		if (string.IsNullOrWhiteSpace(composed))
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "缺少可用于查询的问题内容。" });

		var response = await _bi.AskAsync(composed, tenantId);
		return Ok(response);
	}

	/// <summary>
	/// 将「原始问题 + 历史用户轮次 + 本轮细化指令」合成为一条独立可理解的自然语言问题。
	/// 以中文分号连接以保留语义并列关系；去重避免历史轮次重复拼接导致的意图漂移。
	/// </summary>
	private static string ComposeRefinedQuestion(AskRefineRequest request)
	{
		var parts = new List<string>();

		void Add(string? text)
		{
			if (string.IsNullOrWhiteSpace(text)) return;
			var value = text.Trim();
			if (!parts.Contains(value)) parts.Add(value);
		}

		Add(request.Question);

		if (request.History is not null)
		{
			foreach (var turn in request.History)
			{
				// 仅用户轮次参与合成：助手轮次是答案而非意图，并入会污染 QueryUnderstanding。
				if (turn is not null && string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase))
					Add(turn.Content);
			}
		}

		Add(request.Instruction);

		return string.Join("；", parts);
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

/// <summary>
/// Ask 多轮语义调整请求（P11.3）。
/// </summary>
public sealed class AskRefineRequest
{
	/// <summary>原始问题。可空——仅凭历史轮次 + 本轮指令亦可发起细化。</summary>
	public string? Question { get; set; }

	/// <summary>本轮细化指令，必填。如「只看华东地区」「改成按月统计」。</summary>
	public string? Instruction { get; set; }

	/// <summary>可选对话历史，用于补全指代（仅 role=user 的轮次参与问题合成）。</summary>
	public List<AskRefineTurn>? History { get; set; }

	public long DataSourceId { get; set; }
}

/// <summary>多轮调整中的一轮对话。</summary>
public sealed class AskRefineTurn
{
	/// <summary>角色：<c>user</c> 或 <c>assistant</c>。仅 <c>user</c> 参与问题合成。</summary>
	public string Role { get; set; } = "user";

	public string? Content { get; set; }
}
