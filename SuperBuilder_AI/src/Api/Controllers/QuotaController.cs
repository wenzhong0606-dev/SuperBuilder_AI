using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces.Quota;
using SuperBuilder_AI.Models.Quota;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 租户配额端点（api/quota）。配额是租户级概念，仅对 TenantId&gt;0 作用域有效。
/// 所有逻辑委托 IQuotaService（确定性，不调 LLM），不影响 Golden 行为契约。
/// 租户标识通过 [FromQuery] tenantId 显式传入（与 IdentityController 一致，测试无需 HttpContext）。
/// </summary>
[ApiController]
[Route("api/quota")]
public class QuotaController : ControllerBase
{
    private readonly IQuotaService _quota;

    public QuotaController(IQuotaService quota) => _quota = quota;

    private static bool ParseResource(string s, out QuotaResourceType rt)
    {
        rt = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return Enum.TryParse<QuotaResourceType>(s.Trim(), true, out rt);
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (tenantId <= 0) return BadRequest("配额查询仅对租户作用域有效（TenantId>0）。");
        var overview = await _quota.GetQuotaAsync(tenantId, ct);
        return Ok(overview);
    }

    [HttpGet("{resourceType}")]
    public async Task<IActionResult> GetOne(string resourceType, [FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (tenantId <= 0) return BadRequest("配额查询仅对租户作用域有效（TenantId>0）。");
        if (!ParseResource(resourceType, out var rt)) return BadRequest($"未知资源类型：{resourceType}");
        var check = await _quota.CheckAsync(tenantId, rt, 0, ct);
        return Ok(check);
    }

    [HttpPost("check")]
    public async Task<IActionResult> Check([FromBody] QuotaCheckRequest req, [FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (tenantId <= 0) return BadRequest("配额校验仅对租户作用域有效（TenantId>0）。");
        if (!ParseResource(req.ResourceType, out var rt)) return BadRequest($"未知资源类型：{req.ResourceType}");
        var res = await _quota.CheckAsync(tenantId, rt, req.Requested, ct);
        return Ok(res);
    }

    [HttpPost("consume")]
    public async Task<IActionResult> Consume([FromBody] QuotaConsumeRequest req, [FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (tenantId <= 0) return BadRequest("配额扣减仅对租户作用域有效（TenantId>0）。");
        if (!ParseResource(req.ResourceType, out var rt)) return BadRequest($"未知资源类型：{req.ResourceType}");
        if (req.Amount <= 0) return BadRequest("Amount 须为正数。");
        var ok = await _quota.ConsumeAsync(tenantId, rt, req.Amount, ct);
        if (!ok) return Conflict(new { message = "配额不足。", allowed = false });
        var after = await _quota.CheckAsync(tenantId, rt, 0, ct);
        return Ok(after);
    }
}
