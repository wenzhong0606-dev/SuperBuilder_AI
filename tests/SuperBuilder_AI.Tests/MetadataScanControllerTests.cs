using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Services;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M4-05：元数据扫描后台任务接入验证。
/// POST 创建任务并入队（202 + jobId）、GET 轮询返回任务状态、授权/权限门禁、旧固定入口停用（410）。
/// </summary>
public class MetadataScanControllerTests
{
	private const long TenantA = 5;
	private const long TenantB = 6;
	private const long UserA = 1;

	private static ClaimsPrincipal AsTenant(long tid, long uid = UserA) =>
		new(new ClaimsIdentity(new[]
		{
			new Claim("tid", tid.ToString()),
			new Claim(ClaimTypes.NameIdentifier, uid.ToString()),
		}));

	private sealed class PermissiveIdentity : IIdentityService
	{
		public Task SeedAsync(CancellationToken ct = default) => Task.CompletedTask;
		public Task<IdentityResult> CreateUserAsync(long tenantId, string username, string? displayName, string? email, string[]? roleCodes, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IdentityResult> AssignRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IdentityResult> RevokeRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IdentityResult> SetPasswordAsync(long tenantId, long userId, string password, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IdentityResult> SetUserStatusAsync(long tenantId, long userId, UserStatus newStatus, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IReadOnlyList<string>> GetPermissionsAsync(long tenantId, long userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
		public Task<bool> HasPermissionAsync(long tenantId, long userId, string permissionCode, CancellationToken ct = default) => Task.FromResult(true);
	}

	private sealed class DenyingIdentity : IIdentityService
	{
		public Task SeedAsync(CancellationToken ct = default) => Task.CompletedTask;
		public Task<IdentityResult> CreateUserAsync(long tenantId, string username, string? displayName, string? email, string[]? roleCodes, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IdentityResult> AssignRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IdentityResult> RevokeRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IdentityResult> SetPasswordAsync(long tenantId, long userId, string password, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IdentityResult> SetUserStatusAsync(long tenantId, long userId, UserStatus newStatus, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IReadOnlyList<string>> GetPermissionsAsync(long tenantId, long userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
		public Task<bool> HasPermissionAsync(long tenantId, long userId, string permissionCode, CancellationToken ct = default) => Task.FromResult(false);
	}

	private sealed class PermissiveAuth : IDataSourceAuthorizationService
	{
		public Task<IReadOnlyList<long>> GetAuthorizedDataSourceIdsAsync(long tenantId, long userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>(Array.Empty<long>());
		public Task<bool> IsAuthorizedAsync(long tenantId, long userId, long dataSourceId, CancellationToken ct = default) => Task.FromResult(true);
		public Task GrantAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task RevokeAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task RevokeBySubjectAsync(long tenantId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task<IReadOnlyList<DataSourceAccessGrant>> DetectOrphanGrantsAsync(long tenantId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DataSourceAccessGrant>>(Array.Empty<DataSourceAccessGrant>());
	}

	private sealed class DenyingAuth : IDataSourceAuthorizationService
	{
		public Task<IReadOnlyList<long>> GetAuthorizedDataSourceIdsAsync(long tenantId, long userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>(Array.Empty<long>());
		public Task<bool> IsAuthorizedAsync(long tenantId, long userId, long dataSourceId, CancellationToken ct = default) => Task.FromResult(false);
		public Task GrantAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task RevokeAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task RevokeBySubjectAsync(long tenantId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task<IReadOnlyList<DataSourceAccessGrant>> DetectOrphanGrantsAsync(long tenantId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DataSourceAccessGrant>>(Array.Empty<DataSourceAccessGrant>());
	}

	private sealed class RecordingQueue : IMetadataScanQueue
	{
		public long? Enqueued;
		public ValueTask EnqueueAsync(long jobId, CancellationToken ct = default) { Enqueued = jobId; return ValueTask.CompletedTask; }
		public ValueTask<long> DequeueAsync(CancellationToken ct = default) => throw new NotImplementedException();
	}

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		ctx.Tenants.AddRange(
			new Tenant { Id = TenantA, TenantCode = "t5", TenantName = "T5", Enabled = true, CreatedTime = DateTime.UtcNow },
			new Tenant { Id = TenantB, TenantCode = "t6", TenantName = "T6", Enabled = true, CreatedTime = DateTime.UtcNow });
		ctx.SaveChanges();
		return ctx;
	}

	private static (MetadataController ctrl, RecordingQueue queue) Build(SuperBIContext db, IIdentityService identity, IDataSourceAuthorizationService auth, long tid, long uid = UserA)
	{
		var queue = new RecordingQueue();
		var controller = new MetadataController(null!, null!, queue, db, auth, identity);
		controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = AsTenant(tid, uid) } };
		return (controller, queue);
	}

	private static long SeedDataSource(SuperBIContext ctx, long tenantId)
	{
		var ds = new DataSource
		{
			TenantId = tenantId,
			Name = "WMS",
			NormalizedName = "wms",
			DbType = "MYSQL",
			ConnectionString = "Server=127.0.0.1;User=root;",
			Enabled = true
		};
		ctx.DataSources.Add(ds);
		ctx.SaveChanges();
		return ds.Id;
	}

	[Fact]
	public async Task Scan_CreatesQueuedJob_AndEnqueues()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var (ctrl, queue) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);

		var result = await ctrl.ScanDataSource(dsId, CancellationToken.None);

		var code = Assert.IsType<ObjectResult>(result);
		Assert.Equal(202, code.StatusCode);
		var job = await ctx.MetadataScanJobs.SingleAsync();
		Assert.Equal(MetadataScanJobStatus.Queued, job.Status);
		Assert.Equal(TenantA, job.TenantId);
		Assert.Equal(dsId, job.DataSourceId);
		// 入队调用的 jobId 与持久化任务一致。
		Assert.Equal(job.Id, queue.Enqueued);
	}

	[Fact]
	public async Task GetScanJob_ReturnsCreatedJob_Status()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);
		var post = Assert.IsType<ObjectResult>(await ctrl.ScanDataSource(dsId, CancellationToken.None));
		Assert.Equal(202, post.StatusCode);

		var jobId = (await ctx.MetadataScanJobs.SingleAsync()).Id;
		var get = await ctrl.GetScanJob(dsId, jobId, CancellationToken.None);

		var ok = Assert.IsType<OkObjectResult>(get);
		Assert.NotNull(ok.Value);
		var statusProp = ok.Value!.GetType().GetProperty("status");
		Assert.Equal("Queued", statusProp!.GetValue(ok.Value)!.ToString());
	}

	[Fact]
	public async Task Scan_Forbidden_WhenDataSourceNotAuthorized()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new DenyingAuth(), TenantA);

		var result = await ctrl.ScanDataSource(dsId, CancellationToken.None);

		Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
		Assert.Equal(0, await ctx.MetadataScanJobs.CountAsync());
	}

	[Fact]
	public async Task Scan_Forbidden_WhenMissingMetadataScanPermission()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var (ctrl, _) = Build(ctx, new DenyingIdentity(), new PermissiveAuth(), TenantA);

		var result = await ctrl.ScanDataSource(dsId, CancellationToken.None);

		Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
	}

	[Fact]
	public async Task Scan_OldFixedEndpoint_Returns410()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);
		var result = ctrl.Scan();
		Assert.Equal(410, Assert.IsType<ObjectResult>(result).StatusCode);
	}

	[Fact]
	public async Task GetScanJob_ReturnsTableFailures()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var dsId = SeedDataSource(ctx, TenantA);
		var jobId = SeedJob(ctx, dsId, MetadataScanJobStatus.Failed);
		ctx.MetadataScanJobFailures.Add(new MetadataScanJobFailure
		{
			JobId = jobId, DataSourceId = dsId, Database = "db", Schema = "dbo",
			TableName = "orders", Stage = "VectorIndex", ErrorType = "VectorIndexIncomplete",
			ErrorMessage = "必需向量未同步", RetryCount = 3
		});
		await ctx.SaveChangesAsync();
		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);
		var result = Assert.IsType<OkObjectResult>(await ctrl.GetScanJob(dsId, jobId, CancellationToken.None));
		var failures = result.Value!.GetType().GetProperty("failures")!.GetValue(result.Value);
		var json = System.Text.Json.JsonSerializer.Serialize(failures);
		Assert.Contains("orders", json);
		Assert.Contains("VectorIndex", json);
	}

	[Fact]
	public async Task GetScanJob_Returns_RichProgressSnapshot()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var telemetry = new ScanTelemetry();
		telemetry.SetStage("IndexingVectors", "正在写入向量索引。", "VectorIndexStarted");
		telemetry.Details.TablesDiscovered = 8;
		telemetry.Details.TablesProcessed = 8;
		telemetry.Details.ColumnsDiscovered = 80;
		telemetry.Details.ColumnsProcessed = 80;
		telemetry.Details.VectorsTotal = 120;
		telemetry.Details.VectorsProcessed = 60;

		var job = new MetadataScanJob
		{
			TenantId = TenantA,
			DataSourceId = dsId,
			Status = MetadataScanJobStatus.Running,
			Stage = "IndexingVectors",
			ProgressPercent = 86,
			ProgressDetailsJson = telemetry.ToJson()
		};
		ctx.MetadataScanJobs.Add(job);
		await ctx.SaveChangesAsync();

		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);
		var result = Assert.IsType<OkObjectResult>(
			await ctrl.GetScanJob(dsId, job.Id, CancellationToken.None));

		Assert.NotNull(result.Value);
		var type = result.Value!.GetType();
		Assert.Equal("IndexingVectors", type.GetProperty("stage")!.GetValue(result.Value));

		var details = Assert.IsType<ScanProgressDetails>(
			type.GetProperty("progressDetails")!.GetValue(result.Value));
		Assert.Equal(8, details.TablesDiscovered);
		Assert.Equal(120, details.VectorsTotal);
		Assert.Equal(60, details.VectorsProcessed);
		Assert.Contains(details.Events, e => e.EventCode == "VectorIndexStarted");
	}

	private static long SeedJob(SuperBIContext ctx, long dsId, MetadataScanJobStatus status, long? originalJobId = null)
	{
		var job = new MetadataScanJob
		{
			TenantId = TenantA,
			DataSourceId = dsId,
			Status = status,
			Stage = status.ToString(),
			ProgressPercent = 0,
			OriginalJobId = originalJobId
		};
		ctx.MetadataScanJobs.Add(job);
		ctx.SaveChanges();
		// 模拟生产独立 scope：detach 避免与控制器 AsNoTracking 查询 + Update 的跟踪冲突。
		ctx.Entry(job).State = EntityState.Detached;
		return job.Id;
	}

	// —— C3 软取消（§L.3）——
	[Fact]
	public async Task CancelScan_QueuedJob_BecomesCancelled()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var jobId = SeedJob(ctx, dsId, MetadataScanJobStatus.Queued);
		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);

		var result = await ctrl.CancelScan(dsId, jobId, CancellationToken.None);

		Assert.Equal(202, Assert.IsType<ObjectResult>(result).StatusCode);
		var saved = await ctx.MetadataScanJobs.FindAsync(jobId);
		Assert.Equal(MetadataScanJobStatus.Cancelled, saved!.Status);
	}

	[Fact]
	public async Task CancelScan_RunningJob_BecomesCancelling()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var jobId = SeedJob(ctx, dsId, MetadataScanJobStatus.Running);
		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);

		var result = await ctrl.CancelScan(dsId, jobId, CancellationToken.None);

		Assert.Equal(202, Assert.IsType<ObjectResult>(result).StatusCode);
		var saved = await ctx.MetadataScanJobs.FindAsync(jobId);
		Assert.Equal(MetadataScanJobStatus.Cancelling, saved!.Status);
	}

	[Fact]
	public async Task CancelScan_TerminalJob_Conflict()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var jobId = SeedJob(ctx, dsId, MetadataScanJobStatus.Succeeded);
		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);

		var result = await ctrl.CancelScan(dsId, jobId, CancellationToken.None);

		Assert.Equal(409, Assert.IsType<ObjectResult>(result).StatusCode);
		Assert.Equal(MetadataScanJobStatus.Succeeded, (await ctx.MetadataScanJobs.FindAsync(jobId))!.Status);
	}

	[Fact]
	public async Task CancelScan_Forbidden_WhenMissingCancelPermission()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var jobId = SeedJob(ctx, dsId, MetadataScanJobStatus.Running);
		var (ctrl, _) = Build(ctx, new DenyingIdentity(), new PermissiveAuth(), TenantA);

		var result = await ctrl.CancelScan(dsId, jobId, CancellationToken.None);

		Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
	}

	[Fact]
	public async Task CancelScan_NotFound_ForUnknownJob()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);

		var result = await ctrl.CancelScan(dsId, 9999, CancellationToken.None);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	// —— C7 失败项续扫（§L.6）——
	[Fact]
	public async Task RetryFailedScan_CreatesOriginalJobLinkedJob()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var failedJobId = SeedJob(ctx, dsId, MetadataScanJobStatus.Failed);
		ctx.MetadataScanJobFailures.Add(new MetadataScanJobFailure
		{
			JobId = failedJobId, DataSourceId = dsId, Database = "db", Schema = "dbo",
			TableName = "orders", Stage = "DiscoveringColumns", ErrorType = "TimeoutException"
		});
		await ctx.SaveChangesAsync();
		var (ctrl, queue) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);

		var result = await ctrl.RetryFailedScan(dsId, failedJobId, CancellationToken.None);

		Assert.Equal(202, Assert.IsType<ObjectResult>(result).StatusCode);
		var jobs = await ctx.MetadataScanJobs.ToListAsync();
		Assert.Equal(2, jobs.Count);
		var newJob = jobs.Single(j => j.Id != failedJobId);
		Assert.Equal(failedJobId, newJob.OriginalJobId);
		Assert.Equal(MetadataScanJobStatus.Queued, newJob.Status);
		Assert.Equal(newJob.Id, queue.Enqueued);
	}

	[Fact]
	public async Task RetryFailedScan_RejectsJobWithoutTableFailures()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var dsId = SeedDataSource(ctx, TenantA);
		var failedJobId = SeedJob(ctx, dsId, MetadataScanJobStatus.Failed);
		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);

		var result = await ctrl.RetryFailedScan(dsId, failedJobId, CancellationToken.None);

		Assert.Equal(409, Assert.IsType<ObjectResult>(result).StatusCode);
		Assert.Single(await ctx.MetadataScanJobs.ToListAsync());
	}

	[Fact]
	public async Task RetryFailedScan_DuplicateActive_Conflict()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		SeedJob(ctx, dsId, MetadataScanJobStatus.Running); // 进行中，触发同源去重 409
		var failedJobId = SeedJob(ctx, dsId, MetadataScanJobStatus.Failed);
		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);

		var result = await ctrl.RetryFailedScan(dsId, failedJobId, CancellationToken.None);

		Assert.Equal(409, Assert.IsType<ObjectResult>(result).StatusCode);
		Assert.Equal(2, await ctx.MetadataScanJobs.CountAsync());
	}

	[Fact]
	public async Task RetryFailedScan_NotFound_ForUnknownOriginal()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);

		var result = await ctrl.RetryFailedScan(dsId, 9999, CancellationToken.None);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	// —— C5 进入页面恢复续显（§L.5b）——
	[Fact]
	public async Task GetLatestScanJob_ReturnsNull_WhenNone()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);

		var result = Assert.IsType<OkObjectResult>(await ctrl.GetLatestScanJob(dsId, CancellationToken.None));
		Assert.Null(result.Value!.GetType().GetProperty("jobId")!.GetValue(result.Value));
	}

	[Fact]
	public async Task GetLatestScanJob_ReturnsLatest()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var dsId = SeedDataSource(ctx, TenantA);
		var first = SeedJob(ctx, dsId, MetadataScanJobStatus.Succeeded);
		var second = SeedJob(ctx, dsId, MetadataScanJobStatus.Running);
		var (ctrl, _) = Build(ctx, new PermissiveIdentity(), new PermissiveAuth(), TenantA);

		var result = Assert.IsType<OkObjectResult>(await ctrl.GetLatestScanJob(dsId, CancellationToken.None));
		var value = result.Value!;
		Assert.Equal(second, (long)value.GetType().GetProperty("jobId")!.GetValue(value)!);
		Assert.Equal("Running", value.GetType().GetProperty("status")!.GetValue(value)!.ToString());
	}
}
