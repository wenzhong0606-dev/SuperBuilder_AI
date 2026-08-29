using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 租户管理 API（P4 Multi-Tenant Platform Core 生产端点）。
///
/// 提供租户的列举 / 查询 / 创建 / 启用停用。
/// 本控制器只读与写 <c>Organization.Tenant</c>，不触碰任何查询链路与 Golden 契约数据；
/// 属平台级管理面，不影响 Golden 18/18 行为契约。
/// </summary>
[ApiController]
[Route("api/tenant-management")]
public sealed class TenantManagementController : ControllerBase
{
	private readonly SuperBIContext _db;

	public TenantManagementController(SuperBIContext db)
	{
		_db = db;
	}

	[HttpGet]
	public async Task<IActionResult> List(CancellationToken cancellationToken = default)
	{
		var tenants = await _db.Tenants
			.AsNoTracking()
			.OrderBy(t => t.Id)
			.Select(t => new TenantSummary(t.Id, t.TenantCode, t.TenantName, t.Enabled))
			.ToListAsync(cancellationToken);
		return Ok(tenants);
	}

	[HttpGet("{id:long}")]
	public async Task<IActionResult> Get(long id, CancellationToken cancellationToken = default)
	{
		var t = await _db.Tenants
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (t is null) return NotFound();
		return Ok(new TenantSummary(t.Id, t.TenantCode, t.TenantName, t.Enabled));
	}

	[HttpPost]
	public async Task<IActionResult> Create(
		[FromBody] CreateTenantRequest request,
		CancellationToken cancellationToken = default)
	{
		var code = (request.TenantCode ?? string.Empty).Trim();
		if (code.Length == 0)
			return BadRequest("TenantCode 不能为空。");

		if (await _db.Tenants.AnyAsync(t => t.TenantCode == code, cancellationToken))
			return Conflict($"租户编码 {code} 已存在。");

		var tenant = new Tenant
		{
			TenantCode = code,
			TenantName = (request.TenantName ?? string.Empty).Trim(),
			Enabled = true
		};
		_db.Tenants.Add(tenant);
		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new TenantSummary(tenant.Id, tenant.TenantCode, tenant.TenantName, tenant.Enabled));
	}

	[HttpPatch("{id:long}/enable")]
	public async Task<IActionResult> Enable(long id, CancellationToken cancellationToken = default)
		=> await SetEnabledAsync(id, true, cancellationToken);

	[HttpPatch("{id:long}/disable")]
	public async Task<IActionResult> Disable(long id, CancellationToken cancellationToken = default)
		=> await SetEnabledAsync(id, false, cancellationToken);

	private async Task<IActionResult> SetEnabledAsync(long id, bool enabled, CancellationToken cancellationToken)
	{
		var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (t is null) return NotFound();
		t.Enabled = enabled;
		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new TenantSummary(t.Id, t.TenantCode, t.TenantName, t.Enabled));
	}

	[HttpGet("{id:long}/settings")]
	public async Task<IActionResult> ListSettings(long id, CancellationToken cancellationToken = default)
	{
		if (!await _db.Tenants.AnyAsync(t => t.Id == id, cancellationToken))
			return NotFound($"租户 {id} 不存在。");

		var settings = await _db.TenantSettings
			.AsNoTracking()
			.Where(s => s.TenantId == id)
			.OrderBy(s => s.Key)
			.Select(s => new TenantSettingSummary(s.Id, s.Key, s.Value, s.DataType))
			.ToListAsync(cancellationToken);
		return Ok(settings);
	}

	[HttpPost("{id:long}/settings")]
	public async Task<IActionResult> UpsertSetting(
		long id,
		[FromBody] UpsertTenantSettingRequest request,
		CancellationToken cancellationToken = default)
	{
		var key = (request.Key ?? string.Empty).Trim();
		if (key.Length == 0)
			return BadRequest("Key 不能为空。");

		if (!await _db.Tenants.AnyAsync(t => t.Id == id, cancellationToken))
			return NotFound($"租户 {id} 不存在。");

		var existing = await _db.TenantSettings
			.FirstOrDefaultAsync(s => s.TenantId == id && s.Key == key, cancellationToken);
		if (existing is null)
		{
			existing = new TenantSetting { TenantId = id, Key = key };
			_db.TenantSettings.Add(existing);
		}
		existing.Value = request.Value;
		existing.DataType = (request.DataType ?? "string").Trim();
		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new TenantSettingSummary(existing.Id, existing.Key, existing.Value, existing.DataType));
	}
}

/// <summary>租户摘要 DTO。</summary>
public sealed record TenantSummary(long Id, string? TenantCode, string? TenantName, bool Enabled);

/// <summary>创建租户请求。</summary>
public sealed record CreateTenantRequest(string? TenantCode, string? TenantName);

/// <summary>租户配置项 DTO。</summary>
public sealed record TenantSettingSummary(long Id, string Key, string? Value, string? DataType);

/// <summary>写入租户配置项请求。</summary>
public sealed record UpsertTenantSettingRequest(string? Key, string? Value, string? DataType);
