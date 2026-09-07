using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Interfaces.Quota;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Quota;
using SuperBuilder_AI.Services.Identity;

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
    private readonly IPlatformAdminScopeService? _scope;

    public QuotaController(IQuotaService quota, IPlatformAdminScopeService? scope = null)
    {
        _quota = quota;
        _scope = scope;
    }

    private static bool ParseResource(string s, out QuotaResourceType rt)
    {
        rt = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return Enum.TryParse<QuotaResourceType>(s.Trim(), true, out rt);
    }

    private static bool ParseWindow(string s, out QuotaWindow window)
    {
        window = default;
        return !string.IsNullOrWhiteSpace(s)
            && Enum.TryParse(s.Trim(), true, out window)
            && Enum.IsDefined(window);
    }

    private long CallerId()
    {
        var raw = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(raw, out var id) ? id : 0;
    }

    /// <summary>
    /// 平台管理员可在其授权范围内查看/维护目标租户；普通租户主体只能读取和扣减自身配额。
    /// requireManager=true 的策略与用量维护端点必须持有 platform:quota:manage。
    /// </summary>
    private async Task<IActionResult?> AuthorizeTargetAsync(
        long tenantId,
        bool requireManager,
        string action,
        CancellationToken ct)
    {
        var isManager = User.HasClaim("perm", IdentityPermissions.PlatformQuotaManage);
        if (!isManager)
        {
            if (requireManager)
                return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = $"禁止：缺少 {IdentityPermissions.PlatformQuotaManage} 权限。" });

            var rawTid = User.FindFirst("tid")?.Value;
            var authenticatedTenantId = long.TryParse(rawTid, out var tid) ? tid : 0;
            if (tenantId <= 0 || tenantId != authenticatedTenantId)
                return StatusCode(403, new ApiError { Code = ErrorCodes.TenantIsolated, Message = "禁止：只能访问认证租户的配额。" });
            return null;
        }

        if (tenantId > 0 && _scope is not null && CallerId() > 0
            && !await _scope.CanManageAsync(CallerId(), tenantId, ct))
            return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = $"禁止：目标租户 {tenantId} 不在您的授权管理范围内。" });

        if (tenantId > 0)
            TenantDataPlanePolicy.StoreManagementTarget(HttpContext, tenantId, action, true);
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (tenantId <= 0) return BadRequest("配额查询仅对租户作用域有效（TenantId>0）。");
        if (await AuthorizeTargetAsync(tenantId, false, "quota.read", ct) is { } denied) return denied;
        var overview = await _quota.GetQuotaAsync(tenantId, ct);
        return Ok(overview);
    }

    /// <summary>平台默认策略（TenantId=0）；仅平台配额管理员可见。</summary>
    [HttpGet("defaults")]
    public async Task<IActionResult> GetDefaults(CancellationToken ct = default)
    {
        if (await AuthorizeTargetAsync(0, true, "quota.defaults.read", ct) is { } denied) return denied;
        return Ok(await _quota.GetQuotaAsync(0, ct));
    }

    [HttpGet("{resourceType}")]
    public async Task<IActionResult> GetOne(string resourceType, [FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (tenantId <= 0) return BadRequest("配额查询仅对租户作用域有效（TenantId>0）。");
        if (!ParseResource(resourceType, out var rt)) return BadRequest($"未知资源类型：{resourceType}");
        if (await AuthorizeTargetAsync(tenantId, false, "quota.read", ct) is { } denied) return denied;
        var check = await _quota.CheckAsync(tenantId, rt, 0, ct);
        return Ok(check);
    }

    [HttpPost("check")]
    public async Task<IActionResult> Check([FromBody] QuotaCheckRequest req, [FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (tenantId <= 0) return BadRequest("配额校验仅对租户作用域有效（TenantId>0）。");
        if (!ParseResource(req.ResourceType, out var rt)) return BadRequest($"未知资源类型：{req.ResourceType}");
        if (await AuthorizeTargetAsync(tenantId, false, "quota.check", ct) is { } denied) return denied;
        var res = await _quota.CheckAsync(tenantId, rt, req.Requested, ct);
        return Ok(res);
    }

    [HttpPost("consume")]
    public async Task<IActionResult> Consume([FromBody] QuotaConsumeRequest req, [FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (tenantId <= 0) return BadRequest("配额扣减仅对租户作用域有效（TenantId>0）。");
        if (!ParseResource(req.ResourceType, out var rt)) return BadRequest($"未知资源类型：{req.ResourceType}");
        if (req.Amount <= 0) return BadRequest("Amount 须为正数。");
        if (await AuthorizeTargetAsync(tenantId, false, "quota.consume", ct) is { } denied) return denied;
        var ok = await _quota.ConsumeAsync(tenantId, rt, req.Amount, ct);
        if (!ok) return Conflict(new { message = "配额不足。", allowed = false });
        var after = await _quota.CheckAsync(tenantId, rt, 0, ct);
        return Ok(after);
    }

    /// <summary>新增或更新平台默认（tenantId=0）或目标租户覆盖策略。</summary>
    [HttpPut("policy/{resourceType}")]
    public async Task<IActionResult> UpsertPolicy(
        string resourceType,
        [FromBody] QuotaPolicyUpdateRequest req,
        [FromQuery] long tenantId = 0,
        CancellationToken ct = default)
    {
        if (!ParseResource(resourceType, out var rt)) return BadRequest($"未知资源类型：{resourceType}");
        if (!ParseWindow(req.Window, out var window)) return BadRequest($"未知周期窗口：{req.Window}");
        if (req.Limit < 0) return BadRequest("Limit 不能为负数。");
        if (await AuthorizeTargetAsync(tenantId, true, "quota.policy.upsert", ct) is { } denied) return denied;
        try
        {
            return Ok(await _quota.UpsertPolicyAsync(tenantId, rt, req.Limit, window, ct));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = ex.Message });
        }
    }

    /// <summary>删除租户覆盖策略，恢复继承平台默认。</summary>
    [HttpDelete("policy/{resourceType}")]
    public async Task<IActionResult> RemoveOverride(
        string resourceType,
        [FromQuery] long tenantId = 0,
        CancellationToken ct = default)
    {
        if (tenantId <= 0) return BadRequest("平台默认策略不可删除。");
        if (!ParseResource(resourceType, out var rt)) return BadRequest($"未知资源类型：{resourceType}");
        if (await AuthorizeTargetAsync(tenantId, true, "quota.policy.inherit", ct) is { } denied) return denied;
        var removed = await _quota.RemoveTenantOverrideAsync(tenantId, rt, ct);
        if (!removed) return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = "该资源未配置租户覆盖。" });
        return Ok(await _quota.GetQuotaAsync(tenantId, ct));
    }

    /// <summary>维护目标租户当前周期已用量。</summary>
    [HttpPut("usage/{resourceType}")]
    public async Task<IActionResult> SetUsage(
        string resourceType,
        [FromBody] QuotaUsageUpdateRequest req,
        [FromQuery] long tenantId = 0,
        CancellationToken ct = default)
    {
        if (tenantId <= 0) return BadRequest("用量只能维护到实际租户。");
        if (!ParseResource(resourceType, out var rt)) return BadRequest($"未知资源类型：{resourceType}");
        if (req.Used < 0) return BadRequest("Used 不能为负数。");
        if (await AuthorizeTargetAsync(tenantId, true, "quota.usage.set", ct) is { } denied) return denied;
        try
        {
            return Ok(await _quota.SetUsageAsync(tenantId, rt, req.Used, ct));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = ex.Message });
        }
    }
}
