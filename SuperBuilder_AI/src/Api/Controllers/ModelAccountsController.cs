using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Application.ModelAccounts;
using SuperBuilder_AI.Data;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 模型账号（BYO）管理端点（M7-07）。
///
/// <list type="bullet">
/// <item><c>POST /api/model-accounts</c>：创建租户模型绑定（API Key 经 <see cref="ISecretStore"/> 加密落库，明文绝不下发）。</item>
/// <item><c>GET /api/model-accounts</c>：按租户作用域列表（掩码）。</item>
/// <item><c>GET /api/model-accounts/{id}</c>：单个绑定摘要（掩码）。</item>
/// <item><c>PUT /api/model-accounts/{id}</c>：更新（可轮换 Key / 改备注）。</item>
/// <item><c>DELETE /api/model-accounts/{id}</c>：删除绑定。</item>
/// <item><c>POST /api/model-accounts/{id}/set-default</c>：设为租户默认（取消其它默认）。</item>
/// </list>
///
/// <para>租户隔离沿用 P4.3 的"显式开启"策略（同 ThemeController）：每个端点内调用 <see cref="TenantDataPlanePolicy.ResolvePlatformScope"/>。</para>
/// <para>本控制器属平台管理面，不触碰 BI 查询链路与 Golden 契约数据。</para>
/// </summary>
[ApiController]
[Route("api/model-accounts")]
public sealed class ModelAccountsController : ControllerBase
{
	private readonly SuperBIContext _db;
	private readonly ModelAccountService _service;

	public ModelAccountsController(SuperBIContext db, ModelAccountService service)
	{
		_db = db;
		_service = service;
	}

	/// <summary>在当前请求作用域内开启租户隔离，返回解析出的有效租户 Id。跨租户显式请求直接拒绝（403）。</summary>
	private long ScopeTo(long requestedTenantId)
	{
		var resolution = TenantDataPlanePolicy.ResolvePlatformScope(User, requestedTenantId);
		// P0-02B：把解析出的租户上下文写盘，供审计/可观测中间件读取。
		TenantDataPlanePolicy.StorePlatformScope(HttpContext, resolution, "ModelAccount");
		if (!resolution.Authorized)
			throw new SuperBuilderException(
				ErrorCodes.TenantIsolated,
				"禁止：租户作用域请求只能访问认证租户的数据，跨租户访问被拒绝。",
				403);
		_db.ApplyTenantScope(resolution.EffectiveTenantId);
		return resolution.EffectiveTenantId;
	}

	/// <summary>创建租户模型绑定：加密 API Key 后落库；首个绑定自动成为租户默认。</summary>
	[HttpPost]
	public async Task<IActionResult> Create(
		[FromBody] CreateModelAccountRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		try
		{
			var tenantId = ScopeTo(request.TenantId);
			var summary = await _service.CreateAsync(
				new CreateModelAccountRequest(tenantId, request.Provider, request.ModelId, request.ApiKey, request.DisplayName, request.Note),
				cancellationToken);
			return CreatedAtAction(nameof(GetById), new { id = summary.Id, tenantId }, summary);
		}
		catch (SuperBuilderException ex)
		{
			return StatusCode(ex.StatusCode, new { errors = new[] { ex.Message } });
		}
	}

	/// <summary>列表：按租户作用域返回（掩码）。</summary>
	[HttpGet]
	public async Task<IActionResult> List(
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		try
		{
			ScopeTo(tenantId);
			var items = await _service.ListAsync(tenantId, cancellationToken);
			return Ok(items);
		}
		catch (SuperBuilderException ex)
		{
			return StatusCode(ex.StatusCode, new { errors = new[] { ex.Message } });
		}
	}

	/// <summary>获取单个绑定摘要（掩码）。</summary>
	[HttpGet("{id}")]
	public async Task<IActionResult> GetById(
		long id,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		try
		{
			ScopeTo(tenantId);
			var item = await _service.GetAsync(tenantId, id, cancellationToken);
			return item is null ? NotFound() : Ok(item);
		}
		catch (SuperBuilderException ex)
		{
			return StatusCode(ex.StatusCode, new { errors = new[] { ex.Message } });
		}
	}

	/// <summary>更新绑定：可仅改备注/名称，或连同轮换 API Key。</summary>
	[HttpPut("{id}")]
	public async Task<IActionResult> Update(
		long id,
		[FromBody] UpdateModelAccountRequest request,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		try
		{
			ScopeTo(tenantId);
			var summary = await _service.UpdateAsync(tenantId, id, request, cancellationToken);
			return Ok(summary);
		}
		catch (SuperBuilderException ex)
		{
			return StatusCode(ex.StatusCode, new { errors = new[] { ex.Message } });
		}
	}

	/// <summary>删除绑定。</summary>
	[HttpDelete("{id}")]
	public async Task<IActionResult> Delete(
		long id,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		try
		{
			ScopeTo(tenantId);
			await _service.DeleteAsync(tenantId, id, cancellationToken);
			return NoContent();
		}
		catch (SuperBuilderException ex)
		{
			return StatusCode(ex.StatusCode, new { errors = new[] { ex.Message } });
		}
	}

	/// <summary>将指定绑定设为租户默认（取消其它默认）。</summary>
	[HttpPost("{id}/set-default")]
	public async Task<IActionResult> SetDefault(
		long id,
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (tenantId <= 0) return BadRequest("指派默认模型需要 tenantId > 0。");
		try
		{
			ScopeTo(tenantId);
			await _service.SetDefaultAsync(tenantId, id, cancellationToken);
			return Ok(new { tenantId, id, isDefault = true });
		}
		catch (SuperBuilderException ex)
		{
			return StatusCode(ex.StatusCode, new { errors = new[] { ex.Message } });
		}
	}
}
