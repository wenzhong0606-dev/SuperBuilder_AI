using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Caching;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.BI;

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
	private readonly IAskResponseCache? _cache;
	private readonly IDataSourceAuthorizationService? _dataSourceAuthorization;
	private readonly IRowLevelSecurityService? _rowSecurity;
	private readonly IAskConversationService _conversations;
	private readonly IAskCacheVersionProvider? _versionProvider;
	private readonly IAskAuditSink? _askAudit;

	public AskController(
		IBIConversationService bi,
		IIdentityService identity,
		IAskResponseCache? cache = null,
		IDataSourceAuthorizationService? dataSourceAuthorization = null,
		IRowLevelSecurityService? rowSecurity = null,
		IAskConversationService? conversations = null,
		IAskCacheVersionProvider? versionProvider = null,
		IAskAuditSink? askAudit = null)
	{
		_bi = bi;
		_identity = identity;
		_cache = cache;
		_dataSourceAuthorization = dataSourceAuthorization;
		_rowSecurity = rowSecurity;
		_conversations = conversations ?? new AskConversationService();
		_versionProvider = versionProvider;
		_askAudit = askAudit;
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

		// M6-02：Question 长度契约 2–2000 字符。
		var question = request.Question!;
		if (question.Length < 2 || question.Length > 2000)
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "question 长度须为 2–2000 字符。" });

		// M6-02：ConversationId 仅作长度护栏（归属由 AskConversationService 按租户/用户校验）。
		if (request.ConversationId is { Length: > 128 })
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "conversationId 长度不得超过 128 字符。" });

		if (User?.Identity is not { IsAuthenticated: true })
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：缺少访问令牌。" });

		var tenantId = ResolveTenantId();
		var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0)
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });

		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.DashboardView, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 dashboard:view 权限。" });

		var access = await ResolveDataSourceAccessAsync(tenantId, userId, request.DataSourceId, cancellationToken);
		if (access.ForbiddenResult is not null) return access.ForbiddenResult;
		var turn = _conversations.Resolve(tenantId, userId, request.ConversationId, request.Question!,
			new AskResolveContext { AuthorizedDataSourceIds = access.AuthorizedDataSourceIds });

		// P11.5.1 语义缓存：命中则直接返回，跳过整条 BI 链路（仅作用于 api/ask；Golden 走独立端点不受影响）。
		// ?noCache=1 旁路，便于联调/强制刷新。
		var bypass = BypassCache() || access.BypassCache;
		// M6-04：缓存键折叠 7 维版本上下文（权限/策略/语言/模型/语义/元数据/数据源集合），
		// 任一维度变化即不复用旧结果。未注入版本提供器时走 Legacy 分支（保留 policy 段，兼容旧测试）。
		var cacheQuestion = await BuildCacheKeyAsync(turn.StandaloneQuestion, tenantId, userId, access, cancellationToken);
		if (_cache is not null && !bypass)
		{
			var cached = _cache.Get(tenantId, cacheQuestion, access.EffectiveDataSourceId ?? 0);
			if (cached is not null)
			{
				Response.Headers["X-Cache"] = "HIT";
				return Ok(cached);
			}
			Response.Headers["X-Cache"] = "MISS";
		}

		var correlationId = Guid.NewGuid().ToString();
		BIResponse? response = null;
		string? auditError = null;
		var outcome = AskAuditOutcome.Completed;
		var sw = Stopwatch.StartNew();
		try
		{
			response = await _bi.AskAsync(
				turn.StandaloneQuestion,
				tenantId,
				access.EffectiveDataSourceId,
				access.AuthorizedDataSourceIds);

			var awaiting = response.Explanation?.Summary.RequiresConfirmation == true;
			_conversations.Record(turn.ConversationId, tenantId, userId, turn.StandaloneQuestion, awaiting,
				new AskResolveContext { AuthorizedDataSourceIds = access.AuthorizedDataSourceIds });
			response.ConversationId = turn.ConversationId;
			response.ConversationStatus = awaiting ? ConversationStatus.AwaitingClarification : ConversationStatus.Completed;
			response.RewrittenQuestion = turn.AppliedClarification ? turn.StandaloneQuestion : null;
			// M6-03：单轮行为分类 + 结构化澄清详情（仅非首问且澄清态下填充）。
			response.AskBehavior = turn.Behavior;
			if (turn.Clarification is not null) response.Clarification = turn.Clarification;
			if (_cache is not null && !bypass) _cache.Set(tenantId, cacheQuestion, access.EffectiveDataSourceId ?? 0, response);
			outcome = DeriveAuditOutcome(response, awaiting);
		}
		catch (Exception ex)
		{
			auditError = ex.Message;
			outcome = AskAuditOutcome.Failed;
			throw;
		}
		finally
		{
			sw.Stop();
			await RecordAuditSafeAsync(correlationId, turn, access, response, auditError, outcome, sw.ElapsedMilliseconds);
		}

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

		// M6-02：Refine 历史轮数护栏（最多 20 轮），避免无限拼接历史导致意图漂移。
		if (request.History is { Count: > 20 })
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "history 轮数过多（最多 20 轮）。" });

		if (User?.Identity is not { IsAuthenticated: true })
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：缺少访问令牌。" });

		var tenantId = ResolveTenantId();
		var userId = ResolveUserId();
		if (tenantId <= 0 || userId <= 0)
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });

		if (!await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.DashboardView, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 dashboard:view 权限。" });

		var access = await ResolveDataSourceAccessAsync(tenantId, userId, request.DataSourceId, cancellationToken);
		if (access.ForbiddenResult is not null) return access.ForbiddenResult;

		var composed = ComposeRefinedQuestion(request);
		if (string.IsNullOrWhiteSpace(composed))
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "缺少可用于查询的问题内容。" });

		var correlationId = Guid.NewGuid().ToString();
		BIResponse? response = null;
		string? auditError = null;
		var outcome = AskAuditOutcome.Completed;
		var sw = Stopwatch.StartNew();
		try
		{
			response = await _bi.AskAsync(
				composed,
				tenantId,
				access.EffectiveDataSourceId,
				access.AuthorizedDataSourceIds);
			outcome = DeriveAuditOutcome(response, response.Explanation?.Summary.RequiresConfirmation == true);
		}
		catch (Exception ex)
		{
			auditError = ex.Message;
			outcome = AskAuditOutcome.Failed;
			throw;
		}
		finally
		{
			sw.Stop();
			// Refine 不维护会话状态，构造最小 turn 供审计（ConversationId 空、行为 NewQuestion）。
			var refineTurn = new AskConversationTurn(string.Empty, composed, false, composed);
			await RecordAuditSafeAsync(correlationId, refineTurn, access, response, auditError, outcome, sw.ElapsedMilliseconds);
		}

		return Ok(response);
	}

	/// <summary>
	/// 从响应推导审计产出结果（M6-05）。
	/// </summary>
	private static AskAuditOutcome DeriveAuditOutcome(BIResponse response, bool awaiting)
	{
		if (!response.Success) return AskAuditOutcome.Failed;
		if (response.AskBehavior == AskBehavior.Cancel) return AskAuditOutcome.Cancelled;
		var decision = response.Explanation?.Decision?.Decision;
		if (decision == QueryPlanDecisionType.Reject) return AskAuditOutcome.Rejected;
		if (decision == QueryPlanDecisionType.RequireApproval) return AskAuditOutcome.RequiresConfirmation;
		if (awaiting || decision == QueryPlanDecisionType.AskClarification) return AskAuditOutcome.AskClarification;
		return AskAuditOutcome.Completed;
	}

	/// <summary>
	/// 安全提交 Ask 审计记录（M6-05）：审计旁路故障绝不破坏主流程返回；敏感字段已在构建器内脱敏。
	/// </summary>
	private async Task RecordAuditSafeAsync(
		string? correlationId,
		AskConversationTurn turn,
		DataSourceAccessResolution access,
		BIResponse? response,
		string? errorMessage,
		AskAuditOutcome outcome,
		long durationMs)
	{
		if (_askAudit is null) return;
		try
		{
			var record = AskAuditRecordBuilder.Build(
				correlationId, turn, access.AuthorizedDataSourceIds, response, errorMessage, outcome, durationMs, response?.SegmentTimings);
			await _askAudit.RecordAsync(record);
		}
		catch
		{
			// 审计写入失败不影响主响应。
		}
	}

	private async Task<DataSourceAccessResolution> ResolveDataSourceAccessAsync(
		long tenantId,
		long userId,
		long requestedDataSourceId,
		CancellationToken cancellationToken)
	{
		// 兼容纯单元测试和 Golden 内部路径；生产依赖注入始终提供授权服务。
		if (_dataSourceAuthorization is null)
			return new DataSourceAccessResolution(
				requestedDataSourceId > 0 ? requestedDataSourceId : null, null, null, false, null);

		var allowed = await _dataSourceAuthorization
			.GetAuthorizedDataSourceIdsAsync(tenantId, userId, cancellationToken);
		if (allowed.Count == 0 || (requestedDataSourceId > 0 && !allowed.Contains(requestedDataSourceId)))
		{
			return new DataSourceAccessResolution(null, allowed, null, true,
				StatusCode(403, new ApiError
				{
					Code = ErrorCodes.DataSourceForbidden,
					Message = ErrorCodes.Message(ErrorCodes.DataSourceForbidden)
				}));
		}

		var fingerprint = Convert.ToHexString(SHA256.HashData(
			Encoding.UTF8.GetBytes(string.Join(",", allowed.OrderBy(id => id)))));
		if (requestedDataSourceId > 0)
			return new DataSourceAccessResolution(requestedDataSourceId, allowed, fingerprint, false, null);

		// 未指定且仅有一个授权源时，可将其变成显式约束并安全缓存；多授权源下
		// 计划可能选择任一来源，因此绕过旧的 DataSourceId=0 缓存，避免撤权后复用旧答案。
		return allowed.Count == 1
			? new DataSourceAccessResolution(allowed[0], allowed, fingerprint, false, null)
			: new DataSourceAccessResolution(null, allowed, fingerprint, true, null);
	}

	/// <summary>
	/// 构建 Ask 缓存键：优先用注入的 <see cref="IAskCacheVersionProvider"/> 折叠 7 维版本上下文；
	/// 未注入时走 <see cref="AskCacheVersionContext.Legacy"/>（仅权限/策略，其余 na），兼容旧测试。
	/// </summary>
	private async Task<string> BuildCacheKeyAsync(
		string standaloneQuestion, long tenantId, long userId, DataSourceAccessResolution access, CancellationToken cancellationToken)
	{
		AskCacheVersionContext versionCtx;
		if (_versionProvider is not null)
		{
			versionCtx = await _versionProvider.ResolveAsync(
				tenantId, userId, access.AuthorizedDataSourceIds ?? Array.Empty<long>(),
				access.PermissionFingerprint, cancellationToken);
		}
		else
		{
			var policyFingerprint = _rowSecurity is null
				? "legacy"
				: await _rowSecurity.GetPolicyFingerprintAsync(tenantId, userId, cancellationToken);
			versionCtx = AskCacheVersionContext.Legacy(access.PermissionFingerprint, policyFingerprint, CultureInfo.CurrentUICulture.Name);
		}

		return versionCtx.BuildCacheKey(standaloneQuestion);
	}

	private sealed record DataSourceAccessResolution(
		long? EffectiveDataSourceId,
		IReadOnlyCollection<long>? AuthorizedDataSourceIds,
		string? PermissionFingerprint,
		bool BypassCache,
		IActionResult? ForbiddenResult);

	/// <summary>
	/// 将「原始问题 + 历史用户轮次 + 本轮细化指令」合成为一条独立可理解的自然语言问题。
	/// 以中文分号连接以保留语义并列关系；去重避免历史轮次重复拼接导致的意图漂移。
	/// </summary>
	private static readonly string[] FieldExpansionMarkers = new[]
	{
		"显示更多字段", "显示所有字段", "显示更多列", "更多列", "展开列",
		"显示全部字段", "全部字段", "详细字段", "更多字段", "多显示", "显示详细信息"
	};

	private static bool IsFieldExpansionInstruction(string? instruction)
	{
		if (string.IsNullOrWhiteSpace(instruction)) return false;
		return FieldExpansionMarkers.Any(m => instruction.Contains(m, StringComparison.OrdinalIgnoreCase));
	}

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

		// 当用户明确要求“显示更多字段”时，强化合成问题，避免 LLM 仍然只选时间字段。
		if (IsFieldExpansionInstruction(request.Instruction))
		{
			Add("请返回该表的主要业务字段，不要只返回时间字段");
		}

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

	/// <summary>
	/// 是否旁路缓存：<c>?noCache=1</c> / <c>true</c> / <c>yes</c> 视为要求强制刷新。
	/// 仅读取查询串，不影响主链路；异常时保守返回 false（不旁路）。
	/// </summary>
	private bool BypassCache()
	{
		try
		{
			if (Request.Query.TryGetValue("noCache", out var v))
			{
				var s = v.ToString().Trim().ToLowerInvariant();
				return s is "1" or "true" or "yes";
			}
		}
		catch
		{
			// 读取失败默认不旁路
		}
		return false;
	}
}

/// <summary>Ask 请求。</summary>
public sealed class AskRequest
{
	public string? Question { get; set; }
	public string? ConversationId { get; set; }
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
