using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Services;
using SuperBuilder_AI.Interfaces;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Api.Diagnostics;


namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 元数据控制器，提供元数据扫描相关的管理接口。
/// 注意：当前示例 Scan 方法使用硬编码连接字符串，仅用于测试或本地调试，生产环境应通过配置或安全存储传入。
/// </summary>
public class MetadataController : Controller
{
	private readonly MetadataScannerService _scanner;
	private readonly IMetadataPromptBuilder _builder;
	private readonly IMetadataScanQueue _queue;
	private readonly SuperBIContext? _db;
	private readonly IDataSourceAuthorizationService? _authorization;
	private readonly IIdentityService? _identity;
	private readonly ScanBacklogGauge? _backlog;

	public MetadataController(
		MetadataScannerService scanner,
		IMetadataPromptBuilder builder,
		IMetadataScanQueue queue,
		SuperBIContext? db = null,
		IDataSourceAuthorizationService? authorization = null,
		IIdentityService? identity = null,
		ScanBacklogGauge? backlog = null)
	{
		_scanner = scanner;
		_builder = builder;
		_queue = queue;
		_db = db;
		_authorization = authorization;
		_identity = identity;
		_backlog = backlog;
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
		if (_db is null || _authorization is null || _identity is null || _queue is null)
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

		// M4-05：创建扫描任务并入队，由后台处理器异步执行；接口立即返回 202（任务 Id）。
		var job = new MetadataScanJob
		{
			TenantId = tenantId,
			DataSourceId = dataSourceId,
			TriggeredBy = userValue,
			Status = MetadataScanJobStatus.Queued,
			ProgressPercent = 0
		};
		_db.MetadataScanJobs.Add(job);
		await _db.SaveChangesAsync(cancellationToken);
		await _queue.EnqueueAsync(job.Id, cancellationToken);
		_backlog?.Increment();

		return StatusCode(202, new { jobId = job.Id, dataSourceId, status = job.Status.ToString() });
	}

	[HttpGet("/api/data-sources/{dataSourceId:long}/metadata/scan/{jobId:long}")]
	public async Task<IActionResult> GetScanJob(long dataSourceId, long jobId, CancellationToken cancellationToken)
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

		var job = await _db.MetadataScanJobs.AsNoTracking().FirstOrDefaultAsync(
			j => j.Id == jobId && j.DataSourceId == dataSourceId && j.TenantId == tenantId,
			cancellationToken);
		if (job is null)
			return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = "扫描任务不存在。" });

		return Ok(new
		{
			jobId = job.Id,
			dataSourceId = job.DataSourceId,
			status = job.Status.ToString(),
			progressPercent = job.ProgressPercent,
			tablesScanned = job.TablesScanned,
			columnsScanned = job.ColumnsScanned,
			orphansDetected = job.OrphansDetected,
			errorCode = job.ErrorCode,
			errorMessage = job.ErrorMessage,
			startedAt = job.StartedAt,
			finishedAt = job.FinishedAt
		});
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
