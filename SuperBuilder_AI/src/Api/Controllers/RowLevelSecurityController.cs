using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Controllers;

[ApiController]
[Route("api/data-policies/row")]
public sealed class RowLevelSecurityController : ControllerBase
{
	private readonly SuperBIContext _db;
	private readonly IIdentityService _identity;

	public RowLevelSecurityController(SuperBIContext db, IIdentityService identity)
	{
		_db = db;
		_identity = identity;
	}

	[HttpGet]
	public async Task<IActionResult> List([FromQuery] long dataSourceId, CancellationToken ct)
	{
		var scope = await ScopeAsync(ct);
		if (scope.Error is not null) return scope.Error;
		return Ok(await _db.RowLevelSecurityPolicies.AsNoTracking()
			.Where(x => x.TenantId == scope.TenantId && x.DataSourceId == dataSourceId)
			.OrderBy(x => x.MetadataTableId).ThenBy(x => x.Id).ToListAsync(ct));
	}

	/// <summary>
	/// M12-18：跨数据源的行级策略管理视图。在原始实体之上补齐数据源名 / 表名 / 列名 / 数据类型
	/// 与主体显示名，供可视化管理页直接渲染（纯增量，不影响既有 List 契约）。
	/// </summary>
	[HttpGet("manage")]
	public async Task<IActionResult> Manage([FromQuery] long? dataSourceId, CancellationToken ct)
	{
		var scope = await ScopeAsync(ct);
		if (scope.Error is not null) return scope.Error;

		var query = _db.RowLevelSecurityPolicies.AsNoTracking().Where(x => x.TenantId == scope.TenantId);
		if (dataSourceId.HasValue && dataSourceId.Value > 0)
			query = query.Where(x => x.DataSourceId == dataSourceId.Value);

		var rows = await query
			.OrderBy(x => x.DataSourceId).ThenBy(x => x.MetadataTableId).ThenBy(x => x.Id)
			.Select(x => new
			{
				x.Id,
				x.DataSourceId,
				x.MetadataTableId,
				x.MetadataColumnId,
				DataSourceName = _db.DataSources.IgnoreQueryFilters().AsNoTracking()
					.Where(d => d.Id == x.DataSourceId).Select(d => d.Name).FirstOrDefault(),
				TableName = _db.MetadataTables.IgnoreQueryFilters().AsNoTracking()
					.Where(t => t.Id == x.MetadataTableId).Select(t => t.TableName).FirstOrDefault(),
				ColumnName = _db.MetadataColumns.IgnoreQueryFilters().AsNoTracking()
					.Where(c => c.Id == x.MetadataColumnId).Select(c => c.ColumnName).FirstOrDefault(),
				DataType = _db.MetadataColumns.IgnoreQueryFilters().AsNoTracking()
					.Where(c => c.Id == x.MetadataColumnId).Select(c => c.DataType).FirstOrDefault(),
				x.SubjectType,
				x.SubjectId,
				x.SubjectKey,
				x.SubjectValue,
				x.Effect,
				x.Operator,
				x.Value,
				x.Enabled,
				x.Version,
				x.UpdatedTime,
			})
			.ToListAsync(ct);

		var userIds = rows.Where(r => r.SubjectType == RowPolicySubjectType.User && r.SubjectId.HasValue)
			.Select(r => r.SubjectId!.Value).Distinct().ToList();
		var roleIds = rows.Where(r => r.SubjectType == RowPolicySubjectType.Role && r.SubjectId.HasValue)
			.Select(r => r.SubjectId!.Value).Distinct().ToList();

		var userNames = userIds.Count == 0
			? new Dictionary<long, string>()
			: (await _db.Users.IgnoreQueryFilters().AsNoTracking().Where(u => userIds.Contains(u.Id))
				.Select(u => new { u.Id, u.Username, u.DisplayName }).ToListAsync(ct))
				.ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.Username : u.DisplayName);
		var roleNames = roleIds.Count == 0
			? new Dictionary<long, string>()
			: (await _db.Roles.IgnoreQueryFilters().AsNoTracking().Where(r => roleIds.Contains(r.Id))
				.Select(r => new { r.Id, r.Name, r.Code }).ToListAsync(ct))
				.ToDictionary(r => r.Id, r => string.IsNullOrWhiteSpace(r.Name) ? r.Code : r.Name);

		var items = rows.Select(r => new
		{
			r.Id,
			r.DataSourceId,
			r.MetadataTableId,
			r.MetadataColumnId,
			DataSourceName = r.DataSourceName ?? string.Empty,
			TableName = r.TableName ?? string.Empty,
			ColumnName = r.ColumnName ?? string.Empty,
			DataType = r.DataType ?? string.Empty,
			r.SubjectType,
			r.SubjectId,
			r.SubjectKey,
			r.SubjectValue,
			SubjectDisplayName = r.SubjectType switch
			{
				RowPolicySubjectType.User => r.SubjectId.HasValue && userNames.TryGetValue(r.SubjectId.Value, out var un) ? un : string.Empty,
				RowPolicySubjectType.Role => r.SubjectId.HasValue && roleNames.TryGetValue(r.SubjectId.Value, out var rn) ? rn : string.Empty,
				RowPolicySubjectType.Attribute => $"{r.SubjectKey}={r.SubjectValue}",
				_ => string.Empty,
			},
			r.Effect,
			r.Operator,
			r.Value,
			r.Enabled,
			r.Version,
			r.UpdatedTime,
		}).ToList();

		return Ok(items);
	}

	[HttpPost]
	public async Task<IActionResult> Save([FromBody] RowLevelSecurityPolicyRequest request, CancellationToken ct)
	{
		var scope = await ScopeAsync(ct);
		if (scope.Error is not null) return scope.Error;
		if (request is null || request.DataSourceId <= 0 || request.MetadataTableId <= 0 || request.MetadataColumnId <= 0 ||
			!RlsVocabularyValidator.IsValidOperator(request.Operator) || !Enum.IsDefined(request.SubjectType) || !Enum.IsDefined(request.Effect))
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "行级策略参数无效。" });
		var normalizedOperator = RlsVocabularyValidator.NormalizeToSymbol(request.Operator!);
		if (normalizedOperator is not ("IS NULL" or "IS NOT NULL") && string.IsNullOrWhiteSpace(request.Value))
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "行级策略值不能为空。" });

		var bindingExists = await _db.MetadataColumns.AsNoTracking().Include(x => x.MetadataTable).AnyAsync(x =>
			x.Id == request.MetadataColumnId && x.MetadataTableId == request.MetadataTableId && x.MetadataTable != null &&
			x.MetadataTable.TenantId == scope.TenantId && x.MetadataTable.DataSourceId == request.DataSourceId, ct);
		if (!bindingExists || !await SubjectExistsAsync(scope.TenantId, request, ct))
			return StatusCode(403, new ApiError { Code = ErrorCodes.RowPolicyForbidden, Message = ErrorCodes.Message(ErrorCodes.RowPolicyForbidden) });

		RowLevelSecurityPolicy entity;
		if (request.Id > 0)
		{
			entity = await _db.RowLevelSecurityPolicies.FirstOrDefaultAsync(x => x.Id == request.Id && x.TenantId == scope.TenantId, ct)
				?? throw SuperBuilderException.FromCode(ErrorCodes.RowPolicyForbidden, 403);
			entity.Version++;
		}
		else
		{
			entity = new RowLevelSecurityPolicy { TenantId = scope.TenantId };
			_db.RowLevelSecurityPolicies.Add(entity);
		}
		entity.DataSourceId = request.DataSourceId;
		entity.MetadataTableId = request.MetadataTableId;
		entity.MetadataColumnId = request.MetadataColumnId;
		entity.SubjectType = request.SubjectType;
		entity.SubjectId = request.SubjectId;
		entity.SubjectKey = request.SubjectKey?.Trim();
		entity.SubjectValue = request.SubjectValue;
		entity.Effect = request.Effect;
		entity.Operator = normalizedOperator;
		entity.Value = request.Value ?? string.Empty;
		entity.Enabled = request.Enabled;
		entity.UpdatedTime = DateTime.UtcNow;
		await _db.SaveChangesAsync(ct);
		return Ok(entity);
	}

	[HttpDelete("{id:long}")]
	public async Task<IActionResult> Delete(long id, CancellationToken ct)
	{
		var scope = await ScopeAsync(ct);
		if (scope.Error is not null) return scope.Error;
		var entity = await _db.RowLevelSecurityPolicies.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == scope.TenantId, ct);
		if (entity is not null) { _db.Remove(entity); await _db.SaveChangesAsync(ct); }
		return NoContent();
	}

	/// <summary>M12-18：启用 / 停用一条行级策略。body 省略时按当前状态取反。</summary>
	[HttpPost("{id:long}/toggle")]
	public async Task<IActionResult> Toggle(long id, [FromBody] ToggleRowPolicyRequest? request, CancellationToken ct)
	{
		var scope = await ScopeAsync(ct);
		if (scope.Error is not null) return scope.Error;

		var entity = await _db.RowLevelSecurityPolicies
			.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == scope.TenantId, ct);
		if (entity is null)
			return NotFound(new ApiError { Code = ErrorCodes.BadRequest, Message = "行级策略不存在。" });

		entity.Enabled = request?.Enabled ?? !entity.Enabled;
		entity.Version++;
		entity.UpdatedTime = DateTime.UtcNow;
		await _db.SaveChangesAsync(ct);
		return Ok(new { entity.Id, entity.Enabled, entity.Version });
	}

	private async Task<bool> SubjectExistsAsync(long tenantId, RowLevelSecurityPolicyRequest request, CancellationToken ct) => request.SubjectType switch
	{
		RowPolicySubjectType.Everyone => true,
		RowPolicySubjectType.User => request.SubjectId.HasValue && await _db.Users.IgnoreQueryFilters().AnyAsync(x => x.Id == request.SubjectId && x.TenantId == tenantId, ct),
		RowPolicySubjectType.Role => request.SubjectId.HasValue && await _db.Roles.IgnoreQueryFilters().AnyAsync(x => x.Id == request.SubjectId && (x.TenantId == tenantId || x.TenantId == 0), ct),
		RowPolicySubjectType.Attribute => !string.IsNullOrWhiteSpace(request.SubjectKey) && request.SubjectValue is not null,
		_ => false
	};

	private async Task<(long TenantId, IActionResult? Error)> ScopeAsync(CancellationToken ct)
	{
		if (!long.TryParse(User.FindFirst("tid")?.Value, out var tenantId) || !long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
			return (0, Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" }));
		var canManageIdentity = await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.IdentityManage, ct);
		var canEditMetadata = await _identity.HasPermissionAsync(tenantId, userId, IdentityPermissions.MetadataEdit, ct);
		if (!canManageIdentity || !canEditMetadata)
			return (tenantId, StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少策略管理权限。" }));
		_db.ApplyTenantScope(tenantId);
		return (tenantId, null);
	}
}

public sealed class ToggleRowPolicyRequest
{
	public bool Enabled { get; set; }
}

public sealed class RowLevelSecurityPolicyRequest
{
	public long Id { get; set; }
	public long DataSourceId { get; set; }
	public long MetadataTableId { get; set; }
	public long MetadataColumnId { get; set; }
	public RowPolicySubjectType SubjectType { get; set; }
	public long? SubjectId { get; set; }
	public string? SubjectKey { get; set; }
	public string? SubjectValue { get; set; }
	public RowPolicyEffect Effect { get; set; } = RowPolicyEffect.Allow;
	public string? Operator { get; set; } = "=";
	public string? Value { get; set; }
	public bool Enabled { get; set; } = true;
}
