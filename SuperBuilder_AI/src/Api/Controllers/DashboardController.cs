using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Dashboard;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Interfaces.Theme;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Dashboard.Rendering;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Models.Theme;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Low-code BI 仪表盘 API（P6.4 DashboardController 生产端点）。
///
/// <para>
/// 提供仪表盘的 CRUD、结构化渲染（经由 P6.3 LowcodeRenderer）与编辑器蓝图占位：
/// <list type="bullet">
/// <item><c>POST /api/dashboards</c>：保存整份 DSL 文档（先反序列化+校验，再落地冗余列）。</item>
/// <item><c>GET /api/dashboards</c>：按租户作用域列表（全局模板 TenantId=0 对所有租户可见）。</item>
/// <item><c>GET /api/dashboards/{id}</c> / <c>PUT</c> / <c>DELETE</c>：单资源读写与删除。</item>
/// <item><c>GET /api/dashboards/{id}/render</c>：加载 DSL → P6.3 渲染器 → 纯结构化 <see cref="DashboardRenderModel"/>（绝不输出 HTML）。</item>
/// <item><c>GET /api/dashboards/editor/blueprint</c>：编辑器前端占位（DSL 骨架 + 可用枚举清单）。</item>
/// </list>
/// </para>
///
/// <para>
/// 租户隔离沿用 P4.3 的"显式开启"策略：本控制器在每个端点内调用
/// <see cref="SuperBIContext.ApplyTenantScope"/>，而非依赖 DbContext 自行读取访问器，
/// 避免请求期取值错位。当前平台无 IAM 中间件，租户由 <c>tenantId</c> 查询参数显式传入
/// （默认 0 = 系统/全局上下文），与 <see cref="SemanticLabelController"/> 一致；
/// 若上游已在 <see cref="IPlatformContextAccessor"/> 写好作用域上下文则优先采用。
/// </para>
///
/// <para>本控制器不触碰 BI 查询链路与 Golden 契约数据，属平台管理面，不影响 Golden 18/18 行为契约。</para>
/// </summary>
[ApiController]
[Route("api/dashboards")]
public sealed class DashboardController : ControllerBase
{
	private readonly IDashboardDslSerializer _serializer;
	private readonly IDashboardRenderer _renderer;
	private readonly IPlatformContextAccessor _accessor;
	private readonly IThemeResolver _themeResolver;
	private readonly SuperBIContext _db;

	public DashboardController(
		IDashboardDslSerializer serializer,
		IDashboardRenderer renderer,
		IPlatformContextAccessor accessor,
		IThemeResolver themeResolver,
		SuperBIContext db)
	{
		_serializer = serializer;
		_renderer = renderer;
		_accessor = accessor;
		_themeResolver = themeResolver;
		_db = db;
	}

	/// <summary>
	/// 在当前请求作用域内解析并开启租户隔离；优先采用显式传入的 tenantId，
	/// 其次回退到访问器中已作用域的租户；二者皆无则视为系统/全局上下文（不施加隔离）。
	/// 同时把解析出的上下文写回访问器，供下游（渲染取数链路）一致读取。
	/// </summary>
	/// <summary>
	/// 在当前请求作用域内解析并开启租户隔离：有效租户恒为认证租户（数据面单租户恒等），
	/// 跨租户显式请求直接拒绝。同时把解析出的上下文写回访问器，供下游（渲染取数链路）一致读取。
	/// </summary>
	private (long TenantId, PlatformContext Context) ScopeTo(long requestedTenantId)
	{
		var resolution = TenantDataPlanePolicy.ResolvePlatformScope(User, requestedTenantId);
		// P0-02B：把解析出的租户上下文写盘，供审计/可观测中间件读取；治理角色管理他租户时另记管理目标
		TenantDataPlanePolicy.StorePlatformScope(HttpContext, resolution, "Dashboard");
		if (!resolution.Authorized)
			throw new SuperBuilder_AI.Api.Errors.SuperBuilderException(
				SuperBuilder_AI.Api.Errors.ErrorCodes.TenantIsolated,
				"禁止：租户作用域请求只能访问认证租户的数据，跨租户访问被拒绝。",
				403);
		var tenantId = resolution.EffectiveTenantId;
		var context = tenantId > 0 ? PlatformContext.FromTenant(tenantId) : PlatformContext.System;
		_accessor.Current = context;
		_db.ApplyTenantScope(tenantId);
		return (tenantId, context);
	}

	private async Task<IActionResult?> ValidateThemeAsync(long tenantId, string? themeKey, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(themeKey) || themeKey == BuiltInThemeKeys.Default) return null;
		var accessible = await _db.Themes.IgnoreQueryFilters()
			.AnyAsync(t => t.Key == themeKey && (t.TenantId == tenantId || t.TenantId == 0), ct);
		return accessible ? null : BadRequest(new { errors = new[] { $"主题不可用或不属于当前租户：{themeKey}。" } });
	}

	/// <summary>创建仪表盘：反序列化+校验 DSL，落地冗余列与 DslJson。</summary>
	[HttpPost]
	public async Task<IActionResult> Create(
		[FromBody] CreateDashboardRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (string.IsNullOrWhiteSpace(request.DslJson)) return BadRequest("DslJson 不能为空。");

		if (!_serializer.TryDeserialize(request.DslJson, out var dsl, out var errors) || dsl is null)
			return BadRequest(new { errors });

		var validationErrors = _serializer.Validate(dsl);
		if (validationErrors.Count > 0) return BadRequest(new { errors = validationErrors });

		var (tenantId, _) = ScopeTo(request.TenantId);
		if (await ValidateThemeAsync(tenantId, dsl.ThemeKey, cancellationToken) is { } themeDenied) return themeDenied;

		var status = string.IsNullOrWhiteSpace(request.Status) ? DashboardStatuses.Draft : request.Status;
		if (!DashboardStatuses.Supported.Contains(status))
			return BadRequest(new { errors = new[] { $"不支持的状态：{status}。" } });

		var entity = new Dashboard
		{
			TenantId = tenantId,
			Code = dsl.Code ?? string.Empty,
			Title = dsl.Title,
			Description = dsl.Description,
			Status = status,
			DslVersion = dsl.Version,
			DslJson = _serializer.Serialize(dsl),
			ThemeKey = dsl.ThemeKey,
		};

		_db.Dashboards.Add(entity);
		await _db.SaveChangesAsync(cancellationToken);

		return CreatedAtAction(nameof(GetById), new { id = entity.Id, tenantId }, ToSummary(entity));
	}

	/// <summary>列表：按租户作用域返回（含全局模板 TenantId=0）。</summary>
	[HttpGet]
	public async Task<IActionResult> List(
		[FromQuery] long tenantId = 0,
		[FromQuery] string? status = null,
		CancellationToken cancellationToken = default)
	{
		ScopeTo(tenantId);

		var query = _db.Dashboards.AsNoTracking();
		if (!string.IsNullOrWhiteSpace(status)) query = query.Where(d => d.Status == status);

		var items = await query.OrderBy(d => d.Id).ToListAsync(cancellationToken);
		return Ok(items.Select(ToSummary).ToList());
	}

	/// <summary>获取单个仪表盘（受租户作用域约束，不可见则返回 404）。</summary>
	[HttpGet("{id:long}")]
	public async Task<IActionResult> GetById(
		long id,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		ScopeTo(tenantId);

		var entity = await _db.Dashboards.AsNoTracking()
			.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
		if (entity is null) return NotFound();

		return Ok(ToDetail(entity));
	}

	/// <summary>更新仪表盘：重新校验 DSL 后覆盖冗余列与 DslJson。</summary>
	[HttpPut("{id:long}")]
	public async Task<IActionResult> Update(
		long id,
		[FromBody] CreateDashboardRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (string.IsNullOrWhiteSpace(request.DslJson)) return BadRequest("DslJson 不能为空。");

		if (!_serializer.TryDeserialize(request.DslJson, out var dsl, out var errors) || dsl is null)
			return BadRequest(new { errors });

		var validationErrors = _serializer.Validate(dsl);
		if (validationErrors.Count > 0) return BadRequest(new { errors = validationErrors });

		var (effectiveTenantId, _) = ScopeTo(tenantId);
		var entity = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
		if (entity is null) return NotFound();
		if (await ValidateThemeAsync(effectiveTenantId, dsl.ThemeKey, cancellationToken) is { } themeDenied) return themeDenied;

		var status = string.IsNullOrWhiteSpace(request.Status) ? entity.Status : request.Status;
		if (!DashboardStatuses.Supported.Contains(status))
			return BadRequest(new { errors = new[] { $"不支持的状态：{status}。" } });

		entity.Code = dsl.Code ?? string.Empty;
		entity.Title = dsl.Title;
		entity.Description = dsl.Description;
		entity.Status = status;
		entity.DslVersion = dsl.Version;
		entity.DslJson = _serializer.Serialize(dsl);
		entity.ThemeKey = dsl.ThemeKey;

		await _db.SaveChangesAsync(cancellationToken);
		return Ok(ToSummary(entity));
	}

	/// <summary>删除仪表盘（受租户作用域约束）。</summary>
	[HttpDelete("{id:long}")]
	public async Task<IActionResult> Delete(
		long id,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		ScopeTo(tenantId);
		var entity = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
		if (entity is null) return NotFound();

		_db.Dashboards.Remove(entity);
		await _db.SaveChangesAsync(cancellationToken);
		return NoContent();
	}

	/// <summary>
	/// 渲染仪表盘：加载 DslJson → P6.3 LowcodeRenderer → 纯结构化 <see cref="DashboardRenderModel"/>。
	/// 取数作用域按仪表盘所属租户开启，保证渲染链路只能访问该租户（及全局）的数据源。
	/// </summary>
	[HttpGet("{id:long}/render")]
	public async Task<IActionResult> Render(
		long id,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		// 先以入参租户试探可见性（或系统上下文不隔离）。
		ScopeTo(tenantId);
		var entity = await _db.Dashboards.AsNoTracking()
			.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
		if (entity is null) return NotFound();

		// 渲染取数严格按仪表盘所属租户作用域（即使入参为 0，也按实体租户隔离）。
		if (entity.TenantId > 0)
		{
			var ctx = PlatformContext.FromTenant(entity.TenantId);
			_accessor.Current = ctx;
			_db.ApplyTenantScope(entity.TenantId);
		}

		// 草稿/发布隔离：已发布且存在发布快照时，渲染「发布态」而非草稿，
		// 保证编辑草稿不影响线上。未发布（或发布快照缺失）回退渲染草稿。
		var dslJsonToRender = (entity.Status == DashboardStatuses.Published
			&& !string.IsNullOrEmpty(entity.PublishedDslJson))
			? entity.PublishedDslJson
			: entity.DslJson;
		if (!_serializer.TryDeserialize(dslJsonToRender, out var dsl, out var errors) || dsl is null)
			return StatusCode(500, new { errors });

		// P7.3：按仪表盘所属租户 + 仪表盘显式 ThemeKey 级联解析主题，注入渲染上下文。
		// 解析失败（或租户/键未命中）时 ThemeResolver 自动兜底内置默认，渲染永不失败。
		var themeContext = await _themeResolver.ResolveAsync(entity.TenantId, dsl.ThemeKey, cancellationToken);
		var platformContext = (_accessor.Current ?? PlatformContext.System) with { Theme = themeContext };
		var model = await _renderer.RenderAsync(dsl, platformContext, cancellationToken);
		return Ok(model);
	}

	/// <summary>
	/// 发布仪表盘（M7-01）：把当前草稿 <see cref="Dashboard.DslJson"/> 固化为发布快照，
	/// 写入 <see cref="Dashboard.PublishedDslJson"/> 并自增 <see cref="Dashboard.PublishedVersion"/>，
	/// 同时在 <c>DashboardVersions</c> 落一条不可变版本记录（可追溯回滚）。
	/// 仅当草稿非空时允许发布；发布后渲染端点将优先返回发布态。
	/// </summary>
	[HttpPost("{id:long}/publish")]
	public async Task<IActionResult> Publish(
		long id,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		var (tid, _) = ScopeTo(tenantId);
		var entity = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
		if (entity is null) return NotFound();
		if (string.IsNullOrWhiteSpace(entity.DslJson))
			return BadRequest(new { errors = new[] { "草稿 DSL 为空，无法发布。" } });
		if (await ValidateThemeAsync(tid, entity.ThemeKey, cancellationToken) is { } themeDenied) return themeDenied;

		entity.PublishedDslJson = entity.DslJson;
		entity.PublishedVersion += 1;
		entity.Status = DashboardStatuses.Published;
		entity.PublishedAt = DateTime.UtcNow;
		entity.PublishedBy = Actor();

		_db.DashboardVersions.Add(new DashboardVersion
		{
			DashboardId = entity.Id,
			TenantId = tid,
			Version = entity.PublishedVersion,
			Code = entity.Code,
			Title = entity.Title,
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
	/// 回滚仪表盘（M7-01）：把指定历史版本恢复为「当前发布态」。
	/// 历史快照只读，不会改写；回滚会再固化为一条<em>新</em>版本
	/// （<see cref="DashboardVersion.RolledBackFromVersion"/> 指向被恢复的来源版本），
	/// 保证版本链单调递增、全程可追溯。
	/// </summary>
	[HttpPost("{id:long}/rollback/{version:int}")]
	public async Task<IActionResult> Rollback(
		long id,
		int version,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		var (tid, _) = ScopeTo(tenantId);
		var entity = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
		if (entity is null) return NotFound();

		var target = await _db.DashboardVersions.AsNoTracking()
			.FirstOrDefaultAsync(v => v.DashboardId == id && v.Version == version, cancellationToken);
		if (target is null)
			return NotFound(new { errors = new[] { $"版本 {version} 不存在。" } });
		if (await ValidateThemeAsync(tid, target.ThemeKey, cancellationToken) is { } themeDenied) return themeDenied;

		entity.PublishedDslJson = target.DslJson;
		entity.PublishedVersion += 1;
		entity.Status = DashboardStatuses.Published;
		entity.PublishedAt = DateTime.UtcNow;
		entity.PublishedBy = Actor();

		_db.DashboardVersions.Add(new DashboardVersion
		{
			DashboardId = entity.Id,
			TenantId = tid,
			Version = entity.PublishedVersion,
			Code = target.Code,
			Title = target.Title,
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
	/// 列出仪表盘的全部发布版本（M7-01，按版本号倒序；含是否当前发布态标记）。
	/// 仅元数据与版本链，不含 DSL 正文（避免大负载；如需恢复用 Rollback）。
	/// </summary>
	[HttpGet("{id:long}/versions")]
	public async Task<IActionResult> Versions(
		long id,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		ScopeTo(tenantId);
		var entity = await _db.Dashboards.AsNoTracking()
			.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
		if (entity is null) return NotFound();

		var list = await _db.DashboardVersions.AsNoTracking()
			.Where(v => v.DashboardId == id)
			.OrderByDescending(v => v.Version)
			.Select(v => new DashboardVersionSummary(
				v.Id, v.Version, v.Title, v.Code, v.DslVersion, v.PublishedAt, v.PublishedBy,
				v.RolledBackFromVersion, v.Version == entity.PublishedVersion))
			.ToListAsync(cancellationToken);
		return Ok(list);
	}

	/// <summary>当前操作者标识（用于发布/回滚审计）。无认证身份时回退 "system"。</summary>
	private string Actor() => User.Identity?.Name ?? "system";

	/// <summary>
	/// 编辑器前端占位（P8 AI App Builder / 前端编辑器接入前的中继）。
	/// 返回 DSL 骨架模板与全部可用枚举清单，供编辑器初始化与校验下拉使用。
	/// 仅输出结构化 JSON，绝不承载 HTML。
	/// </summary>
	[HttpGet("editor/blueprint")]
	public IActionResult EditorBlueprint()
	{
		var skeleton = new DashboardDsl
		{
			Title = "新建仪表盘",
			Pages = new List<PageDsl>
			{
				new()
				{
					Id = "page-1",
					Name = "概览",
					Order = 1,
					Widgets = new List<WidgetDsl>
					{
						new TextWidgetDsl
						{
							Id = "w-title",
							Content = "仪表盘标题",
							Markdown = true,
						},
						new ChartWidgetDsl
						{
							Id = "w-chart",
							Title = "示例图表",
							ChartType = ChartTypes.Column,
							Query = new WidgetQueryDsl { Question = "各区域销售额" },
						},
					},
				},
			},
		};

		return Ok(new DashboardEditorBlueprint(
			DslVersion: DslVersions.Current,
			WidgetTypes: WidgetTypes.Supported,
			ChartTypes: ChartTypes.Supported,
			AggregateTypes: AggregateTypes.Supported,
			FilterOperators: FilterOperators.Supported,
			LayoutKinds: new[] { LayoutKinds.Grid, LayoutKinds.Flow },
			Statuses: DashboardStatuses.Supported,
			Skeleton: _serializer.Serialize(skeleton)));
	}

	private static DashboardSummary ToSummary(Dashboard d) =>
		new(d.Id, d.TenantId, d.Code, d.Title, d.Description, d.Status, d.DslVersion, d.ThemeKey, d.CreatedTime, d.PublishedVersion, d.PublishedAt);

	private static DashboardDetail ToDetail(Dashboard d) =>
		new(d.Id, d.TenantId, d.Code, d.Title, d.Description, d.Status, d.DslVersion, d.ThemeKey, d.DslJson, d.CreatedTime, d.PublishedVersion, d.PublishedAt);
}

/// <summary>创建/更新仪表盘请求体。</summary>
public sealed record CreateDashboardRequest(
	long TenantId,
	string DslJson,
	string? Status = null);

/// <summary>仪表盘摘要 DTO。</summary>
public sealed record DashboardSummary(
	long Id,
	long TenantId,
	string Code,
	string Title,
	string? Description,
	string Status,
	string DslVersion,
	string? ThemeKey,
	DateTime CreatedTime,
	int PublishedVersion,
	DateTime? PublishedAt);

public sealed record DashboardDetail(
	long Id,
	long TenantId,
	string Code,
	string Title,
	string? Description,
	string Status,
	string DslVersion,
	string? ThemeKey,
	string DslJson,
	DateTime CreatedTime,
	int PublishedVersion,
	DateTime? PublishedAt);

/// <summary>发布/回滚结果 DTO（M7-01）。</summary>
public sealed record PublishResult(
	long DashboardId,
	long TenantId,
	int Version,
	DateTime? PublishedAt,
	string? PublishedBy,
	int? RolledBackFromVersion = null);

/// <summary>仪表盘版本摘要 DTO（M7-01，列表不含 DSL 正文）。</summary>
public sealed record DashboardVersionSummary(
	long Id,
	int Version,
	string Title,
	string Code,
	string DslVersion,
	DateTime PublishedAt,
	string? PublishedBy,
	int? RolledBackFromVersion,
	bool IsCurrent);

/// <summary>编辑器蓝图 DTO（结构化，无 HTML）。</summary>
public sealed record DashboardEditorBlueprint(
	string DslVersion,
	IReadOnlyList<string> WidgetTypes,
	IReadOnlyList<string> ChartTypes,
	IReadOnlyList<string> AggregateTypes,
	IReadOnlyList<string> FilterOperators,
	IReadOnlyList<string> LayoutKinds,
	IReadOnlyList<string> Statuses,
	string Skeleton);
