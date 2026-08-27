using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces.BI.Entity;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Phase 3.1 Business Entity Runtime Verification HTTP 入口。
/// 仅用于本地 Runtime / Golden 验证，不改变生产 QueryPlan Pipeline。
/// </summary>
[ApiController]
[Route("evaluation/business-entity")]
public sealed class BusinessEntityRuntimeController : ControllerBase
{
    private readonly IBusinessEntityService _entityService;
    private readonly IPhysicalBindingResolver _bindingResolver;
    private readonly IEntityQueryPlanMapper _mapper;

    public BusinessEntityRuntimeController(IBusinessEntityService entityService, IPhysicalBindingResolver bindingResolver, IEntityQueryPlanMapper mapper)
    {
        _entityService = entityService;
        _bindingResolver = bindingResolver;
        _mapper = mapper;
    }

    [HttpPost("resolve")]
    public async Task<ActionResult<object>> Resolve([FromBody] ResolveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId <= 0 || request.DataSourceId <= 0 || request.BusinessEntityId <= 0)
            return BadRequest(new { passed = false, message = "TenantId/DataSourceId/BusinessEntityId must be positive." });

        var entity = await _entityService.GetAsync(request.TenantId, request.BusinessEntityId, cancellationToken);
        if (entity is null)
            return NotFound(new { passed = false, message = "BusinessEntity not found in tenant scope." });

        var bindings = await _bindingResolver.ResolveAsync(request.TenantId, request.DataSourceId, request.BusinessEntityId, cancellationToken);

        try
        {
            var resolution = await _mapper.MapAsync(entity, request.Intent, request.DataSourceId, cancellationToken);
            return Ok(new
            {
                passed = resolution.Tables.Count > 0 &&
                         resolution.Metrics.Count == request.Intent.Metrics.Count &&
                         resolution.Dimensions.Count == request.Intent.Dimensions.Count &&
                         resolution.Filters.Count == request.Intent.Filters.Count,
                caseId = request.CaseId,
                tenantId = request.TenantId,
                dataSourceId = request.DataSourceId,
                businessEntityId = request.BusinessEntityId,
                bindingCount = bindings.Count,
                resolution
            });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { passed = false, caseId = request.CaseId, tenantId = request.TenantId, dataSourceId = request.DataSourceId, businessEntityId = request.BusinessEntityId, bindingCount = bindings.Count, error = ex.Message });
        }
    }

    [HttpGet("bindings")]
    public async Task<ActionResult<object>> Bindings([FromQuery] long tenantId, [FromQuery] long dataSourceId, [FromQuery] long businessEntityId, CancellationToken cancellationToken = default)
    {
        var bindings = await _bindingResolver.ResolveAsync(tenantId, dataSourceId, businessEntityId, cancellationToken);
        return Ok(new
        {
            passed = bindings.All(x => x.DataSourceId == dataSourceId),
            tenantId, dataSourceId, businessEntityId, count = bindings.Count,
            bindings = bindings.Select(x => new { x.Id, x.DataSourceId, x.MetadataTableId, x.MetadataColumnId, x.Priority, x.IsActive })
        });
    }

    [HttpGet("negative-data-source")]
    public async Task<ActionResult<object>> NegativeDataSource([FromQuery] long tenantId, [FromQuery] long dataSourceId, [FromQuery] long businessEntityId, CancellationToken cancellationToken = default)
    {
        var bindings = await _bindingResolver.ResolveAsync(tenantId, dataSourceId, businessEntityId, cancellationToken);
        return Ok(new { passed = bindings.Count == 0, expected = "No Binding / no cross-DataSource fallback", tenantId, dataSourceId, businessEntityId, bindingCount = bindings.Count });
    }

    public sealed record ResolveRequest(string CaseId, long TenantId, long DataSourceId, long BusinessEntityId, QueryIntent Intent);
}
