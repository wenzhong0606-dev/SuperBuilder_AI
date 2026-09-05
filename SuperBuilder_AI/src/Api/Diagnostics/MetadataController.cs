using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Services;


namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 元数据控制器，提供元数据扫描相关的管理接口。
/// 注意：当前示例 Scan 方法使用硬编码连接字符串，仅用于测试或本地调试，生产环境应通过配置或安全存储传入。
/// </summary>
public class MetadataController : Controller
{
	private readonly MetadataScannerService _scanner;
	private readonly IMetadataPromptBuilder _builder;
	private readonly SuperBIContext? _db;
	private readonly IDataSourceAuthorizationService? _authorization;
	private readonly IIdentityService? _identity;

	public MetadataController(
		MetadataScannerService scanner,
		IMetadataPromptBuilder builder,
		SuperBIContext? db = null,
		IDataSourceAuthorizationService? authorization = null,
		IIdentityService? identity = null)
	{
		_scanner = scanner;
		_builder = builder;
		_db = db;
		_authorization = authorization;
		_identity = identity;
	}

	/// <summary>
	/// 触发元数据扫描并将结果持久化到本地数据库。
	/// </summary>
	/// <returns>操作结果内容（扫描完成）。</returns>
	[HttpGet]
	public IActionResult Scan()
	{
		return StatusCode(410, new ApiError
		{
			Code = ErrorCodes.BadRequest,
			Message = "旧版固定数据源扫描入口已停用，请使用受授权的数据源扫描接口。"
		});
	}

	[HttpPost("/api/data-sources/{dataSourceId:long}/metadata/scan")]
	public async Task<IActionResult> ScanDataSource(long dataSourceId, CancellationToken cancellationToken)
	{
		if (_db is null || _authorization is null || _identity is null)
			return StatusCode(503, new ApiError { Code = ErrorCodes.BadRequest, Message = "元数据扫描服务未就绪。" });

		var tenantValue = User.FindFirst("tid")?.Value;
		var userValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (!long.TryParse(tenantValue, out var tenantId) || !long.TryParse(userValue, out var userId))
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });
		if (!await _identity.HasPermissionAsync(tenantId, userId, SuperBuilder_AI.Models.Identity.IdentityPermissions.MetadataScan, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 metadata:scan 权限。" });

		if (!await _authorization.IsAuthorizedAsync(tenantId, userId, dataSourceId, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.DataSourceForbidden, Message = ErrorCodes.Message(ErrorCodes.DataSourceForbidden) });

		var source = await _db.DataSources.AsNoTracking().FirstOrDefaultAsync(
			x => x.Id == dataSourceId && x.TenantId == tenantId && x.Enabled == true,
			cancellationToken);
		if (source is null || string.IsNullOrWhiteSpace(source.ConnectionString))
			return StatusCode(403, new ApiError { Code = ErrorCodes.DataSourceForbidden, Message = ErrorCodes.Message(ErrorCodes.DataSourceForbidden) });

		await _scanner.ScanAsync(tenantId, dataSourceId, source.ConnectionString);
		var scanned = await _db.DataSources.FindAsync(dataSourceId, cancellationToken);
		if (scanned is not null)
		{
			scanned.LastScanAt = DateTime.UtcNow;
			await _db.SaveChangesAsync(cancellationToken);
		}
		return Ok(new { dataSourceId, status = "completed" });
	}

	[HttpGet("Prompt")]
	public async Task<IActionResult> Prompt(string question)
	{

		var result =
			await _builder
			.BuildAsync(question);



		return Content(
			result.Prompt);

	}
}
