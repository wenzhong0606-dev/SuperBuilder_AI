using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Services;
using SuperBuilder_AI.Interfaces;
using System.Security.Claims;
using System.Text.Json;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Api.Diagnostics;
using SuperBuilder_AI.Api.Background;
using SuperBuilder_AI.Interfaces.Audit;


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
	private readonly IAuditLogService? _audit;
	private readonly MetadataScanHostedService? _scanHost;

	public MetadataController(
		MetadataScannerService scanner,
		IMetadataPromptBuilder builder,
		IMetadataScanQueue queue,
		SuperBIContext? db = null,
		IDataSourceAuthorizationService? authorization = null,
		IIdentityService? identity = null,
		ScanBacklogGauge? backlog = null,
		IAuditLogService? audit = null,
		MetadataScanHostedService? scanHost = null)
	{
		_scanner = scanner;
		_builder = builder;
		_queue = queue;
		_db = db;
		_authorization = authorization;
		_identity = identity;
		_backlog = backlog;
		_audit = audit;
		_scanHost = scanHost;
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
	public async Task<IActionResult> ScanDataSource(long dataSourceId, CancellationToken cancellationToken, [FromBody] ScanScope? scope = null)
	{
		if (_db is null || _authorization is null || _identity is null || _queue is null)
			return StatusCode(503, new ApiError { Code = ErrorCodes.BadRequest, Message = "元数据扫描服务未就绪。" });

		var tenantValue = User.FindFirst("tid")?.Value;
		var userValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (!long.TryParse(tenantValue, out var tenantId) || !long.TryParse(userValue, out var userId))
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });
		if (!await _identity.HasPermissionAsync(tenantId, userId, SuperBuilder_AI.Models.Identity.IdentityPermissions.MetadataScan, cancellationToken))
		{
			await AuditScanAsync(tenantId, userId, userValue, dataSourceId, "failure", "缺少 metadata:scan 权限。");
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 metadata:scan 权限。" });
		}

		if (!await _authorization.IsAuthorizedAsync(tenantId, userId, dataSourceId, cancellationToken))
		{
			await AuditScanAsync(tenantId, userId, userValue, dataSourceId, "failure", "数据源未授权。");
			return StatusCode(403, new ApiError { Code = ErrorCodes.DataSourceForbidden, Message = ErrorCodes.Message(ErrorCodes.DataSourceForbidden) });
		}

		// C9 同源去重：已有进行中任务则 409 + 当前 jobId，不重复入队。
		var activeJob = await _db.MetadataScanJobs.AsNoTracking()
			.Where(j => j.DataSourceId == dataSourceId && j.TenantId == tenantId
				&& (j.Status == MetadataScanJobStatus.Queued
					|| j.Status == MetadataScanJobStatus.Running
					|| j.Status == MetadataScanJobStatus.Cancelling
					|| j.Status == MetadataScanJobStatus.Retrying))
			.OrderByDescending(j => j.Id)
			.FirstOrDefaultAsync(cancellationToken);
		if (activeJob is not null)
		{
			await AuditScanAsync(tenantId, userId, userValue, dataSourceId, "success", $"已有进行中的扫描任务 jobId={activeJob.Id}，未重复入队。");
			return StatusCode(409, new ApiError
			{
				Code = ErrorCodes.Conflict,
				Message = $"该数据源已有进行中的扫描任务（jobId={activeJob.Id}），请等待完成后重试。"
			});
		}

		var source = await _db.DataSources.AsNoTracking().FirstOrDefaultAsync(
			x => x.Id == dataSourceId && x.TenantId == tenantId && x.Enabled == true,
			cancellationToken);
		if (source is null || string.IsNullOrWhiteSpace(source.ConnectionString))
			return StatusCode(403, new ApiError { Code = ErrorCodes.DataSourceForbidden, Message = ErrorCodes.Message(ErrorCodes.DataSourceForbidden) });

		// C2：MySQL 下 Databases 与 Schemas 同义（均指向 database 名），二者若同时指定且不一致则拒绝请求。
		if (scope is not null && source.DbType == "MYSQL"
			&& scope.Databases is { Count: > 0 } && scope.Schemas is { Count: > 0 }
			&& !scope.Databases.Intersect(scope.Schemas, StringComparer.OrdinalIgnoreCase).Any())
		{
			return BadRequest(new ApiError
			{
				Code = ErrorCodes.BadRequest,
				Message = "MySQL 下 Databases 与 Schemas 均指定且不一致，请仅指定其一。"
			});
		}

		// M4-05：创建扫描任务并入队，由后台处理器异步执行；接口立即返回 202（任务 Id）。
		var job = new MetadataScanJob
		{
			TenantId = tenantId,
			DataSourceId = dataSourceId,
			TriggeredBy = userValue,
			Status = MetadataScanJobStatus.Queued,
			ProgressPercent = 0,
			// C2：持久化范围，后台处理器按 §L.6 从 active 选种仅应用 scope 内表。
			ScopeJson = scope is null ? null : JsonSerializer.Serialize(scope)
		};

		try
		{
			_db.MetadataScanJobs.Add(job);
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch (Microsoft.EntityFrameworkCore.DbUpdateException)
		{
			// 并发竞态下过滤唯一索引兜底：已有活动任务则视为冲突。
			return StatusCode(409, new ApiError
			{
				Code = ErrorCodes.Conflict,
				Message = "该数据源已有进行中的扫描任务，请等待完成后重试。"
			});
		}

		await _queue.EnqueueAsync(job.Id, cancellationToken);
		_backlog?.Increment();

		await AuditScanAsync(tenantId, userId, userValue, dataSourceId, "success", $"已入队扫描任务 jobId={job.Id}。");
		return StatusCode(202, new { jobId = job.Id, dataSourceId, status = job.Status.ToString() });
	}

	[HttpPost("/api/data-sources/{dataSourceId:long}/metadata/scan/{jobId:long}/cancel")]
	public async Task<IActionResult> CancelScan(long dataSourceId, long jobId, CancellationToken cancellationToken)
	{
		if (_db is null || _authorization is null || _identity is null)
			return StatusCode(503, new ApiError { Code = ErrorCodes.BadRequest, Message = "元数据扫描服务未就绪。" });

		var tenantValue = User.FindFirst("tid")?.Value;
		var userValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (!long.TryParse(tenantValue, out var tenantId) || !long.TryParse(userValue, out var userId))
			return Unauthorized(new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：令牌声明缺失。" });
		if (!await _identity.HasPermissionAsync(tenantId, userId, SuperBuilder_AI.Models.Identity.IdentityPermissions.MetadataCancelScan, cancellationToken))
		{
			await AuditScanAsync(tenantId, userId, userValue, dataSourceId, "failure", "缺少 metadata:cancel_scan 权限。");
			return StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = "禁止：缺少 metadata:cancel_scan 权限。" });
		}

		if (!await _authorization.IsAuthorizedAsync(tenantId, userId, dataSourceId, cancellationToken))
			return StatusCode(403, new ApiError { Code = ErrorCodes.DataSourceForbidden, Message = ErrorCodes.Message(ErrorCodes.DataSourceForbidden) });

		var job = await _db.MetadataScanJobs.AsNoTracking()
			.FirstOrDefaultAsync(j => j.Id == jobId && j.DataSourceId == dataSourceId && j.TenantId == tenantId, cancellationToken);
		if (job is null)
			return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = "扫描任务不存在。" });

		// C3：可中断状态（Queued/Running/Cancelling）才允许取消。
		if (job.Status is MetadataScanJobStatus.Succeeded or MetadataScanJobStatus.Failed
			or MetadataScanJobStatus.Cancelled or MetadataScanJobStatus.PartiallySucceeded or MetadataScanJobStatus.Retrying)
		{
			return StatusCode(409, new ApiError
			{
				Code = ErrorCodes.Conflict,
				Message = $"任务状态为 {job.Status}，不可取消。"
			});
		}

		// Queued 尚未开始：直接置 Cancelled（无 staging 可清理）。
		if (job.Status == MetadataScanJobStatus.Queued)
		{
			job.Status = MetadataScanJobStatus.Cancelled;
			job.CancelledAt = DateTime.UtcNow;
			job.FinishedAt = DateTime.UtcNow;
			job.Stage = "Cancelled";
			_db.MetadataScanJobs.Update(job);
			await _db.SaveChangesAsync(cancellationToken);
			await AuditScanAsync(tenantId, userId, userValue, dataSourceId, "success", $"已取消排队中的扫描任务 jobId={jobId}。");
			return StatusCode(202, new { jobId, status = MetadataScanJobStatus.Cancelled.ToString() });
		}

		// Running/Cancelling：标记 Cancelling 并中断运行中的 CTS。
		job.Status = MetadataScanJobStatus.Cancelling;
		_db.MetadataScanJobs.Update(job);
		await _db.SaveChangesAsync(cancellationToken);
		_scanHost?.RequestCancel(jobId);
		await AuditScanAsync(tenantId, userId, userValue, dataSourceId, "success", $"已请求取消扫描任务 jobId={jobId}。");
		return StatusCode(202, new { jobId, status = MetadataScanJobStatus.Cancelling.ToString() });
	}

	[HttpPost("/api/data-sources/{dataSourceId:long}/metadata/scan/{jobId:long}/retry-failed")]
	public async Task<IActionResult> RetryFailedScan(long dataSourceId, long jobId, CancellationToken cancellationToken)
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

		// 同源活动去重：已有进行中任务则 409（与 ScanDataSource 一致）。
		var activeJob = await _db.MetadataScanJobs.AsNoTracking()
			.Where(j => j.DataSourceId == dataSourceId && j.TenantId == tenantId
				&& (j.Status == MetadataScanJobStatus.Queued
					|| j.Status == MetadataScanJobStatus.Running
					|| j.Status == MetadataScanJobStatus.Cancelling
					|| j.Status == MetadataScanJobStatus.Retrying))
			.OrderByDescending(j => j.Id)
			.FirstOrDefaultAsync(cancellationToken);
		if (activeJob is not null)
			return StatusCode(409, new ApiError
			{
				Code = ErrorCodes.Conflict,
				Message = $"该数据源已有进行中的扫描任务（jobId={activeJob.Id}），请等待完成后重试。"
			});

		var source = await _db.MetadataScanJobs.AsNoTracking()
			.FirstOrDefaultAsync(j => j.Id == jobId && j.DataSourceId == dataSourceId && j.TenantId == tenantId, cancellationToken);
		if (source is null)
			return NotFound(new ApiError { Code = ErrorCodes.NotFound, Message = "原扫描任务不存在。" });

		// §L.6：从 active 续扫失败项——新建任务（OriginalJobId 指向失败任务），
		// ProcessJobAsync 重分配 BatchVersion 并从 active 重播（成功表保留，仅重扫失败项）。
		var newJob = new MetadataScanJob
		{
			TenantId = tenantId,
			DataSourceId = dataSourceId,
			TriggeredBy = userValue,
			OriginalJobId = jobId,
			Status = MetadataScanJobStatus.Queued,
			ProgressPercent = 0
		};

		try
		{
			_db.MetadataScanJobs.Add(newJob);
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch (Microsoft.EntityFrameworkCore.DbUpdateException)
		{
			return StatusCode(409, new ApiError { Code = ErrorCodes.Conflict, Message = "该数据源已有进行中的扫描任务，请等待完成后重试。" });
		}

		await _queue.EnqueueAsync(newJob.Id, cancellationToken);
		_backlog?.Increment();

		await AuditScanAsync(tenantId, userId, userValue, dataSourceId, "success", $"已发起失败项续扫任务 newJobId={newJob.Id}（原 jobId={jobId}）。");
		return StatusCode(202, new { jobId = newJob.Id, originalJobId = jobId, dataSourceId, status = newJob.Status.ToString() });
	}

	[HttpGet("/api/data-sources/{dataSourceId:long}/metadata/scan/latest")]
	public async Task<IActionResult> GetLatestScanJob(long dataSourceId, CancellationToken cancellationToken)
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

		// C5：返回最新任务（不限终态），供前端进入页面后恢复续显（含重启后任务仍在）。
		var job = await _db.MetadataScanJobs.AsNoTracking()
			.Where(j => j.DataSourceId == dataSourceId && j.TenantId == tenantId)
			.OrderByDescending(j => j.Id)
			.FirstOrDefaultAsync(cancellationToken);
		if (job is null)
			return Ok(new { jobId = (long?)null, dataSourceId, status = (string?)null });

		return Ok(new
		{
			jobId = job.Id,
			dataSourceId = job.DataSourceId,
			status = job.Status.ToString(),
			progressPercent = job.ProgressPercent,
			stage = job.Stage,
			progressDetails = ScanTelemetry.FromJson(job.ProgressDetailsJson),
			tablesScanned = job.TablesScanned,
			columnsScanned = job.ColumnsScanned,
			orphansDetected = job.OrphansDetected,
			errorCode = job.ErrorCode,
			errorMessage = job.ErrorMessage,
			startedAt = job.StartedAt,
			finishedAt = job.FinishedAt
		});
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
			stage = job.Stage,
			progressDetails = ScanTelemetry.FromJson(job.ProgressDetailsJson),
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

	private async Task AuditScanAsync(long tenantId, long userId, string? actor, long dataSourceId, string result, string message)
	{
		try
		{
			if (_audit is null) return;
			await _audit.LogAsync(new AuditLogEntry(
				TenantId: tenantId,
				Action: "metadata:scan",
				EntityType: "DataSource",
				UserId: userId,
				Actor: actor ?? "http",
				EntityId: dataSourceId.ToString(),
				Result: result,
				Message: message), default);
		}
		catch
		{
			// 审计写入失败不应影响主流程。
		}
	}
}
