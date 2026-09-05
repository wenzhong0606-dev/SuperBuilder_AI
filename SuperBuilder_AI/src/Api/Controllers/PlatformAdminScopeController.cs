using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.Identity;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 平台管理员租户范围治理端点（M2-02）。
/// 提供查询与设置某平台管理员的「可管理租户范围」：无范围记录 = 全部租户（默认），有记录 = 仅所列租户。
/// 属平台级管理面，调用者须持有 <c>platform:admin:manage</c> 权限；结构独立于业务/BI 数据与 Golden 契约。
/// </summary>
[ApiController]
[Route("api/platform-admin")]
public sealed class PlatformAdminScopeController : ControllerBase
{
    private readonly IPlatformAdminScopeService _scope;

    public PlatformAdminScopeController(IPlatformAdminScopeService scope) => _scope = scope;

    private IActionResult? RequireManage() =>
        User.HasClaim("perm", IdentityPermissions.PlatformAdminManage)
            ? null
            : StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 platform:admin:manage 权限。" });

    private string Actor() => User.Identity?.Name ?? "system";

    /// <summary>获取指定平台管理员的租户范围视图。</summary>
    [HttpGet("{id:long}/scope")]
    public async Task<IActionResult> GetScope(long id, CancellationToken ct)
    {
        if (RequireManage() is { } denied) return denied;
        var view = await _scope.GetScopeAsync(id, ct);
        return Ok(view);
    }

    /// <summary>
    /// 设置指定平台管理员的租户范围。tenantIds 为空 = 恢复默认（全部租户）；
    /// 非空 = 仅授权所列（不存在的租户会被忽略，仅保留真实存在的租户）。
    /// </summary>
    [HttpPut("{id:long}/scope")]
    public async Task<IActionResult> SetScope(long id, [FromBody] SetPlatformAdminScopeRequest request, CancellationToken ct)
    {
        if (RequireManage() is { } denied) return denied;
        var tenantIds = request?.TenantIds ?? Array.Empty<long>();
        var view = await _scope.SetScopeAsync(id, tenantIds, Actor(), ct);
        return Ok(view);
    }
}
