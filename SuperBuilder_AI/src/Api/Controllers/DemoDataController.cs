using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Interfaces.Seed;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 演示数据安装 API（M2-07 / DEC-05：独立安装器，不进生产默认种子）。
/// <list type="bullet">
///   <item><c>GET  api/demo-data/preview</c>：平台管理员预览将创建的演示内容。</item>
///   <item><c>POST api/demo-data/install</c>：平台管理员触发安装（事务原子、重复执行保护）。</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/demo-data")]
public sealed class DemoDataController : ControllerBase
{
    private readonly IDemoDataInstaller _installer;

    public DemoDataController(IDemoDataInstaller installer) => _installer = installer;

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(CancellationToken cancellationToken = default)
    {
        if (!User.HasClaim("perm", IdentityPermissions.PlatformAdminManage))
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 platform:admin:manage 权限。" });

        var plan = await _installer.PreviewAsync(cancellationToken);
        return Ok(plan);
    }

    [HttpPost("install")]
    public async Task<IActionResult> Install(CancellationToken cancellationToken = default)
    {
        if (!User.HasClaim("perm", IdentityPermissions.PlatformAdminManage))
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 platform:admin:manage 权限。" });

        var result = await _installer.InstallAsync(cancellationToken);
        return result.Status switch
        {
            "installed" => Ok(result),
            "already_installed" => Ok(result),
            _ => StatusCode(StatusCodes.Status500InternalServerError,
                new ApiError { Code = ErrorCodes.Internal, Message = result.Error ?? "演示数据安装失败。" }),
        };
    }
}
