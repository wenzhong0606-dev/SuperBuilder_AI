using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Models.Theme;
using SuperBuilder_AI.Services.Theming;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 多主题 / 样式引擎 管理端点（P7.4 Multi-Theme / Style Engine）。
///
/// <para>
/// 提供主题（结构化设计令牌文档）的租户作用域 CRUD、租户默认主题指派、复制/导出，以及编辑器蓝图：
/// <list type="bullet">
/// <item><c>POST /api/themes</c>：创建租户主题（先反序列化+校验 DSL，再落地冗余列与 DslJson）。</item>
/// <item><c>GET /api/themes</c>：按租户作用域列表（含内置/全局主题 TenantId=0）。</item>
/// <item><c>GET /api/themes/{key}</c>：获取单个主题（含完整 DSL；内置主题无 DB 行时由代码默认合成）。</item>
/// <item><c>PUT /api/themes/{key}</c>：更新主题（内置主题不可改）。</item>
/// <item><c>DELETE /api/themes/{key}</c>：删除主题（内置主题不可删）。</item>
/// <item><c>POST /api/themes/{key}/assign-default</c>：将主题指派为租户默认（写入 TenantSetting "theme:defaultKey"）。</item>
/// <item><c>POST /api/themes/{key}/copy</c>：从现有主题（含内置）复制为新的租户主题。</item>
/// <item><c>GET /api/themes/editor/blueprint</c>：编辑器蓝图（DSL 骨架 + 内置键清单），仅 JSON。</item>
/// </list>
/// </para>
///
/// <para>
/// 租户隔离沿用 P4.3 的"显式开启"策略：每个端点内调用 <see cref="SuperBIContext.ApplyTenantScope"/>。
/// 当前平台无 IAM 中间件，租户由 <c>tenantId</c> 查询参数显式传入（默认 0 = 系统/全局上下文）。
/// 内置主题（TenantId=0）对所有租户可见但不可被租户修改/删除。
/// </para>
///
/// <para>本控制器属平台管理面，不触碰 BI 查询链路与 Golden 契约数据，不影响 Golden 18/18 行为契约。</para>
/// </summary>
[ApiController]
[Route("api/themes")]
public sealed class ThemeController : ControllerBase
{
	private const string TenantDefaultThemeKey = "theme:defaultKey";

	private readonly SuperBIContext _db;

	public ThemeController(SuperBIContext db) => _db = db;

	/// <summary>在当前请求作用域内开启租户隔离，返回解析出的租户 Id。</summary>
	/// <summary>在当前请求作用域内开启租户隔离：有效租户恒为认证租户，跨租户显式请求直接拒绝。</summary>
	private long ScopeTo(long requestedTenantId)
	{
		var resolution = TenantDataPlanePolicy.ResolvePlatformScope(User, requestedTenantId);
		// P0-02B：把解析出的租户上下文写盘，供审计/可观测中间件读取；治理角色管理他租户时另记管理目标
		TenantDataPlanePolicy.StorePlatformScope(HttpContext, resolution, "Theme");
		if (!resolution.Authorized)
			throw new SuperBuilder_AI.Api.Errors.SuperBuilderException(
				SuperBuilder_AI.Api.Errors.ErrorCodes.TenantIsolated,
				"禁止：租户作用域请求只能访问认证租户的数据，跨租户访问被拒绝。",
				403);
		_db.ApplyTenantScope(resolution.EffectiveTenantId);
		return resolution.EffectiveTenantId;
	}

	/// <summary>创建租户主题：反序列化+校验 DSL，落地冗余列与 DslJson。</summary>
	[HttpPost]
	public async Task<IActionResult> Create(
		[FromBody] CreateThemeRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (request.TenantId <= 0) return BadRequest("不能通过 API 创建内置/全局主题（TenantId 必须 &gt; 0）。");
		if (string.IsNullOrWhiteSpace(request.Key)) return BadRequest("主题 Key 不能为空。");
		if (string.IsNullOrWhiteSpace(request.DslJson)) return BadRequest("DslJson 不能为空。");

		if (!ThemeDslSerializer.TryDeserialize(request.DslJson, out var dsl, out var errors) || dsl is null)
			return BadRequest(new { errors });

		var tenantId = ScopeTo(request.TenantId);

		// 同租户或内置已存在该 Key 则冲突（含与内置默认键重名）。
		var exists = await _db.Themes
			.IgnoreQueryFilters()
			.AnyAsync(t => t.Key == request.Key && (t.TenantId == tenantId || t.TenantId == 0), cancellationToken);
		if (exists) return Conflict(new { errors = new[] { $"主题键已存在：{request.Key}。" } });

		var entity = new Theme
		{
			TenantId = tenantId,
			Key = request.Key,
			Name = string.IsNullOrWhiteSpace(request.Name) ? request.Key : request.Name,
			IsBuiltIn = false,
			DslVersion = dsl.Version,
			DslJson = ThemeDslSerializer.Serialize(dsl),
		};

		_db.Themes.Add(entity);
		await _db.SaveChangesAsync(cancellationToken);

		return CreatedAtAction(nameof(GetByKey), new { key = entity.Key, tenantId }, ToDetail(entity));
	}

	/// <summary>列表：按租户作用域返回（含内置/全局主题 TenantId=0）。tenantId=0 为系统视图。</summary>
	[HttpGet]
	public async Task<IActionResult> List(
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		ScopeTo(tenantId);

		var items = await _db.Themes.AsNoTracking()
			.OrderBy(t => t.TenantId) // 内置(0) 在前，租户自有在后
			.ThenBy(t => t.Key)
			.ToListAsync(cancellationToken);
		return Ok(items.Select(ToSummary).ToList());
	}

	/// <summary>获取单个主题（含完整 DSL）。内置主题无 DB 行时由代码默认合成。</summary>
	[HttpGet("{key}")]
	public async Task<IActionResult> GetByKey(
		string key,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(key)) return BadRequest("主题 Key 不能为空。");
		ScopeTo(tenantId);

		var entity = await _db.Themes.AsNoTracking()
			.FirstOrDefaultAsync(t => t.Key == key, cancellationToken);
		if (entity is not null) return Ok(ToDetail(entity));

		// 内置主题可能未落库行（解析器在缺失时回退到代码默认）；此处合成一个只读视图。
		if (key == BuiltInThemeKeys.Default)
		{
			return Ok(new ThemeDetail(
				Id: 0,
				TenantId: 0,
				Key: BuiltInThemeKeys.Default,
				Name: "平台默认（内置浅色）",
				IsBuiltIn: true,
				DslVersion: ThemeDslVersions.Current,
				DslJson: ThemeDslSerializer.Serialize(BuiltInThemes.DefaultDsl())));
		}

		return NotFound();
	}

	/// <summary>更新主题：重新校验 DSL 后覆盖冗余列与 DslJson（内置主题不可改）。</summary>
	[HttpPut("{key}")]
	public async Task<IActionResult> Update(
		string key,
		[FromBody] UpdateThemeRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (string.IsNullOrWhiteSpace(request.DslJson)) return BadRequest("DslJson 不能为空。");

		if (!ThemeDslSerializer.TryDeserialize(request.DslJson, out var dsl, out var errors) || dsl is null)
			return BadRequest(new { errors });

		ScopeTo(tenantId);
		var entity = await _db.Themes.FirstOrDefaultAsync(t => t.Key == key, cancellationToken);
		if (entity is null) return NotFound();
		if (entity.IsBuiltIn || entity.TenantId == 0)
			return BadRequest(new { errors = new[] { "内置主题不可修改。" } });

		entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
		entity.DslVersion = dsl.Version;
		entity.DslJson = ThemeDslSerializer.Serialize(dsl);

		await _db.SaveChangesAsync(cancellationToken);
		return Ok(ToDetail(entity));
	}

	/// <summary>删除主题（内置主题不可删）。</summary>
	[HttpDelete("{key}")]
	public async Task<IActionResult> Delete(
		string key,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(key)) return BadRequest("主题 Key 不能为空。");
		ScopeTo(tenantId);
		var entity = await _db.Themes.FirstOrDefaultAsync(t => t.Key == key, cancellationToken);
		if (entity is null) return NotFound();
		if (entity.IsBuiltIn || entity.TenantId == 0)
			return BadRequest(new { errors = new[] { "内置主题不可删除。" } });

		_db.Themes.Remove(entity);
		await _db.SaveChangesAsync(cancellationToken);
		return NoContent();
	}

	/// <summary>
	/// 将主题指派为租户默认（写入 TenantSetting "theme:defaultKey"）。指派后 P7.2 级联解析将命中该主题。
	/// </summary>
	[HttpPost("{key}/assign-default")]
	public async Task<IActionResult> AssignDefault(
		string key,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(key)) return BadRequest("主题 Key 不能为空。");
		if (tenantId <= 0) return BadRequest("指派租户默认主题需要 tenantId &gt; 0。");

		// 校验主题对当前租户（含内置）可见且存在。
		ScopeTo(tenantId);
		var theme = await _db.Themes.AsNoTracking()
			.FirstOrDefaultAsync(t => t.Key == key, cancellationToken);
		if (theme is null)
		{
			if (key == BuiltInThemeKeys.Default)
				theme = new Theme { TenantId = 0, Key = BuiltInThemeKeys.Default, IsBuiltIn = true };
			else
				return NotFound(new { errors = new[] { $"主题不存在：{key}。" } });
		}

		// 租户必须存在（TenantSetting 含指向 Tenant 的外键）。
		var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken);
		if (!tenantExists) return BadRequest(new { errors = new[] { $"租户不存在：{tenantId}。" } });

		// M1-02：租户侧写入须通过策略校验（Key 允许目录且非锁定安全配置）。
		if (!TenantSettingPolicy.ValidateTenantWrite(TenantDefaultThemeKey, out var policyError))
			return BadRequest(new { errors = new[] { policyError } });

		var setting = await _db.TenantSettings
			.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == TenantDefaultThemeKey, cancellationToken);
		if (setting is null)
		{
			setting = new TenantSetting
			{
				TenantId = tenantId,
				Key = TenantDefaultThemeKey,
				Value = key,
				DataType = "string",
			};
			_db.TenantSettings.Add(setting);
		}
		else
		{
			setting.Value = key;
			setting.DataType = "string";
		}

		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new { tenantId, themeKey = key, assigned = true });
	}

	/// <summary>
	/// 从现有主题（含内置）复制为新的租户主题：克隆 DSL，换 Key/Name，归到目标租户。
	/// </summary>
	[HttpPost("{key}/copy")]
	public async Task<IActionResult> Copy(
		string key,
		[FromBody] CopyThemeRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		if (string.IsNullOrWhiteSpace(key)) return BadRequest("源主题 Key 不能为空。");
		if (string.IsNullOrWhiteSpace(request.TargetKey)) return BadRequest("目标主题 Key 不能为空。");

		// 解析源 DSL：先查 DB（租户作用域可见；内置无行则合成）。
		var scopeTenant = ScopeTo(tenantId);

		// M0-06：忽略请求体 TargetTenantId，目标租户恒为认证租户（生产由 AuthMiddleware 从 JWT 注入 tid）；
		// 测试/遗留显式租户模式（无 tid 声明）回退到请求 tenantId。杜绝租户管理员伪造他租户主题。
		var authenticatedTenant = User?.FindFirst("tid") is { } tidClaim && long.TryParse(tidClaim.Value, out var at) ? at : 0;
		var targetTenant = authenticatedTenant > 0 ? authenticatedTenant : scopeTenant;
		if (targetTenant <= 0) return BadRequest("目标租户必须 &gt; 0（不能通过复制创建内置主题）。");

		var source = await _db.Themes.AsNoTracking()
			.FirstOrDefaultAsync(t => t.Key == key, cancellationToken);
		string? sourceDslJson = source?.DslJson;
		if (sourceDslJson is null && key == BuiltInThemeKeys.Default)
			sourceDslJson = ThemeDslSerializer.Serialize(BuiltInThemes.DefaultDsl());

		if (sourceDslJson is null) return NotFound(new { errors = new[] { $"源主题不存在：{key}。" } });
		if (!ThemeDslSerializer.TryDeserialize(sourceDslJson, out var dsl, out var errors) || dsl is null)
			return BadRequest(new { errors });

		// 目标键冲突检测（目标租户自身 ∪ 内置）。
		var conflict = await _db.Themes
			.IgnoreQueryFilters()
			.AnyAsync(t => t.Key == request.TargetKey && (t.TenantId == targetTenant || t.TenantId == 0), cancellationToken);
		if (conflict) return Conflict(new { errors = new[] { $"目标主题键已存在：{request.TargetKey}。" } });

		var entity = new Theme
		{
			TenantId = targetTenant,
			Key = request.TargetKey,
			Name = string.IsNullOrWhiteSpace(request.TargetName) ? request.TargetKey : request.TargetName,
			IsBuiltIn = false,
			DslVersion = dsl.Version,
			DslJson = ThemeDslSerializer.Serialize(dsl),
		};

		_db.Themes.Add(entity);
		await _db.SaveChangesAsync(cancellationToken);

		return CreatedAtAction(nameof(GetByKey), new { key = entity.Key, tenantId = entity.TenantId }, ToDetail(entity));
	}

	/// <summary>
	/// 编辑器蓝图：返回主题 DSL 骨架与可用内置键清单，供编辑器初始化。
	/// 仅结构化 JSON，绝不承载 HTML。
	/// </summary>
	[HttpGet("editor/blueprint")]
	public IActionResult EditorBlueprint()
	{
		var skeleton = new ThemeDsl
		{
			Brand = new ThemeBrand { Primary = "#2563eb", Accent = "#7c3aed" },
			Color = new ThemeColor
			{
				Primary = "#2563eb",
				Success = "#16a34a",
				Warning = "#d97706",
				Danger = "#dc2626",
				Neutral = "#64748b",
				Background = "#ffffff",
				Surface = "#f8fafc",
				Text = "#0f172a",
				TextMuted = "#64748b",
				Border = "#e2e8f0",
			},
			Component = new ThemeComponent
			{
				CardShowBorder = true,
				CardPadding = "normal",
				ButtonPrimaryBg = "primary",
				TableStriped = true,
				KpiValueColor = "primary",
				KpiGoodColor = "success",
			},
			DashboardTemplate = new ThemeDashboardTemplate
			{
				Background = "background",
				Surface = "surface",
				HeaderStyle = "standard",
			},
		};

		return Ok(new ThemeEditorBlueprint(
			DslVersion: ThemeDslVersions.Current,
			BuiltInKeys: new[] { BuiltInThemeKeys.Default },
			Skeleton: ThemeDslSerializer.Serialize(skeleton)));
	}

	private static ThemeSummary ToSummary(Theme t) =>
		new(t.Id, t.TenantId, t.Key, t.Name, t.IsBuiltIn, t.DslVersion);

	private static ThemeDetail ToDetail(Theme t) =>
		new(t.Id, t.TenantId, t.Key, t.Name, t.IsBuiltIn, t.DslVersion, t.DslJson);

	#region Request / Response DTOs
	/// <summary>创建主题请求体。</summary>
	public sealed record CreateThemeRequest(
		long TenantId,
		string Key,
		string DslJson,
		string? Name = null);

	/// <summary>更新主题请求体。</summary>
	public sealed record UpdateThemeRequest(
		string DslJson,
		string? Name = null);

	/// <summary>复制主题请求体。</summary>
	public sealed record CopyThemeRequest(
		long TargetTenantId,
		string TargetKey,
		string? TargetName = null);

	/// <summary>主题摘要 DTO。</summary>
	public sealed record ThemeSummary(
		long Id,
		long TenantId,
		string Key,
		string Name,
		bool IsBuiltIn,
		string DslVersion);

	/// <summary>主题详情 DTO（含完整 DSL）。</summary>
	public sealed record ThemeDetail(
		long Id,
		long TenantId,
		string Key,
		string Name,
		bool IsBuiltIn,
		string DslVersion,
		string DslJson);

	/// <summary>编辑器蓝图 DTO（结构化，无 HTML）。</summary>
	public sealed record ThemeEditorBlueprint(
		string DslVersion,
		IReadOnlyList<string> BuiltInKeys,
		string Skeleton);
	#endregion
}
