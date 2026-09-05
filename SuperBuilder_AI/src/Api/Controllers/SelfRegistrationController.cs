using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 租户自助注册 API（M2-06 骨架）。
/// <list type="bullet">
/// <item><c>GET  api/self-registration/status</c>：匿名探查是否开放（仅返回 enabled 布尔，不含敏感配置）。</item>
/// <item><c>POST api/self-registration/register</c>：匿名注册新租户与首位管理员；受 <c>SelfRegistration:Enabled</c> 开关保护，默认关闭。</item>
/// <item><c>GET  api/self-registration/config</c>：平台管理员查看当前配置（是否开放、域名白名单、默认语言等）。</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/self-registration")]
public sealed class SelfRegistrationController : ControllerBase
{
    private readonly ISelfRegistrationService _service;
    private readonly SelfRegistrationOptions _options;

    public SelfRegistrationController(ISelfRegistrationService service, IOptions<SelfRegistrationOptions> options)
    {
        _service = service;
        _options = options.Value;
    }

    /// <summary>匿名探查自助注册是否开放（仅暴露布尔，避免泄露配置细节）。</summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public IActionResult Status() => Ok(new { enabled = _options.Enabled });

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] SelfRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest(new { error = "请求体不能为空。" });

        var result = await _service.RegisterAsync(request, cancellationToken);
        return result.Status switch
        {
            "ok" => Ok(result),
            "disabled" => StatusCode(StatusCodes.Status403Forbidden,
                new ApiError { Code = ErrorCodes.Forbidden, Message = result.Error ?? "自助注册未开放。" }),
            "feature_not_implemented" => StatusCode(StatusCodes.Status501NotImplemented,
                new ApiError { Code = ErrorCodes.Unsupported, Message = result.Error ?? "该功能尚未实现。" }),
            "conflict" => StatusCode(StatusCodes.Status409Conflict,
                new ApiError { Code = ErrorCodes.BadRequest, Message = result.Error ?? "资源冲突。" }),
            _ => BadRequest(new { error = result.Error ?? "请求无效。" }),
        };
    }

    [HttpGet("config")]
    public IActionResult Config()
    {
        if (!User.HasClaim("perm", IdentityPermissions.PlatformAdminManage))
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 platform:admin:manage 权限。" });

        return Ok(new SelfRegistrationConfigView(
            _options.Enabled,
            _options.AllowedEmailDomains ?? System.Array.Empty<string>(),
            _options.DefaultCulture,
            _options.DefaultAvailableCultures ?? new[] { "zh-CN" },
            _options.ApprovalRequired,
            _options.RequireCaptcha));
    }
}

/// <summary>自助注册配置视图（平台管理员只读）。</summary>
public sealed record SelfRegistrationConfigView(
    bool Enabled,
    string[] AllowedEmailDomains,
    string DefaultCulture,
    string[] DefaultAvailableCultures,
    bool ApprovalRequired,
    bool RequireCaptcha);
