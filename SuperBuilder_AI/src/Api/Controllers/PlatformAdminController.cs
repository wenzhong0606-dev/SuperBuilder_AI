using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.Identity;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 平台管理员常态化治理端点（列表/新增/停用/启用/重置密码）。
/// 仅平台治理主体（持有 platform:* 权限声明）可访问；匿名或非治理账号返回 403。
/// 与一次性 <c>PlatformBootstrapController</c> 不同，本端点用于把 platform-admin 授予额外治理账号。
/// </summary>
[ApiController]
[Route("api/platform-admin")]
public sealed class PlatformAdminController : ControllerBase
{
    private readonly IPlatformAdminService _service;

    public PlatformAdminController(IPlatformAdminService service) => _service = service;

    private IActionResult? EnsureGovernance()
    {
        if (!GovernanceDataPlanePolicy.IsGovernancePrincipal(User))
            return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "仅平台治理管理员可执行此操作。" });
        return null;
    }

    private string Actor() => User.Identity?.Name ?? "system";

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var guard = EnsureGovernance();
        if (guard is not null) return guard;
        try
        {
            return Ok(await _service.ListAsync(ct));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new ApiError { Code = ErrorCodes.ServiceUnavailable, Message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddPlatformAdminRequest request, CancellationToken ct)
    {
        var guard = EnsureGovernance();
        if (guard is not null) return guard;
        try
        {
            return Ok(await _service.AddAsync(request, Actor(), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiError { Code = ErrorCodes.BadRequest, Message = ex.Message });
        }
    }

    [HttpPost("{id}/disable")]
    public async Task<IActionResult> Disable(long id, CancellationToken ct)
    {
        var guard = EnsureGovernance();
        if (guard is not null) return guard;
        try
        {
            await _service.DisableAsync(id, Actor(), ct);
            return Ok(new { id, status = nameof(UserStatus.Disabled) });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiError { Code = ErrorCodes.BadRequest, Message = ex.Message });
        }
    }

    [HttpPost("{id}/enable")]
    public async Task<IActionResult> Enable(long id, CancellationToken ct)
    {
        var guard = EnsureGovernance();
        if (guard is not null) return guard;
        try
        {
            await _service.EnableAsync(id, Actor(), ct);
            return Ok(new { id, status = nameof(UserStatus.Active) });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = ex.Message });
        }
    }

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(long id, [FromBody] ResetPlatformAdminPasswordRequest request, CancellationToken ct)
    {
        var guard = EnsureGovernance();
        if (guard is not null) return guard;
        try
        {
            await _service.ResetPasswordAsync(id, request.NewPassword, Actor(), ct);
            return Ok(new { id, status = "password-reset" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError { Code = ErrorCodes.BadRequest, Message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = ex.Message });
        }
    }
}
