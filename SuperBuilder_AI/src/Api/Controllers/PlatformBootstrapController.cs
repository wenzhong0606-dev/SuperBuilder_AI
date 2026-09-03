using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Services.Identity;

namespace SuperBuilder_AI.Controllers;

/// <summary>一次性平台初始化入口：仅无平台管理员时开放，创建操作仅允许本机请求。</summary>
[ApiController]
[AllowAnonymous]
[Route("api/platform-bootstrap")]
public sealed class PlatformBootstrapController : ControllerBase
{
    private readonly PlatformAdminBootstrapper _bootstrapper;

    public PlatformBootstrapController(PlatformAdminBootstrapper bootstrapper) => _bootstrapper = bootstrapper;

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct) =>
        Ok(new
        {
            required = !await _bootstrapper.HasAdministratorAsync(ct),
            platformTenantId = await _bootstrapper.GetPlatformTenantIdAsync(ct),
        });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PlatformBootstrapRequest request, CancellationToken ct)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp is null || !IPAddress.IsLoopback(remoteIp))
            return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "首次平台初始化仅允许在服务器本机执行。" });
        try
        {
            var created = await _bootstrapper.CreateAsync(
                request.Username ?? string.Empty, request.Password ?? string.Empty, request.DisplayName, ct);
            return Ok(new { tenantId = created.TenantId, userId = created.UserId, username = request.Username });
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
}

public sealed record PlatformBootstrapRequest(string? Username, string? Password, string? DisplayName);
