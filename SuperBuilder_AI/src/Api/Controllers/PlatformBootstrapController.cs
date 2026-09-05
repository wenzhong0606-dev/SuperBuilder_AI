using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;

    public PlatformBootstrapController(PlatformAdminBootstrapper bootstrapper, IConfiguration configuration)
    {
        _bootstrapper = bootstrapper;
        _configuration = configuration;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        try
        {
            var status = await _bootstrapper.GetStatusAsync(ct);
            // 生产可经 PlatformBootstrap:AllowAnonymous=false 关闭匿名初始化，改由部署配置（Username/Password）完成。
            var allowAnonymous = _configuration.GetValue("PlatformBootstrap:AllowAnonymous", true);
            return Ok(new
            {
                status = status.ToString(),
                required = status == BootstrapStatus.NeedsInitialization,
                anonymousAllowed = allowAnonymous,
                platformTenantId = await _bootstrapper.GetPlatformTenantIdAsync(ct),
            });
        }
        catch (Exception ex)
        {
            // 数据库不可达 / Schema 未创建等情况下返回可诊断状态，而非 500 + 堆栈
            return StatusCode(503, new ApiError
            {
                Code = ErrorCodes.ServiceUnavailable,
                Message = $"平台尚未就绪：{ex.Message}",
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PlatformBootstrapRequest request, CancellationToken ct)
    {
        // 生产环境可禁用匿名初始化：关闭后仅允许部署配置（PlatformBootstrap:Username/Password）完成首位管理员创建。
        var allowAnonymous = _configuration.GetValue("PlatformBootstrap:AllowAnonymous", true);
        if (!allowAnonymous)
            return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "匿名初始化已禁用，请通过部署配置完成平台初始化。" });

        // 服务端仅信任真实连接来源：RemoteIpAddress 经受控 ForwardedHeaders 还原，
        // 未配置 KnownProxies/KnownNetworks 时不消费任何 X-Forwarded-*，避免伪造客户端 IP。
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp is null || !IPAddress.IsLoopback(remoteIp))
            return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "首次平台初始化仅允许在服务器本机执行。" });
        try
        {
            var created = await _bootstrapper.CreateAsync(
                request.Username ?? string.Empty, request.Password ?? string.Empty, request.DisplayName,
                request.Email, request.ConfirmPassword, ct);
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

public sealed record PlatformBootstrapRequest(
    string? Username,
    string? Password,
    string? DisplayName,
    string? Email,
    string? ConfirmPassword);
