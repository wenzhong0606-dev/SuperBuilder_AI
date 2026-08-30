using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Dashboard;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Interfaces.Theme;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Dashboard.Rendering;
using SuperBuilder_AI.Models.Organization;

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
	private (long TenantId, PlatformContext Context) ScopeTo(long requestedTenantId)
	{
		var tenantId = requestedTenantId > 0
			? requestedTenantId
			: _accessor.Current?.Tenant?.IsScoped == true
				? _accessor.Current.Tenant.TenantId
				: 0;

		var context = tenantId > 0 ? PlatformContext.FromTenant(tenantId) : PlatformContext.System;
		_accessor.Current = context;
		_db.ApplyTenantScope(tenantId);
		return (tenantId, context);
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

		return Ok(ToSummary(entity));
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

		ScopeTo(tenantId);
		var entity = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
		if (entity is null) return NotFound();

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

		if (!_serializer.TryDeserialize(entity.DslJson, out var dsl, out var errors) || dsl is null)
			return StatusCode(500, new { errors });

		// P7.3：按仪表盘所属租户 + 仪表盘显式 ThemeKey 级联解析主题，注入渲染上下文。
		// 解析失败（或租户/键未命中）时 ThemeResolver 自动兜底内置默认，渲染永不失败。
		var themeContext = await _themeResolver.ResolveAsync(entity.TenantId, entity.ThemeKey, cancellationToken);
		var platformContext = (_accessor.Current ?? PlatformContext.System) with { Theme = themeContext };
		var model = await _renderer.RenderAsync(dsl, platformContext, cancellationToken);
		return Ok(model);
	}

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
		new(d.Id, d.TenantId, d.Code, d.Title, d.Description, d.Status, d.DslVersion, d.ThemeKey, d.CreatedTime);
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
	DateTime CreatedTime);

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
