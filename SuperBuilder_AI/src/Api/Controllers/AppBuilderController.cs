using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.AppBuilder;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Theme;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// AI 应用构建端点（P8.3 AI App Builder + M7-11 应用运行时）。
///
/// <para>
/// 提供应用（结构化 AppDsl 文档）的租户作用域 CRUD、从自然语言描述生成应用、从 Ask 结果生成应用（M7-11），
/// 以及编辑器蓝图与运行时 render/preview：
/// </para>
/// </summary>
[ApiController]
[Route("api/apps")]
public sealed class AppBuilderController : ControllerBase
{
	private readonly SuperBIContext _db;
	private readonly IAppDslSerializer _dslSerializer;
	private readonly IAppBuilderAgent _agent;
	private readonly IAppQueryBindingExporter _bindingExporter;
	private readonly IAppQueryExecutor _executor;
	private readonly IDataSourceAuthorizationService _dataSourceAuth;

	public AppBuilderController(
		SuperBIContext db,
		IAppDslSerializer dslSerializer,
		IAppBuilderAgent agent,
		IAppQueryBindingExporter bindingExporter,
		IAppQueryExecutor executor,
		IDataSourceAuthorizationService dataSourceAuth)
	{
		_db = db;
		_dslSerializer = dslSerializer;
		_agent = agent;
		_bindingExporter = bindingExporter;
		_executor = executor;
		_dataSourceAuth = dataSourceAuth;
	}

	private long ScopeTo(long requestedTenantId)
	{
		var resolution = TenantDataPlanePolicy.ResolvePlatformScope(User, requestedTenantId);
		TenantDataPlanePolicy.StorePlatformScope(HttpContext, resolution, "App");
		if (!resolution.Authorized)
			throw new SuperBuilderException(
				ErrorCodes.TenantIsolated,
				"禁止：租户作用域请求只能访问认证租户的数据，跨租户访问被拒绝。",
				403);
		_db.ApplyTenantScope(resolution.EffectiveTenantId);
		return resolution.EffectiveTenantId;
	}

	/// <summary>权限校验：缺 claim 即 403；通过返回 null。</summary>
	private IActionResult? Require(string permission) =>
		User.Identity?.IsAuthenticated == true && !User.HasClaim("perm", permission)
			? StatusCode(403, new ApiError
			{
				Code = ErrorCodes.AppForbidden,
				Message = $"禁止：缺少 {permission} 权限。",
				Decision = ErrorDecisions.PermissionDenied,
			})
			: null;

	private IActionResult NotFoundApp() =>
		StatusCode(StatusCodes.Status404NotFound, new ApiError
		{
			Code = ErrorCodes.AppNotFound,
			Message = ErrorCodes.Message(ErrorCodes.AppNotFound),
		});

	private IActionResult ConflictApp(string code, string? decision, string message) =>
		StatusCode(StatusCodes.Status409Conflict, new ApiError
		{
			Code = code,
			Message = message,
			Decision = decision,
		});

	private string? IdempotencyKey() =>
		Request.Headers.TryGetValue("Idempotency-Key", out var v) ? v.ToString() : null;

	private long ResolveUserId() =>
		long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var u) ? u : 0;

	private async Task<IActionResult?> ValidateThemeAsync(long tenantId, string? themeKey, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(themeKey) || themeKey == BuiltInThemeKeys.Default) return null;
		var accessible = await _db.Themes.IgnoreQueryFilters()
			.AnyAsync(t => t.Key == themeKey && (t.TenantId == tenantId || t.TenantId == 0), ct);
		return accessible ? null : BadRequest(new { errors = new[] { $"主题不可用或不属于当前租户：{themeKey}。" } });
	}

	/// <summary>创建应用：从结构化 DSL 编排（默认路径，确定性、不调 LLM）。需 app:create。</summary>
	[HttpPost]
	public async Task<IActionResult> Create(
		[FromBody] CreateAppRequest request,
		CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppCreate) is { } denied) return denied;
		if (request is null) return BadRequest("请求体不能为空。");
		if (request.TenantId <= 0) return BadRequest("不能通过 API 创建全局/内置应用（TenantId 必须 > 0）。");
		if (string.IsNullOrWhiteSpace(request.DslJson)) return BadRequest("DslJson 不能为空。");

		if (!_dslSerializer.TryDeserialize(request.DslJson, out var dsl, out var errors) || dsl is null)
			return BadRequest(new { errors });

		var result = await _agent.BuildFromDslAsync(request.TenantId, dsl, request.Code);
		if (!result.Success || result.Plan is null)
			return BadRequest(new { errors = result.Errors });

		var plan = result.Plan;
		var tenantId = ScopeTo(request.TenantId);
		plan.TenantId = tenantId;
		if (await ValidateThemeAsync(tenantId, plan.ThemeKey, cancellationToken) is { } themeDenied) return themeDenied;

		var conflict = await _db.AppPlans
			.IgnoreQueryFilters()
			.AnyAsync(p => p.Code == plan.Code && (p.TenantId == tenantId || p.TenantId == 0), cancellationToken);
		if (conflict) return Conflict(new { errors = new[] { $"应用编码已存在：{plan.Code}。" } });

		_db.AppPlans.Add(plan);
		await _db.SaveChangesAsync(cancellationToken);

		return CreatedAtAction(nameof(GetByCode), new { code = plan.Code, tenantId }, ToDetail(plan));
	}

	/// <summary>从自然语言描述生成应用（非默认路径）：描述 → Qwen 生成 DSL → 校验 → AppPlan。需 app:create。</summary>
	[HttpPost("generate")]
	public async Task<IActionResult> Generate(
		[FromBody] GenerateAppRequest request,
		CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppCreate) is { } denied) return denied;
		if (request is null) return BadRequest("请求体不能为空。");
		if (request.TenantId <= 0) return BadRequest("生成应用需要 tenantId > 0。");
		if (string.IsNullOrWhiteSpace(request.Description)) return BadRequest("应用描述不能为空。");

		var result = await _agent.GenerateFromDescriptionAsync(request.TenantId, request.Description, request.Code);
		if (!result.Success || result.Plan is null)
			return result.UsedAi
				? StatusCode(StatusCodes.Status502BadGateway, new { errors = result.Errors, usedAi = true })
				: BadRequest(new { errors = result.Errors });

		var plan = result.Plan;
		var tenantId = ScopeTo(request.TenantId);
		plan.TenantId = tenantId;
		if (!string.IsNullOrWhiteSpace(request.ThemeKey))
		{
			if (!_dslSerializer.TryDeserialize(plan.DslJson, out var generatedDsl, out var generatedErrors) || generatedDsl is null)
				return BadRequest(new { errors = generatedErrors });
			generatedDsl.ThemeKey = request.ThemeKey;
			plan.ThemeKey = request.ThemeKey;
			plan.DslJson = _dslSerializer.Serialize(generatedDsl);
		}
		if (await ValidateThemeAsync(tenantId, plan.ThemeKey, cancellationToken) is { } themeDenied) return themeDenied;

		var conflict = await _db.AppPlans
			.IgnoreQueryFilters()
			.AnyAsync(p => p.Code == plan.Code && (p.TenantId == tenantId || p.TenantId == 0), cancellationToken);
		if (conflict) return Conflict(new { errors = new[] { $"应用编码已存在：{plan.Code}。" } });

		_db.AppPlans.Add(plan);
		await _db.SaveChangesAsync(cancellationToken);

		return CreatedAtAction(nameof(GetByCode), new { code = plan.Code, tenantId }, ToDetail(plan));
	}

	/// <summary>M7-11：从 Ask 成功查询快照生成应用（确定性绑定，不调 LLM）。需 app:create，且快照须为调用者本人创建。</summary>
	[HttpPost("from-ask")]
	public async Task<IActionResult> CreateFromAsk(
		[FromBody] CreateFromAskRequest request,
		CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppCreate) is { } denied) return denied;
		if (request is null) return BadRequest("请求体不能为空。");
		if (string.IsNullOrWhiteSpace(request.TurnId)) return BadRequest("turnId 必填（来自 api/ask 返回的 TurnId）。");
		if (request.TenantId <= 0) return BadRequest("tenantId 必须 > 0。");

		var tenantId = ScopeTo(request.TenantId);
		var userId = ResolveUserId();
		if (userId <= 0) return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });

		AppDataSourceBinding binding;
		try
		{
			binding = await _bindingExporter.ExportAsync(request.TurnId, tenantId, userId, cancellationToken);
		}
		catch (SuperBuilderException ex)
		{
			return StatusCode(ex.StatusCode, new ApiError { Code = ex.ErrorCode, Message = ex.Message });
		}

		var code = request.Code ?? $"ask-{tenantId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
		var name = request.Name ?? "Ask 生成的应用";
		var componentType = binding.Metrics.Count > 0 ? AppComponentTypes.Chart : AppComponentTypes.Table;
		var props = componentType == AppComponentTypes.Chart
			? new Dictionary<string, string> { { "chartType", "bar" }, { "categoryField", binding.Dimensions.FirstOrDefault() ?? binding.Entity ?? "x" } }
			: new Dictionary<string, string>();
		var dsl = new AppDsl
		{
			Version = AppDslVersions.Current,
			Code = code,
			Name = name,
			ThemeKey = request.ThemeKey,
			Pages = new List<PagePlan>
			{
				new()
				{
					Id = "home",
					Name = "首页",
					Order = 1,
					Components = new List<ComponentPlan>
					{
						new()
						{
							Type = componentType,
							Id = "c1",
							Title = name,
							Order = 1,
							Binding = binding,
							Properties = props,
						},
					},
				},
			},
		};

		var result = await _agent.BuildFromDslAsync(tenantId, dsl, code);
		if (!result.Success || result.Plan is null)
			return BadRequest(new { errors = result.Errors });

		var plan = result.Plan;
		plan.TenantId = tenantId;
		if (await ValidateThemeAsync(tenantId, plan.ThemeKey, cancellationToken) is { } themeDenied) return themeDenied;

		var conflict = await _db.AppPlans
			.IgnoreQueryFilters()
			.AnyAsync(p => p.Code == plan.Code && (p.TenantId == tenantId || p.TenantId == 0), cancellationToken);
		if (conflict) return Conflict(new { errors = new[] { $"应用编码已存在：{plan.Code}。" } });

		_db.AppPlans.Add(plan);
		await _db.SaveChangesAsync(cancellationToken);

		return CreatedAtAction(nameof(GetByCode), new { code = plan.Code, tenantId }, ToDetail(plan));
	}

	/// <summary>列表：按租户作用域返回（含全局模板 TenantId=0），分页信封。需 app:view。</summary>
	[HttpGet]
	public async Task<IActionResult> List(
		[FromQuery] long tenantId = 0,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? sort = null,
		CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppView) is { } denied) return denied;
		ScopeTo(tenantId);

		var query = _db.AppPlans.AsNoTracking();
		query = sort switch
		{
			"updatedAt.desc" => query.OrderByDescending(p => p.UpdatedTime),
			"updatedAt.asc" => query.OrderBy(p => p.UpdatedTime),
			_ => query.OrderBy(p => p.TenantId).ThenBy(p => p.Code),
		};

		var total = await query.CountAsync(cancellationToken);
		var items = await query
			.Skip(System.Math.Max(0, (page - 1) * pageSize))
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		var canEdit = User.HasClaim("perm", IdentityPermissions.AppEdit);
		return Ok(new AppListResult(items.Select(p => ToSummary(p, canEdit)).ToList(), total, page, pageSize));
	}

	/// <summary>获取单个应用。app:view 仅返回发布数据；app:edit 额外返回草稿 DSL（服务端裁剪，不靠前端隐藏）。</summary>
	[HttpGet("{code}")]
	public async Task<IActionResult> GetByCode(
		string code,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppView) is { } denied) return denied;
		if (string.IsNullOrWhiteSpace(code)) return BadRequest("应用编码不能为空。");
		ScopeTo(tenantId);

		var entity = await _db.AppPlans.AsNoTracking()
			.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFoundApp();

		var canEdit = User.HasClaim("perm", IdentityPermissions.AppEdit);
		var dslJson = canEdit ? entity.DslJson : (entity.PublishedDslJson ?? string.Empty);
		return Ok(new AppDetail(
			entity.Id, entity.TenantId, entity.Code, entity.Name, entity.Description, entity.Status,
			entity.DslVersion, entity.ThemeKey, dslJson, entity.PublishedVersion, entity.PublishedAt,
			entity.CreatedTime, entity.UpdatedTime,
			canEdit ? entity.DraftRevision : (int?)null, entity.PublishedDslJson));
	}

	/// <summary>更新应用：重新校验 DSL 后覆盖文档列。需 app:edit。</summary>
	[HttpPut("{code}")]
	public async Task<IActionResult> Update(
		string code,
		[FromBody] UpdateAppRequest request,
		[FromQuery] int? expectedDraftRevision = null,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppEdit) is { } denied) return denied;
		if (request is null) return BadRequest("请求体不能为空。");
		if (string.IsNullOrWhiteSpace(request.DslJson)) return BadRequest("DslJson 不能为空。");

		if (!_dslSerializer.TryDeserialize(request.DslJson, out var dsl, out var errors) || dsl is null)
			return BadRequest(new { errors });

		ScopeTo(tenantId);
		var entity = await _db.AppPlans.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFoundApp();
		if (entity.TenantId == 0)
			return BadRequest(new { errors = new[] { "全局/内置应用不可修改。" } });
		if (expectedDraftRevision is not null && entity.DraftRevision != expectedDraftRevision)
			return ConflictApp(ErrorCodes.AppDraftChanged, null, ErrorCodes.Message(ErrorCodes.AppDraftChanged));

		var result = await _agent.BuildFromDslAsync(entity.TenantId, dsl, entity.Code);
		if (!result.Success || result.Plan is null)
			return BadRequest(new { errors = result.Errors });
		if (await ValidateThemeAsync(entity.TenantId, result.Plan.ThemeKey, cancellationToken) is { } themeDenied) return themeDenied;

		entity.Name = result.Plan.Name;
		entity.Description = result.Plan.Description;
		entity.DslVersion = result.Plan.DslVersion;
		entity.DslJson = result.Plan.DslJson;
		entity.ThemeKey = result.Plan.ThemeKey;
		entity.DraftRevision += 1;

		await _db.SaveChangesAsync(cancellationToken);
		return Ok(ToDetail(entity));
	}

	/// <summary>删除应用。需 app:delete。</summary>
	[HttpDelete("{code}")]
	public async Task<IActionResult> Delete(
		string code,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppDelete) is { } denied) return denied;
		if (string.IsNullOrWhiteSpace(code)) return BadRequest("应用编码不能为空。");
		ScopeTo(tenantId);
		var entity = await _db.AppPlans.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFoundApp();
		if (entity.TenantId == 0)
			return BadRequest(new { errors = new[] { "全局/内置应用不可删除。" } });

		_db.AppPlans.Remove(entity);
		await _db.SaveChangesAsync(cancellationToken);
		return NoContent();
	}

	/// <summary>发布应用（M7-02）：草稿固化为发布快照。需 app:publish。支持 Idempotency-Key 与期望草稿版本并发保护（§7/§10.9/§14）。</summary>
	[HttpPost("{code}/publish")]
	public async Task<IActionResult> Publish(
		string code,
		[FromQuery] int? expectedDraftRevision = null,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppPublish) is { } denied) return denied;
		var tid = ScopeTo(tenantId);
		var entity = await _db.AppPlans.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFoundApp();
		if (string.IsNullOrWhiteSpace(entity.DslJson))
			return BadRequest(new { errors = new[] { "草稿 DSL 为空，无法发布。" } });
		if (await ValidateThemeAsync(tid, entity.ThemeKey, cancellationToken) is { } themeDenied) return themeDenied;

		// 期望草稿版本校验（乐观并发令牌；未提供则跳过，兼容既有调用方）。
		if (expectedDraftRevision is not null && entity.DraftRevision != expectedDraftRevision)
			return ConflictApp(ErrorCodes.AppDraftChanged, null, ErrorCodes.Message(ErrorCodes.AppDraftChanged));

		var key = IdempotencyKey();
		if (!string.IsNullOrWhiteSpace(key))
		{
			var prior = await _db.AppPublishIdempotencies.AsNoTracking()
				.FirstOrDefaultAsync(x => x.TenantId == tid && x.AppCode == code && x.IdempotencyKey == key, cancellationToken);
			if (prior is not null)
			{
				// 同键重入：期望版本一致（或未提供）返回既有版本，不重复发布。
				if (expectedDraftRevision is null || prior.ExpectedDraftRevision == expectedDraftRevision)
					return Ok(new PublishResult(entity.Id, tid, prior.PublishedVersion, entity.PublishedAt, entity.PublishedBy));
				return ConflictApp(ErrorCodes.AppIdempotencyConflict, null, ErrorCodes.Message(ErrorCodes.AppIdempotencyConflict));
			}
		}

		var newVersion = entity.PublishedVersion + 1;
		entity.PublishedDslJson = entity.DslJson;
		entity.PublishedVersion = newVersion;
		entity.Status = AppStatuses.Published;
		entity.PublishedAt = DateTime.UtcNow;
		entity.PublishedBy = Actor();

		_db.AppVersions.Add(new AppVersion
		{
			AppId = entity.Id,
			TenantId = tid,
			Version = newVersion,
			Code = entity.Code,
			Name = entity.Name,
			Description = entity.Description,
			ThemeKey = entity.ThemeKey,
			DslVersion = entity.DslVersion,
			DslJson = entity.DslJson,
			PublishedAt = entity.PublishedAt.Value,
			PublishedBy = entity.PublishedBy,
			RolledBackFromVersion = null,
		});

		if (!string.IsNullOrWhiteSpace(key))
			_db.AppPublishIdempotencies.Add(new AppPublishIdempotency
			{
				TenantId = tid,
				AppCode = code,
				IdempotencyKey = key,
				ExpectedDraftRevision = expectedDraftRevision ?? entity.DraftRevision,
				PublishedVersion = newVersion,
			});

		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException)
		{
			// 并发发布：唯一索引 (AppId,Version) 兜底，避免重复版本号（§14 P0-3）。
			return ConflictApp(ErrorCodes.AppIdempotencyConflict, null, "并发发布冲突，请重试或确认幂等键。");
		}
		return Ok(new PublishResult(entity.Id, tid, newVersion, entity.PublishedAt, entity.PublishedBy));
	}

	/// <summary>回滚应用（M7-02）：指定历史版本恢复为当前发布态。需 app:publish。支持 Idempotency-Key（§7/§10.10/§14）。</summary>
	[HttpPost("{code}/rollback/{version:int}")]
	public async Task<IActionResult> Rollback(
		string code,
		int version,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppPublish) is { } denied) return denied;
		var tid = ScopeTo(tenantId);
		var entity = await _db.AppPlans.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFoundApp();

		var target = await _db.AppVersions.AsNoTracking()
			.FirstOrDefaultAsync(v => v.AppId == entity.Id && v.Version == version, cancellationToken);
		if (target is null)
			return NotFound(new ApiError { Code = ErrorCodes.AppNotFound, Message = $"版本 {version} 不存在。", Decision = null });
		if (await ValidateThemeAsync(tid, target.ThemeKey, cancellationToken) is { } themeDenied) return themeDenied;

		var key = IdempotencyKey();
		if (!string.IsNullOrWhiteSpace(key))
		{
			var prior = await _db.AppPublishIdempotencies.AsNoTracking()
				.FirstOrDefaultAsync(x => x.TenantId == tid && x.AppCode == code && x.IdempotencyKey == key, cancellationToken);
			if (prior is not null)
				return Ok(new PublishResult(entity.Id, tid, prior.PublishedVersion, entity.PublishedAt, entity.PublishedBy, version));
		}

		var newVersion = entity.PublishedVersion + 1;
		entity.PublishedDslJson = target.DslJson;
		entity.PublishedVersion = newVersion;
		entity.Status = AppStatuses.Published;
		entity.PublishedAt = DateTime.UtcNow;
		entity.PublishedBy = Actor();

		_db.AppVersions.Add(new AppVersion
		{
			AppId = entity.Id,
			TenantId = tid,
			Version = newVersion,
			Code = target.Code,
			Name = target.Name,
			Description = target.Description,
			ThemeKey = target.ThemeKey,
			DslVersion = target.DslVersion,
			DslJson = target.DslJson,
			PublishedAt = entity.PublishedAt.Value,
			PublishedBy = entity.PublishedBy,
			RolledBackFromVersion = target.Version,
		});

		if (!string.IsNullOrWhiteSpace(key))
			_db.AppPublishIdempotencies.Add(new AppPublishIdempotency
			{
				TenantId = tid,
				AppCode = code,
				IdempotencyKey = key,
				ExpectedDraftRevision = entity.DraftRevision,
				PublishedVersion = newVersion,
			});

		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException)
		{
			return ConflictApp(ErrorCodes.AppIdempotencyConflict, null, "并发回滚冲突，请重试或确认幂等键。");
		}
		return Ok(new PublishResult(entity.Id, tid, newVersion, entity.PublishedAt, entity.PublishedBy, target.Version));
	}

	/// <summary>列出应用的全部发布版本（M7-02）。需 app:view。</summary>
	[HttpGet("{code}/versions")]
	public async Task<IActionResult> Versions(
		string code,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppView) is { } denied) return denied;
		ScopeTo(tenantId);
		var entity = await _db.AppPlans.AsNoTracking()
			.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFoundApp();

		var list = await _db.AppVersions.AsNoTracking()
			.Where(v => v.AppId == entity.Id)
			.OrderByDescending(v => v.Version)
			.Select(v => new AppVersionSummary(
				v.Id, v.Version, v.Name, v.Code, v.DslVersion, v.PublishedAt, v.PublishedBy,
				v.RolledBackFromVersion, v.Version == entity.PublishedVersion))
			.ToListAsync(cancellationToken);
		return Ok(new AppVersionListResult(list, list.Count));
	}

	/// <summary>
	/// M7-11：运行已发布应用（使用发布快照）。需 app:view。
	/// 未发布应用返回 409；任一数据组件失败使整体 Succeeded=false（不误报完整成功）。
	/// </summary>
	[HttpGet("{code}/render")]
	public async Task<IActionResult> Render(
		string code,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppView) is { } denied) return denied;
		var tid = ScopeTo(tenantId);
		var userId = ResolveUserId();
		if (userId <= 0) return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });
		return await RenderAppAsync(code, tid, userId, preview: false, cancellationToken);
	}

	/// <summary>
	/// M7-11：预览应用草稿（仅编辑者）。需 app:edit。
	/// 未发布也可预览草稿；无草稿返回 409。
	/// </summary>
	[HttpGet("{code}/preview")]
	public async Task<IActionResult> Preview(
		string code,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppEdit) is { } denied) return denied;
		var tid = ScopeTo(tenantId);
		var userId = ResolveUserId();
		if (userId <= 0) return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });
		return await RenderAppAsync(code, tid, userId, preview: true, cancellationToken);
	}

	/// <summary>
	/// M7-11：复制应用为新应用（另存为新）。需 app:create；来源为发布态需 app:view、来源为草稿需 app:edit。
	/// 复制以草稿形式创建（不继承发布态），禁止伪造 turnId。
	/// </summary>
	[HttpPost("{code}/copy")]
	public async Task<IActionResult> Copy(
		string code,
		[FromBody] CopyAppRequest? request,
		CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppCreate) is { } denied) return denied;
		var tenantId = request?.TenantId ?? 0;
		if (tenantId <= 0) return BadRequest("tenantId 必须 > 0。");
		var tid = ScopeTo(tenantId);

		var entity = await _db.AppPlans.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFoundApp();

		// 来源读取权限：发布态需 app:view，草稿需 app:edit（不能仅 app:create 复制他人隐藏草稿）。
		if (entity.Status == AppStatuses.Published)
		{
			if (Require(IdentityPermissions.AppView) is { } srcDenied) return srcDenied;
		}
		else
		{
			if (Require(IdentityPermissions.AppEdit) is { } srcDenied) return srcDenied;
		}

		var sourceDslJson = entity.Status == AppStatuses.Published && !string.IsNullOrWhiteSpace(entity.PublishedDslJson)
			? entity.PublishedDslJson
			: entity.DslJson;
		if (!_dslSerializer.TryDeserialize(sourceDslJson, out var dsl, out var errors) || dsl is null)
			return BadRequest(new { errors });

		var newCode = request?.Code ?? $"{entity.Code}-copy";
		dsl.Code = newCode;
		dsl.Name = request?.Name ?? $"{entity.Name} 副本";

		var result = await _agent.BuildFromDslAsync(tid, dsl, newCode);
		if (!result.Success || result.Plan is null)
			return BadRequest(new { errors = result.Errors });
		if (await ValidateThemeAsync(tid, result.Plan.ThemeKey, cancellationToken) is { } themeDenied) return themeDenied;

		var conflict = await _db.AppPlans
			.IgnoreQueryFilters()
			.AnyAsync(p => p.Code == newCode && (p.TenantId == tid || p.TenantId == 0), cancellationToken);
		if (conflict)
		{
			// 幂等：同 Idempotency-Key 重入直接返回既有新应用，不重建（§10.14）。
			if (!string.IsNullOrWhiteSpace(IdempotencyKey()))
			{
				var existing = await _db.AppPlans.IgnoreQueryFilters()
					.FirstAsync(p => p.Code == newCode && (p.TenantId == tid || p.TenantId == 0), cancellationToken);
				return Ok(ToDetail(existing));
			}
			return Conflict(new { errors = new[] { $"应用编码已存在：{newCode}。" } });
		}

		var plan = result.Plan;
		plan.TenantId = tid;
		plan.Status = AppStatuses.Draft;
		plan.PublishedDslJson = null;
		plan.PublishedVersion = 0;
		plan.PublishedAt = null;
		plan.PublishedBy = null;

		_db.AppPlans.Add(plan);
		await _db.SaveChangesAsync(cancellationToken);
		return CreatedAtAction(nameof(GetByCode), new { code = plan.Code, tenantId = tid }, ToDetail(plan));
	}

	/// <summary>编辑器蓝图（结构化，无 HTML）。需 app:create（契约 §5.3/§10.12）。</summary>
	[HttpGet("editor/blueprint")]
	public async Task<IActionResult> EditorBlueprint([FromQuery] long tenantId = 0, CancellationToken cancellationToken = default)
	{
		if (Require(IdentityPermissions.AppCreate) is { } denied) return denied;
		var tid = tenantId > 0 ? ScopeTo(tenantId) : 0;
		var userId = ResolveUserId();

		var themeKeys = await _db.Themes.IgnoreQueryFilters()
			.Where(t => t.TenantId == tid || t.TenantId == 0)
			.Select(t => t.Key)
			.ToListAsync(cancellationToken);

		IReadOnlyList<AppDataSourceScope> dataSourceScopes = System.Array.Empty<AppDataSourceScope>();
		if (userId > 0 && tid > 0)
		{
			var authorizedIds = await _dataSourceAuth.GetAuthorizedDataSourceIdsAsync(tid, userId, cancellationToken);
			dataSourceScopes = await _db.DataSources.AsNoTracking()
				.Where(d => authorizedIds.Contains(d.Id))
				.Select(d => new AppDataSourceScope(d.Id, d.Name, d.DbType))
				.ToListAsync(cancellationToken);
		}

		var skeleton = new AppDsl
		{
			Version = AppDslVersions.Current,
			Code = "my-app",
			Name = "新应用",
			Description = "应用描述",
			ThemeKey = null,
			Pages = new List<PagePlan>
			{
				new()
				{
					Id = "home",
					Name = "首页",
					Order = 1,
					Layout = new AppLayoutDsl { Kind = AppLayoutKinds.Grid, Columns = 12, RowHeight = 64, Gap = 12 },
					Components = new List<ComponentPlan>
					{
						new()
						{
							Type = AppComponentTypes.Kpi,
							Id = "kpi1",
							Title = "示例指标",
							Order = 1,
							Binding = new AppDataSourceBinding
							{
								Entity = "sales_order",
								Metrics = new List<AppMetricBinding> { new() { Field = "amount", Aggregation = AppAggregateTypes.Sum } },
								Dimensions = new List<string>(),
								Filters = new List<AppFilterBinding>(),
								Limit = null,
							},
							Properties = new Dictionary<string, string> { { "format", "N2" } },
							Style = new AppComponentStyle { Palette = "primary", ShowBorder = true, Padding = "normal" },
						},
					},
				},
			},
		};

		return Ok(new AppEditorBlueprint(
			DslVersion: AppDslVersions.Current,
			ComponentTypes: AppComponentTypes.Supported,
			AggregateTypes: AppAggregateTypes.Supported,
			FilterOperators: AppFilterOperators.Supported,
			LayoutKinds: AppLayoutKinds.Supported,
			Skeleton: _dslSerializer.Serialize(skeleton),
			ThemeKeys: themeKeys,
			DataSourceScopes: dataSourceScopes));
	}

	private async Task<IActionResult> RenderAppAsync(string code, long tenantId, long userId, bool preview, CancellationToken ct)
	{
		var entity = await _db.AppPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, ct);
		if (entity is null) return NotFoundApp();

		var dslJson = preview ? entity.DslJson : entity.PublishedDslJson;
		if (string.IsNullOrWhiteSpace(dslJson))
			return StatusCode(StatusCodes.Status409Conflict, new ApiError
			{
				Code = ErrorCodes.AppNotPublished,
				Message = preview ? "草稿不存在，无法预览。" : "应用尚未发布，无法运行（请先发布）。",
			});

		if (!_dslSerializer.TryDeserialize(dslJson, out var dsl, out var errors) || dsl is null)
			return BadRequest(new { errors });

		var model = new AppRenderModel
		{
			Code = entity.Code,
			Name = entity.Name,
			ThemeKey = entity.ThemeKey,
			PublishedVersion = entity.PublishedVersion,
		};

		foreach (var page in dsl.Pages)
		{
			foreach (var comp in page.Components)
			{
				var render = new AppComponentRender { Id = comp.Id, Type = comp.Type, Title = comp.Title };
				if (comp.Type == AppComponentTypes.Text)
				{
					render.Text = comp.Properties.TryGetValue("text", out var t) ? t
						: comp.Properties.TryGetValue("markdown", out var markdown) ? markdown : "";
					render.Succeeded = true;
				}
				else if (comp.Binding is not null)
				{
					try
					{
						var exec = await _executor.ExecuteComponentAsync(comp.Binding, tenantId, userId, ct);
						render.Succeeded = exec.Succeeded;
						render.ErrorCode = exec.ErrorCode;
						render.ErrorMessage = exec.ErrorMessage;
						render.Columns = exec.Columns;
						render.Data = exec.Data;
						render.Series = exec.Series;
						if (comp.Type == AppComponentTypes.Chart)
						{
							render.ChartType = comp.Properties.TryGetValue("chartType", out var ct2) ? ct2 : "bar";
							render.AxisFields = comp.Properties.TryGetValue("categoryField", out var af)
								? new List<string> { af }
								: new List<string>();
						}
					}
					catch (SuperBuilderException ex)
					{
						render.Succeeded = false;
						render.ErrorCode = ex.ErrorCode;
						render.ErrorMessage = ex.Message;
					}
				}
				else
				{
					render.Succeeded = false;
					render.ErrorCode = ErrorCodes.AppBindingNotSupported;
					render.ErrorMessage = "该组件缺少数据绑定。请在 Ask 重新提问成功后生成应用，再运行新应用。";
				}
				model.Components.Add(render);
			}
		}

		model.Succeeded = model.Components.All(c => c.Succeeded);
		return Ok(model);
	}

	private string Actor() => User.Identity?.Name ?? "system";

	private static AppSummary ToSummary(AppPlan p, bool canEdit = false) =>
		new(p.Id, p.TenantId, p.Code, p.Name, p.Description, p.Status, p.DslVersion, p.ThemeKey, p.DslJson,
			p.PublishedVersion, p.PublishedAt, p.CreatedTime, p.UpdatedTime,
			canEdit && p.PublishedDslJson != p.DslJson);

	private static AppDetail ToDetail(AppPlan p) =>
		new(p.Id, p.TenantId, p.Code, p.Name, p.Description, p.Status, p.DslVersion, p.ThemeKey, p.DslJson,
			p.PublishedVersion, p.PublishedAt, p.CreatedTime, p.UpdatedTime, p.DraftRevision, p.PublishedDslJson);

	#region Request / Response DTOs
	public sealed record CreateAppRequest(
		long TenantId,
		string DslJson,
		string? Code = null);

	public sealed record GenerateAppRequest(
		long TenantId,
		string Description,
		string? Code = null,
		string? ThemeKey = null);

	/// <summary>M7-11：从 Ask 快照生成应用。</summary>
	public sealed record CreateFromAskRequest(
		long TenantId,
		string TurnId,
		string? Name = null,
		string? Code = null,
		string? ThemeKey = null);

	public sealed record UpdateAppRequest(
		string DslJson);

	/// <summary>M7-11：复制应用为新应用。</summary>
	public sealed record CopyAppRequest(
		long TenantId,
		string? Name = null,
		string? Code = null);

	public sealed record AppSummary(
		long Id,
		long TenantId,
		string Code,
		string Name,
		string? Description,
		string Status,
		string DslVersion,
		string? ThemeKey,
		string DslJson,
		int PublishedVersion,
		DateTime? PublishedAt,
		DateTime CreatedAt,
		DateTime? UpdatedAt,
		bool HasDraft)
	{
		/// <summary>契约 §10.5 字段别名（与 ThemeKey 同源）。</summary>
		public string? ThemeRef => ThemeKey;
	}

	public sealed record AppDetail(
		long Id,
		long TenantId,
		string Code,
		string Name,
		string? Description,
		string Status,
		string DslVersion,
		string? ThemeKey,
		string DslJson,
		int PublishedVersion,
		DateTime? PublishedAt,
		DateTime CreatedAt,
		DateTime? UpdatedAt,
		int? DraftRevision,
		string? PublishedDslJson)
	{
		/// <summary>契约 §10.6 字段别名（与 ThemeKey 同源）。</summary>
		public string? ThemeRef => ThemeKey;
	}

	public sealed record PublishResult(
		long AppId,
		long TenantId,
		int Version,
		DateTime? PublishedAt,
		string? PublishedBy,
		int? RolledBackFromVersion = null);

	public sealed record AppVersionSummary(
		long Id,
		int Version,
		string Name,
		string Code,
		string DslVersion,
		DateTime PublishedAt,
		string? PublishedBy,
		int? RolledBackFromVersion,
		bool IsCurrent);

	/// <summary>列表信封（§10.5）：分页后的条目与总数。</summary>
	public sealed record AppListResult(
		IReadOnlyList<AppSummary> Items,
		int Total,
		int Page,
		int PageSize);

	/// <summary>版本列表信封（§10.11）。</summary>
	public sealed record AppVersionListResult(
		IReadOnlyList<AppVersionSummary> Items,
		int Total);

	/// <summary>编辑器蓝图中的数据源作用域摘要（§10.12）：不含凭据。</summary>
	public sealed record AppDataSourceScope(
		long Id,
		string Name,
		string DbType);

	public sealed record AppEditorBlueprint(
		string DslVersion,
		IReadOnlyList<string> ComponentTypes,
		IReadOnlyList<string> AggregateTypes,
		IReadOnlyList<string> FilterOperators,
		IReadOnlyList<string> LayoutKinds,
		string Skeleton,
		IReadOnlyList<string> ThemeKeys,
		IReadOnlyList<AppDataSourceScope> DataSourceScopes);
	#endregion
}
