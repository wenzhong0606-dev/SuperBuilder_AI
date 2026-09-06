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
using SuperBuilder_AI.Application.Metadata;
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
}
