using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Models.AppBuilder;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// AI 应用构建端点（P8.3 AI App Builder）。
///
/// <para>
/// 提供应用（结构化 AppDsl 文档）的租户作用域 CRUD、从自然语言描述生成应用，以及编辑器蓝图：
/// <list type="bullet">
/// <item><c>POST /api/apps</c>：从结构化 DSL 创建应用（默认路径，确定性、不调 LLM）。</item>
/// <item><c>POST /api/apps/generate</c>：从自然语言描述生成应用（非默认路径，仅显式描述启用 LLM）。</item>
/// <item><c>GET /api/apps</c>：按租户作用域列表（含全局模板 TenantId=0）。</item>
/// <item><c>GET /api/apps/{code}</c>：获取单个应用（含完整 DSL）。</item>
/// <item><c>PUT /api/apps/{code}</c>：更新应用（Code 不变；全局模板不可改）。</item>
/// <item><c>DELETE /api/apps/{code}</c>：删除应用（全局模板不可删）。</item>
/// <item><c>GET /api/apps/editor/blueprint</c>：编辑器蓝图（DSL 骨架 + 枚举清单），仅 JSON。</item>
/// </list>
/// </para>
///
/// <para>
/// 编排统一委托给 <see cref="IAppBuilderAgent"/>（P8.2）：<c>Create</c> 走默认路径
/// <c>BuildFromDslAsync</c>（确定性、零回归），<c>Generate</c> 走非默认路径
/// <c>GenerateFromDescriptionAsync</c>（仅显式描述启用 LLM）。传输层负责把请求 JSON 反序列化为
/// <see cref="AppDsl"/> 并先校验后信任，编排层才生成 <see cref="AppPlan"/>。
/// </para>
///
/// <para>
/// 租户隔离沿用 P4.3 的"显式开启"策略：每个端点内调用 <see cref="SuperBIContext.ApplyTenantScope"/>。
/// 当前平台无 IAM 中间件，租户由 <c>tenantId</c> 查询参数显式传入（默认 0 = 系统/全局视图）。
/// 全局模板（TenantId=0）对所有租户可见但不可被租户修改/删除。Code 在同租户 + 全局范围内唯一。
/// </para>
///
/// <para>本控制器属平台管理面，不触碰 BI 查询链路与 Golden 契约数据，不影响 Golden 18/18 行为契约。</para>
/// </summary>
[ApiController]
[Route("api/apps")]
public sealed class AppBuilderController : ControllerBase
{
	private readonly SuperBIContext _db;
	private readonly IAppDslSerializer _dslSerializer;
	private readonly IAppBuilderAgent _agent;

	public AppBuilderController(SuperBIContext db, IAppDslSerializer dslSerializer, IAppBuilderAgent agent)
	{
		_db = db;
		_dslSerializer = dslSerializer;
		_agent = agent;
	}

	/// <summary>在当前请求作用域内开启租户隔离，返回解析出的租户 Id。</summary>
	/// <summary>在当前请求作用域内开启租户隔离：有效租户恒为认证租户，跨租户显式请求直接拒绝。</summary>
	private long ScopeTo(long requestedTenantId)
	{
		var resolution = TenantDataPlanePolicy.ResolvePlatformScope(User, requestedTenantId);
		// P0-02B：把解析出的租户上下文写盘，供审计/可观测中间件读取；治理角色管理他租户时另记管理目标
		TenantDataPlanePolicy.StorePlatformScope(HttpContext, resolution, "App");
		if (!resolution.Authorized)
			throw new SuperBuilder_AI.Api.Errors.SuperBuilderException(
				SuperBuilder_AI.Api.Errors.ErrorCodes.TenantIsolated,
				"禁止：租户作用域请求只能访问认证租户的数据，跨租户访问被拒绝。",
				403);
		_db.ApplyTenantScope(resolution.EffectiveTenantId);
		return resolution.EffectiveTenantId;
	}

	/// <summary>创建应用：从结构化 DSL 编排（默认路径，确定性、不调 LLM）。</summary>
	[HttpPost]
	public async Task<IActionResult> Create(
		[FromBody] CreateAppRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (request.TenantId <= 0) return BadRequest("不能通过 API 创建全局/内置应用（TenantId 必须 > 0）。");
		if (string.IsNullOrWhiteSpace(request.DslJson)) return BadRequest("DslJson 不能为空。");

		if (!_dslSerializer.TryDeserialize(request.DslJson, out var dsl, out var errors) || dsl is null)
			return BadRequest(new { errors });

		// 默认路径：结构化 DSL → AppPlan，确定性、不调 LLM。
		var result = await _agent.BuildFromDslAsync(request.TenantId, dsl, request.Code);
		if (!result.Success || result.Plan is null)
			return BadRequest(new { errors = result.Errors });

		var plan = result.Plan;
		var tenantId = ScopeTo(request.TenantId);
		plan.TenantId = tenantId;

		// 同租户或全局模板已存在该 Code 则冲突。
		var conflict = await _db.AppPlans
			.IgnoreQueryFilters()
			.AnyAsync(p => p.Code == plan.Code && (p.TenantId == tenantId || p.TenantId == 0), cancellationToken);
		if (conflict) return Conflict(new { errors = new[] { $"应用编码已存在：{plan.Code}。" } });

		_db.AppPlans.Add(plan);
		await _db.SaveChangesAsync(cancellationToken);

		return CreatedAtAction(nameof(GetByCode), new { code = plan.Code, tenantId }, ToDetail(plan));
	}

	/// <summary>
	/// 从自然语言描述生成应用（非默认路径）：描述 → Qwen 生成 DSL → 校验 → AppPlan。
	/// 仅当调用方显式传入描述时才启用 LLM；LLM 失败返回 502，参数错误返回 400。
	/// </summary>
	[HttpPost("generate")]
	public async Task<IActionResult> Generate(
		[FromBody] GenerateAppRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (request.TenantId <= 0) return BadRequest("生成应用需要 tenantId > 0。");
		if (string.IsNullOrWhiteSpace(request.Description)) return BadRequest("应用描述不能为空。");

		// 非默认路径：调用 LLM 生成 DSL JSON，再经 agent 先校验后信任。
		var result = await _agent.GenerateFromDescriptionAsync(request.TenantId, request.Description, request.Code);
		if (!result.Success || result.Plan is null)
		{
			// 失败发生在 LLM 增强路径（UsedAi=true）视为服务端依赖问题；否则参数/结构问题。
			return result.UsedAi
				? StatusCode(StatusCodes.Status502BadGateway, new { errors = result.Errors, usedAi = true })
				: BadRequest(new { errors = result.Errors });
		}

		var plan = result.Plan;
		var tenantId = ScopeTo(request.TenantId);
		plan.TenantId = tenantId;

		var conflict = await _db.AppPlans
			.IgnoreQueryFilters()
			.AnyAsync(p => p.Code == plan.Code && (p.TenantId == tenantId || p.TenantId == 0), cancellationToken);
		if (conflict) return Conflict(new { errors = new[] { $"应用编码已存在：{plan.Code}。" } });

		_db.AppPlans.Add(plan);
		await _db.SaveChangesAsync(cancellationToken);

		return CreatedAtAction(nameof(GetByCode), new { code = plan.Code, tenantId }, ToDetail(plan));
	}

	/// <summary>列表：按租户作用域返回（含全局模板 TenantId=0）。tenantId=0 为系统视图。</summary>
	[HttpGet]
	public async Task<IActionResult> List(
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		ScopeTo(tenantId);

		var items = await _db.AppPlans.AsNoTracking()
			.OrderBy(p => p.TenantId) // 全局(0) 在前，租户自有在后
			.ThenBy(p => p.Code)
			.ToListAsync(cancellationToken);
		return Ok(items.Select(ToSummary).ToList());
	}

	/// <summary>获取单个应用（含完整 DSL）。</summary>
	[HttpGet("{code}")]
	public async Task<IActionResult> GetByCode(
		string code,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(code)) return BadRequest("应用编码不能为空。");
		ScopeTo(tenantId);

		var entity = await _db.AppPlans.AsNoTracking()
			.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFound();
		return Ok(ToDetail(entity));
	}

	/// <summary>更新应用：重新校验 DSL 后覆盖文档列（Code 不变；全局模板不可改）。</summary>
	[HttpPut("{code}")]
	public async Task<IActionResult> Update(
		string code,
		[FromBody] UpdateAppRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (string.IsNullOrWhiteSpace(request.DslJson)) return BadRequest("DslJson 不能为空。");

		if (!_dslSerializer.TryDeserialize(request.DslJson, out var dsl, out var errors) || dsl is null)
			return BadRequest(new { errors });

		ScopeTo(tenantId);
		var entity = await _db.AppPlans.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFound();
		if (entity.TenantId == 0)
			return BadRequest(new { errors = new[] { "全局/内置应用不可修改。" } });

		// Code 锁定为原值（与主题 Key 不可变一致），仅允许整文档（DSL）覆盖。
		var result = await _agent.BuildFromDslAsync(entity.TenantId, dsl, entity.Code);
		if (!result.Success || result.Plan is null)
			return BadRequest(new { errors = result.Errors });

		entity.Name = result.Plan.Name;
		entity.Description = result.Plan.Description;
		entity.DslVersion = result.Plan.DslVersion;
		entity.DslJson = result.Plan.DslJson;
		entity.ThemeKey = result.Plan.ThemeKey;
		// Status 保持不变（草稿/发布状态不因改 DSL 而重置）。

		await _db.SaveChangesAsync(cancellationToken);
		return Ok(ToDetail(entity));
	}

	/// <summary>删除应用（全局模板不可删）。</summary>
	[HttpDelete("{code}")]
	public async Task<IActionResult> Delete(
		string code,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(code)) return BadRequest("应用编码不能为空。");
		ScopeTo(tenantId);
		var entity = await _db.AppPlans.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFound();
		if (entity.TenantId == 0)
			return BadRequest(new { errors = new[] { "全局/内置应用不可删除。" } });

		_db.AppPlans.Remove(entity);
		await _db.SaveChangesAsync(cancellationToken);
		return NoContent();
	}

	/// <summary>
	/// 发布应用（M7-02）：把当前草稿 <see cref="AppPlan.DslJson"/> 固化为发布快照，
	/// 写入 <see cref="AppPlan.PublishedDslJson"/> 并自增 <see cref="AppPlan.PublishedVersion"/>，
	/// 同时在 <c>AppVersions</c> 落一条不可变版本记录（可追溯回滚）。
	/// 仅当草稿非空时允许发布；草稿与发布态物理隔离，编辑草稿不会直接覆盖线上版本（M7-02 验收）。
	/// </summary>
	[HttpPost("{code}/publish")]
	public async Task<IActionResult> Publish(
		string code,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		var tid = ScopeTo(tenantId);
		var entity = await _db.AppPlans.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFound();
		if (string.IsNullOrWhiteSpace(entity.DslJson))
			return BadRequest(new { errors = new[] { "草稿 DSL 为空，无法发布。" } });

		entity.PublishedDslJson = entity.DslJson;
		entity.PublishedVersion += 1;
		entity.Status = AppStatuses.Published;
		entity.PublishedAt = DateTime.UtcNow;
		entity.PublishedBy = Actor();

		_db.AppVersions.Add(new AppVersion
		{
			AppId = entity.Id,
			TenantId = tid,
			Version = entity.PublishedVersion,
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

		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new PublishResult(entity.Id, tid, entity.PublishedVersion, entity.PublishedAt, entity.PublishedBy));
	}

	/// <summary>
	/// 回滚应用（M7-02）：把指定历史版本恢复为「当前发布态」。
	/// 历史快照只读，不会改写；回滚会再固化为一条<em>新</em>版本
	/// （<see cref="AppVersion.RolledBackFromVersion"/> 指向被恢复的来源版本），
	/// 保证版本链单调递增、全程可追溯。
	/// </summary>
	[HttpPost("{code}/rollback/{version:int}")]
	public async Task<IActionResult> Rollback(
		string code,
		int version,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		var tid = ScopeTo(tenantId);
		var entity = await _db.AppPlans.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFound();

		var target = await _db.AppVersions.AsNoTracking()
			.FirstOrDefaultAsync(v => v.AppId == entity.Id && v.Version == version, cancellationToken);
		if (target is null)
			return NotFound(new { errors = new[] { $"版本 {version} 不存在。" } });

		entity.PublishedDslJson = target.DslJson;
		entity.PublishedVersion += 1;
		entity.Status = AppStatuses.Published;
		entity.PublishedAt = DateTime.UtcNow;
		entity.PublishedBy = Actor();

		_db.AppVersions.Add(new AppVersion
		{
			AppId = entity.Id,
			TenantId = tid,
			Version = entity.PublishedVersion,
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

		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new PublishResult(entity.Id, tid, entity.PublishedVersion, entity.PublishedAt, entity.PublishedBy, target.Version));
	}

	/// <summary>
	/// 列出应用的全部发布版本（M7-02，按版本号倒序；含是否当前发布态标记）。
	/// 仅元数据与版本链，不含 DSL 正文（避免大负载；如需恢复用 <see cref="Rollback"/>）。
	/// </summary>
	[HttpGet("{code}/versions")]
	public async Task<IActionResult> Versions(
		string code,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		ScopeTo(tenantId);
		var entity = await _db.AppPlans.AsNoTracking()
			.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
		if (entity is null) return NotFound();

		var list = await _db.AppVersions.AsNoTracking()
			.Where(v => v.AppId == entity.Id)
			.OrderByDescending(v => v.Version)
			.Select(v => new AppVersionSummary(
				v.Id, v.Version, v.Name, v.Code, v.DslVersion, v.PublishedAt, v.PublishedBy,
				v.RolledBackFromVersion, v.Version == entity.PublishedVersion))
			.ToListAsync(cancellationToken);
		return Ok(list);
	}

	/// <summary>当前操作者标识（用于发布/回滚审计）。无认证身份时回退 "system"。</summary>
	private string Actor() => User.Identity?.Name ?? "system";

	/// <summary>
	/// 编辑器蓝图：返回 AppDsl 骨架与可用枚举清单（组件类型/聚合/操作符/布局），供编辑器初始化。
	/// 仅结构化 JSON，绝不承载 HTML。
	/// </summary>
	[HttpGet("editor/blueprint")]
	public IActionResult EditorBlueprint()
	{
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
						}
					}
				}
			}
		};

		return Ok(new AppEditorBlueprint(
			DslVersion: AppDslVersions.Current,
			ComponentTypes: AppComponentTypes.Supported,
			AggregateTypes: AppAggregateTypes.Supported,
			FilterOperators: AppFilterOperators.Supported,
			LayoutKinds: AppLayoutKinds.Supported,
			Skeleton: _dslSerializer.Serialize(skeleton)));
	}

	private static AppSummary ToSummary(AppPlan p) =>
		new(p.Id, p.TenantId, p.Code, p.Name, p.Description, p.Status, p.DslVersion, p.ThemeKey, p.PublishedVersion, p.PublishedAt);

	private static AppDetail ToDetail(AppPlan p) =>
		new(p.Id, p.TenantId, p.Code, p.Name, p.Description, p.Status, p.DslVersion, p.ThemeKey, p.DslJson, p.PublishedVersion, p.PublishedAt);

	#region Request / Response DTOs
	/// <summary>创建应用请求体（从结构化 DSL）。</summary>
	public sealed record CreateAppRequest(
		long TenantId,
		string DslJson,
		string? Code = null);

	/// <summary>从描述生成应用请求体（非默认路径，启用 LLM）。</summary>
	public sealed record GenerateAppRequest(
		long TenantId,
		string Description,
		string? Code = null);

	/// <summary>更新应用请求体（Code 不可变，仅覆盖 DSL 文档）。</summary>
	public sealed record UpdateAppRequest(
		string DslJson);

	/// <summary>应用摘要 DTO。</summary>
	public sealed record AppSummary(
		long Id,
		long TenantId,
		string Code,
		string Name,
		string? Description,
		string Status,
		string DslVersion,
		string? ThemeKey,
		int PublishedVersion,
		DateTime? PublishedAt);

	/// <summary>应用详情 DTO（含完整 DSL）。</summary>
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
		DateTime? PublishedAt);

	/// <summary>发布/回滚结果 DTO（M7-02）。</summary>
	public sealed record PublishResult(
		long AppId,
		long TenantId,
		int Version,
		DateTime? PublishedAt,
		string? PublishedBy,
		int? RolledBackFromVersion = null);

	/// <summary>应用版本摘要 DTO（M7-02，列表不含 DSL 正文）。</summary>
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

	/// <summary>编辑器蓝图 DTO（结构化，无 HTML）。</summary>
	public sealed record AppEditorBlueprint(
		string DslVersion,
		IReadOnlyList<string> ComponentTypes,
		IReadOnlyList<string> AggregateTypes,
		IReadOnlyList<string> FilterOperators,
		IReadOnlyList<string> LayoutKinds,
		string Skeleton);
	#endregion
}
