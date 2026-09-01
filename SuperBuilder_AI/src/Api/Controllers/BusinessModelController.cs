using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Interfaces.BI.Entity;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 业务模型（P3 Business Semantic Model）只读 API：
/// 暴露业务实体 / 业务域列举，以及自然语言 → 业务实体语义解析。
/// 不修改任何运行时查询链路，仅提供业务语义层的可观测能力。
/// </summary>
[ApiController]
[Route("api/business-model")]
public sealed class BusinessModelController : ControllerBase
{
    private readonly IBusinessEntityRegistryService _registry;
    private readonly IBusinessSemanticMappingService _mapper;

    public BusinessModelController(IBusinessEntityRegistryService registry, IBusinessSemanticMappingService mapper)
    {
        _registry = registry;
        _mapper = mapper;
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

	private ObjectResult TenantMismatch() => StatusCode(403,
		new ApiError { Code = ErrorCodes.TenantIsolated, Message = "禁止：数据面请求租户必须与认证租户一致。" });
}
