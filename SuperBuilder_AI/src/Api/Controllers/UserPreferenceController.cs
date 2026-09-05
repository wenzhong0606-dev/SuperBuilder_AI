using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces.Localization;
using System.Security.Claims;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 用户级偏好端点（M3-G0「用户语言恢复」）。
/// 仅认证用户可访问；语言偏好按 (TenantId, UserId) 维度持久化，写入由服务强制校验租户范围。
/// </summary>
[ApiController]
[Route("api/user/preferences")]
public sealed class UserPreferenceController : ControllerBase
{
    private readonly IUserLanguagePreferenceService _lang;

    public UserPreferenceController(IUserLanguagePreferenceService lang)
    {
        _lang = lang;
    }

    private (long TenantId, long UserId) CurrentPrincipal()
    {
        var tenantId = long.TryParse(User.FindFirst("tid")?.Value, out var t) ? t : 0;
        var userId = long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var u) ? u : 0;
        return (tenantId, userId);
    }

    /// <summary>读取当前用户的界面语言偏好（无记录时 culture 为 null，前端回退租户默认）。</summary>
    [HttpGet("language")]
    public async Task<IActionResult> GetLanguage(CancellationToken cancellationToken = default)
    {
        if (User?.Identity is not { IsAuthenticated: true }) return Unauthorized(new { error = "未认证。" });
        var (tenantId, userId) = CurrentPrincipal();
        var culture = await _lang.GetAsync(tenantId, userId, cancellationToken);
        return Ok(new { culture });
    }

    /// <summary>设置当前用户的界面语言偏好；越出租户可用范围时回退租户默认。</summary>
    [HttpPut("language")]
    public async Task<IActionResult> SetLanguage([FromBody] SetLanguageRequest? request, CancellationToken cancellationToken = default)
    {
        if (User?.Identity is not { IsAuthenticated: true }) return Unauthorized(new { error = "未认证。" });
        if (request is null || string.IsNullOrWhiteSpace(request.Culture))
            return BadRequest(new { error = "culture 必填。" });
        var (tenantId, userId) = CurrentPrincipal();
        if (tenantId <= 0 || userId <= 0) return BadRequest(new { error = "无法解析当前主体。" });
        var effective = await _lang.SetAsync(tenantId, userId, request.Culture, cancellationToken);
        return Ok(new { culture = effective });
    }
}

/// <summary>设置语言请求。</summary>
public sealed record SetLanguageRequest(string? Culture);
