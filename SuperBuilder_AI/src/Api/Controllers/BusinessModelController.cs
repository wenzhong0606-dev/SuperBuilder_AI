using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Interfaces.BI.Entity;
using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 业务模型（P3 Business Semantic Model）API：
/// 暴露业务实体 / 业务域列举，自然语言 → 业务实体语义解析，以及业务实体的新建/编辑/删除（M7-05 CRUD 真实持久化）。
/// 不修改任何运行时查询链路，仅提供业务语义层的可观测与可编辑能力。
/// </summary>
[ApiController]
[Route("api/business-model")]
public sealed class BusinessModelController : ControllerBase
{
    private readonly IBusinessEntityRegistryService _registry;
    private readonly IBusinessSemanticMappingService _mapper;
    private readonly IBusinessEntityService _entities;

    public BusinessModelController(IBusinessEntityRegistryService registry, IBusinessSemanticMappingService mapper, IBusinessEntityService entities)
    {
        _registry = registry;
        _mapper = mapper;
        _entities = entities;
    }

    [HttpGet("entities")]
    public async Task<IActionResult> ListEntities([FromQuery] long? tenantId, CancellationToken cancellationToken = default)
    {
		var resolution = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!resolution.Authorized) return TenantMismatch();
		return Ok(await _registry.ListEntitiesAsync(resolution.EffectiveTenantId, cancellationToken));
	}

    [HttpGet("domains")]
    public async Task<IActionResult> ListDomains([FromQuery] long? tenantId, CancellationToken cancellationToken = default)
    {
		var resolution = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!resolution.Authorized) return TenantMismatch();
		return Ok(await _registry.ListDomainsAsync(resolution.EffectiveTenantId, cancellationToken));
	}

	/// <summary>M12-15：按租户列举业务实体之间的语义关系，供关系图渲染边。租户隔离由注册表服务保证（两端实体均须属于该租户）。</summary>
	[HttpGet("relationships")]
	public async Task<IActionResult> ListRelationships([FromQuery] long? tenantId, CancellationToken cancellationToken = default)
	{
		var resolution = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!resolution.Authorized) return TenantMismatch();
		return Ok(await _registry.ListRelationshipsAsync(resolution.EffectiveTenantId, cancellationToken));
	}

	/// <summary>M12-16：按租户列举指标治理视图（指标中心）。租户隔离由注册表服务经所属实体的 TenantId 保证。</summary>
	[HttpGet("metrics")]
	public async Task<IActionResult> ListMetrics([FromQuery] long? tenantId, CancellationToken cancellationToken = default)
	{
		var resolution = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!resolution.Authorized) return TenantMismatch();
		return Ok(await _registry.ListMetricsAsync(resolution.EffectiveTenantId, cancellationToken));
	}

	/// <summary>M12-16：按租户列举维度治理视图（指标中心）。</summary>
	[HttpGet("dimensions")]
	public async Task<IActionResult> ListDimensionsByTenant([FromQuery] long? tenantId, CancellationToken cancellationToken = default)
	{
		var resolution = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!resolution.Authorized) return TenantMismatch();
		return Ok(await _registry.ListDimensionsByTenantAsync(resolution.EffectiveTenantId, cancellationToken));
	}

    [HttpGet("resolve")]
    public async Task<IActionResult> Resolve(
        [FromQuery] long? tenantId,
        [FromQuery] long dataSourceId,
        [FromQuery] string q,
        [FromQuery] int topPerDomain = 3,
        CancellationToken cancellationToken = default)
    {
		var resolution = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!resolution.Authorized) return TenantMismatch();
		return Ok(await _mapper.ResolveAsync(resolution.EffectiveTenantId, dataSourceId, q, topPerDomain, cancellationToken));
	}

	/// <summary>按 Id 获取单个业务实体详情（M0-02 补齐契约）。租户隔离由注册表服务保证。</summary>
	[HttpGet("entities/{id:long}")]
	public async Task<IActionResult> GetEntity(long id, [FromQuery] long? tenantId, CancellationToken cancellationToken = default)
	{
		var resolution = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!resolution.Authorized) return TenantMismatch();
		var entity = await _registry.GetAsync(resolution.EffectiveTenantId, id, cancellationToken);
		if (entity is null) return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = "实体不存在或不属于当前租户。" });
		return Ok(entity);
	}

	/// <summary>M7-05：新建业务实体（真实持久化）。租户隔离由数据面策略保证，实体 TenantId 固定为解析租户。</summary>
	[HttpPost("entities")]
	public async Task<IActionResult> CreateEntity([FromBody] BusinessEntityUpsertRequest dto, [FromQuery] long? tenantId, CancellationToken cancellationToken = default)
	{
		var resolution = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!resolution.Authorized) return TenantMismatch();
		if (dto is null || string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.BusinessKey))
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "BusinessKey 与 Name 为必填。" });
		var entity = ToEntity(resolution.EffectiveTenantId, dto);
		var created = await _entities.CreateAsync(entity, cancellationToken);
		return CreatedAtAction(nameof(GetEntity), new { id = created.Id }, created);
	}

	/// <summary>M7-05：编辑业务实体（真实持久化）。Id 与租户隔离由数据面策略保证；先取回带正确 RowVersion 的实体再映射，避免乐观并发冲突。</summary>
	[HttpPut("entities/{id:long}")]
	public async Task<IActionResult> UpdateEntity(long id, [FromBody] BusinessEntityUpsertRequest dto, [FromQuery] long? tenantId, CancellationToken cancellationToken = default)
	{
		var resolution = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!resolution.Authorized) return TenantMismatch();
		if (dto is null || string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.BusinessKey))
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "BusinessKey 与 Name 为必填。" });
		var existing = await _entities.GetAsync(resolution.EffectiveTenantId, id, cancellationToken);
		if (existing is null) return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = "实体不存在或不属于当前租户。" });
		MapDto(existing, dto);
		existing.TenantId = resolution.EffectiveTenantId;
		try
		{
			var updated = await _entities.UpdateAsync(existing, cancellationToken);
			return Ok(updated);
		}
		catch (KeyNotFoundException)
		{
			return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = "实体不存在或不属于当前租户。" });
		}
	}

	/// <summary>M7-05：删除业务实体（真实持久化）。幂等：不存在亦返回 204。</summary>
	[HttpDelete("entities/{id:long}")]
	public async Task<IActionResult> DeleteEntity(long id, [FromQuery] long? tenantId, CancellationToken cancellationToken = default)
	{
		var resolution = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!resolution.Authorized) return TenantMismatch();
		await _entities.DeleteAsync(resolution.EffectiveTenantId, id, cancellationToken);
		return NoContent();
	}

	/// <summary>M12 增量：按业务实体全量合并其指标（含计算口径 Expression/DataType）。租户隔离由数据面策略 + 服务层双重保证。</summary>
	[HttpPut("entities/{id:long}/metrics")]
	public async Task<IActionResult> UpsertMetrics(long id, [FromBody] List<BusinessEntityMetricUpsertRequest> dto, [FromQuery] long? tenantId, CancellationToken cancellationToken = default)
	{
		var resolution = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!resolution.Authorized) return TenantMismatch();
		if (dto is null || dto.Count == 0)
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "指标列表不能为空。" });
		if (dto.Any(m => string.IsNullOrWhiteSpace(m.Name)))
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "每个指标 Name 为必填。" });
		var metrics = dto.Select(ToMetric).ToList();
		try
		{
			await _entities.UpsertMetricsAsync(resolution.EffectiveTenantId, id, metrics, cancellationToken);
		}
		catch (KeyNotFoundException)
		{
			return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = "业务实体不存在或不属于当前租户。" });
		}
		return NoContent();
	}

	/// <summary>M12 增量：按业务域全量合并其维度（含维度表达式 Expression/DataType）。租户隔离由数据面策略 + 服务层双重保证。</summary>
	[HttpPut("domains/{domainId:long}/dimensions")]
	public async Task<IActionResult> UpsertDimensions(long domainId, [FromBody] List<BusinessEntityDimensionUpsertRequest> dto, [FromQuery] long? tenantId, CancellationToken cancellationToken = default)
	{
		var resolution = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!resolution.Authorized) return TenantMismatch();
		if (dto is null || dto.Count == 0)
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "维度列表不能为空。" });
		if (dto.Any(d => string.IsNullOrWhiteSpace(d.Name)))
			return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = "每个维度 Name 为必填。" });
		var dimensions = dto.Select(ToDimension).ToList();
		try
		{
			await _entities.UpsertDimensionsAsync(resolution.EffectiveTenantId, domainId, dimensions, cancellationToken);
		}
		catch (KeyNotFoundException)
		{
			return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = "业务域不存在或不属于当前租户。" });
		}
		return NoContent();
	}

	private static BusinessEntity ToEntity(long tenantId, BusinessEntityUpsertRequest dto)
		=> new BusinessEntity
		{
			TenantId = tenantId,
			BusinessKey = dto.BusinessKey.Trim(),
			Name = dto.Name.Trim(),
			DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? null : dto.DisplayName.Trim(),
			Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
			BusinessDomain = string.IsNullOrWhiteSpace(dto.BusinessDomain) ? null : dto.BusinessDomain.Trim(),
			SemanticText = string.IsNullOrWhiteSpace(dto.SemanticText) ? null : dto.SemanticText.Trim(),
			Status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status.Trim(),
		};

	private static void MapDto(BusinessEntity entity, BusinessEntityUpsertRequest dto)
	{
		entity.BusinessKey = dto.BusinessKey.Trim();
		entity.Name = dto.Name.Trim();
		entity.DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? null : dto.DisplayName.Trim();
		entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
		entity.BusinessDomain = string.IsNullOrWhiteSpace(dto.BusinessDomain) ? null : dto.BusinessDomain.Trim();
		entity.SemanticText = string.IsNullOrWhiteSpace(dto.SemanticText) ? null : dto.SemanticText.Trim();
		entity.Status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status.Trim();
	}

	private static BusinessEntityMetric ToMetric(BusinessEntityMetricUpsertRequest dto) => new()
	{
		Name = dto.Name.Trim(),
		DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? null : dto.DisplayName.Trim(),
		Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
		SemanticType = string.IsNullOrWhiteSpace(dto.SemanticType) ? null : dto.SemanticType.Trim(),
		Aggregation = string.IsNullOrWhiteSpace(dto.Aggregation) ? null : dto.Aggregation.Trim(),
		IsCalculated = dto.IsCalculated,
		Expression = string.IsNullOrWhiteSpace(dto.Expression) ? null : dto.Expression.Trim(),
		DataType = string.IsNullOrWhiteSpace(dto.DataType) ? null : dto.DataType.Trim(),
	};

	private static BusinessEntityDimension ToDimension(BusinessEntityDimensionUpsertRequest dto) => new()
	{
		Name = dto.Name.Trim(),
		Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
		Expression = string.IsNullOrWhiteSpace(dto.Expression) ? null : dto.Expression.Trim(),
		DataType = string.IsNullOrWhiteSpace(dto.DataType) ? null : dto.DataType.Trim(),
	};

	private ObjectResult TenantMismatch() => StatusCode(403,
		new ApiError { Code = ErrorCodes.TenantIsolated, Message = "禁止：数据面请求租户必须与认证租户一致。" });
}
