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
using SuperBuilder_AI.Infrastructure.Security;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M1-04 验证：DataSource 写入路径的字段完整性——
/// DbType 白名单、名称必填/租户内规范化唯一、规范化字段持久化、空值校验。
/// 不依赖 DB 级 NOT NULL（沿用 M1-02/03 的写入路径强制模式，兼容查询计划测试种子）。
/// </summary>
public class DataSourcesControllerTests
{
	private const long TenantA = 5;
	private const long TenantB = 6;

	private static ClaimsPrincipal AsTenant(long tid, long uid = 1) =>
		new(new ClaimsIdentity(new[]
		{
			new Claim("tid", tid.ToString()),
			new Claim(ClaimTypes.NameIdentifier, uid.ToString()),
		}));

	private sealed class PermissiveIdentity(bool allowed = true) : IIdentityService
	{
		public Task SeedAsync(CancellationToken ct = default) => Task.CompletedTask;
		public Task<IdentityResult> CreateUserAsync(long tenantId, string username, string? displayName, string? email, string[]? roleCodes, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IdentityResult> AssignRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IdentityResult> RevokeRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IdentityResult> SetPasswordAsync(long tenantId, long userId, string password, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IdentityResult> SetUserStatusAsync(long tenantId, long userId, UserStatus newStatus, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
		public Task<IReadOnlyList<string>> GetPermissionsAsync(long tenantId, long userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
		public Task<bool> HasPermissionAsync(long tenantId, long userId, string permissionCode, CancellationToken ct = default) => Task.FromResult(allowed);
	}

	private sealed class NoopScanQueue : IMetadataScanQueue
	{
		public ValueTask EnqueueAsync(long jobId, CancellationToken ct = default) => ValueTask.CompletedTask;
		public ValueTask<long> DequeueAsync(CancellationToken ct = default) => ValueTask.FromResult(0L);
	}

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		// 播种租户行：DataSource→Tenant 外键（M1-04）要求写入路径的 TenantId 必须存在对应租户。
		ctx.Tenants.AddRange(
			new Tenant { Id = TenantA, TenantCode = "t5", TenantName = "T5", Enabled = true, CreatedTime = DateTime.UtcNow },
			new Tenant { Id = TenantB, TenantCode = "t6", TenantName = "T6", Enabled = true, CreatedTime = DateTime.UtcNow });
		ctx.SaveChanges();
		return ctx;
	}

	private static DataSourcesController Build(SuperBIContext db, long tid, long uid = 1)
	{
		var controller = new DataSourcesController(db, new PermissiveIdentity(), new AesGcmSecretStore(new byte[32]), new NoopScanQueue());
		controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = AsTenant(tid, uid) } };
		return controller;
	}

	[Fact]
	public async Task Create_Persists_NormalizedName_And_Defaults()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx, TenantA);
		var result = await ctrl.Create(new CreateDataSourceRequest("  Sales DB ", " mysql ", "Server=localhost;"), CancellationToken.None);

		Assert.IsType<OkObjectResult>(result);
		var saved = await ctx.DataSources.FirstAsync();
		Assert.Equal("Sales DB", saved.Name);
		Assert.Equal("sales db", saved.NormalizedName);
		Assert.Equal("MYSQL", saved.DbType);
		Assert.True(saved.Enabled);
		Assert.Null(saved.LastTestStatus);
	}

	[Fact]
	public async Task Create_Duplicate_NormalizedName_SameTenant_Conflicts()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx, TenantA);
		Assert.IsType<OkObjectResult>(await ctrl.Create(new CreateDataSourceRequest("Primary", "MYSQL", "Server=localhost;"), CancellationToken.None));
		// 大小写/空白不同，但规范化后相同 → 视为重复。
		var dup = await ctrl.Create(new CreateDataSourceRequest("primary ", "MYSQL", "Server=localhost;"), CancellationToken.None);

		Assert.IsType<ConflictObjectResult>(dup);
		Assert.Equal(1, await ctx.DataSources.CountAsync());
	}

	[Fact]
	public async Task Create_SameName_CrossTenant_Succeeds()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrlA = Build(ctx, TenantA);
		var ctrlB = Build(ctx, TenantB);
		Assert.IsType<OkObjectResult>(await ctrlA.Create(new CreateDataSourceRequest("Analytics", "POSTGRESQL", "Host=localhost;"), CancellationToken.None));
		Assert.IsType<OkObjectResult>(await ctrlB.Create(new CreateDataSourceRequest("Analytics", "POSTGRESQL", "Host=localhost;"), CancellationToken.None));

		Assert.Equal(2, await ctx.DataSources.CountAsync());
	}

	[Fact]
	public async Task Create_Unsupported_DbType_IsRejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx, TenantA);
		var result = await ctrl.Create(new CreateDataSourceRequest("X", "ORACLE", "Server=localhost;"), CancellationToken.None);

		Assert.IsType<BadRequestObjectResult>(result);
		Assert.Equal(0, await ctx.DataSources.CountAsync());
	}

	[Fact]
	public async Task Create_EmptyName_Or_EmptyConnectionString_IsRejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx, TenantA);
		Assert.IsType<BadRequestObjectResult>(await ctrl.Create(new CreateDataSourceRequest("", "MYSQL", "Server=localhost;"), CancellationToken.None));
		Assert.IsType<BadRequestObjectResult>(await ctrl.Create(new CreateDataSourceRequest("Y", "", "Server=localhost;"), CancellationToken.None));
		Assert.IsType<BadRequestObjectResult>(await ctrl.Create(new CreateDataSourceRequest("Y", "MYSQL", ""), CancellationToken.None));
	}

	[Fact]
	public void DataSource_NormalizeName_And_SupportedDbTypes()
	{
		Assert.Equal("sales db", DataSource.NormalizeName("  Sales DB "));
		Assert.Equal("sales db", DataSource.NormalizeName("SALES DB"));
		Assert.True(DataSource.IsSupportedDbType("mysql"));
		Assert.True(DataSource.IsSupportedDbType("SQLSERVER"));
		Assert.True(DataSource.IsSupportedDbType("PostgreSQL"));
		Assert.False(DataSource.IsSupportedDbType("oracle"));
		Assert.False(DataSource.IsSupportedDbType(null));
		Assert.False(DataSource.IsSupportedDbType(""));
	}

	[Fact]
	public async Task Update_ChangesName_And_ResetsConnectionString()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx, TenantA);
		Assert.IsType<OkObjectResult>(await ctrl.Create(new CreateDataSourceRequest("Primary", "MYSQL", "Server=localhost;"), CancellationToken.None));
		var id = await ctx.DataSources.Select(x => x.Id).FirstAsync();

		var result = await ctrl.Update(id, new UpdateDataSourceRequest("Primary Renamed", "POSTGRESQL", "Host=localhost;"), CancellationToken.None);
		Assert.IsType<OkObjectResult>(result);
		var saved = await ctx.DataSources.FirstAsync(x => x.Id == id);
		Assert.Equal("Primary Renamed", saved.Name);
		Assert.Equal("primary renamed", saved.NormalizedName);
		Assert.Equal("POSTGRESQL", saved.DbType);
		// SEC-01（M13-03）：连接串以 AEAD 密文（v1: 前缀）落库，明文仅服务端可解——
		// 断言值需先解密，直接比明文会得到密文而失败。
		Assert.StartsWith("v1:", saved.ConnectionString);
		Assert.Equal("Host=localhost;", new AesGcmSecretStore(new byte[32]).Unprotect(saved.ConnectionString));
	}

	[Fact]
	public async Task Update_ReturnsNotFound_ForOtherTenant()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var ctrlA = Build(ctx, TenantA);
		Assert.IsType<OkObjectResult>(await ctrlA.Create(new CreateDataSourceRequest("Primary", "MYSQL", "Server=localhost;"), CancellationToken.None));
		var id = await ctx.DataSources.Select(x => x.Id).FirstAsync();
		var ctrlB = Build(ctx, TenantB);
		Assert.IsType<NotFoundObjectResult>(await ctrlB.Update(id, new UpdateDataSourceRequest("x", null, null), CancellationToken.None));
	}

	[Fact]
	public async Task Update_RejectsEmptyName()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var ctrl = Build(ctx, TenantA);
		Assert.IsType<OkObjectResult>(await ctrl.Create(new CreateDataSourceRequest("Primary", "MYSQL", "Server=localhost;"), CancellationToken.None));
		var id = await ctx.DataSources.Select(x => x.Id).FirstAsync();
		Assert.IsType<BadRequestObjectResult>(await ctrl.Update(id, new UpdateDataSourceRequest("", null, null), CancellationToken.None));
	}

	[Fact]
	public async Task EnableDisable_TogglesEnabled()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var ctrl = Build(ctx, TenantA);
		Assert.IsType<OkObjectResult>(await ctrl.Create(new CreateDataSourceRequest("Primary", "MYSQL", "Server=localhost;"), CancellationToken.None));
		var id = await ctx.DataSources.Select(x => x.Id).FirstAsync();
		Assert.True((await ctx.DataSources.FindAsync(id))!.Enabled);
		Assert.IsType<OkObjectResult>(await ctrl.Disable(id, CancellationToken.None));
		Assert.False((await ctx.DataSources.FindAsync(id))!.Enabled);
		Assert.IsType<OkObjectResult>(await ctrl.Enable(id, CancellationToken.None));
		Assert.True((await ctx.DataSources.FindAsync(id))!.Enabled);
	}

	[Fact]
	public async Task TestConnection_ReturnsNotFound_ForMissingSource()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var ctrl = Build(ctx, TenantA);
		Assert.IsType<NotFoundObjectResult>(await ctrl.TestConnection(9999, CancellationToken.None));
	}

	[Fact]
	public async Task TestConnection_ReturnsBadRequest_ForEmptyConnectionString()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var ctrl = Build(ctx, TenantA);
		ctx.DataSources.Add(new DataSource { TenantId = TenantA, Name = "X", NormalizedName = "x", DbType = "MYSQL", ConnectionString = "", Enabled = true });
		await ctx.SaveChangesAsync();
		var id = await ctx.DataSources.Select(x => x.Id).FirstAsync();
		Assert.IsType<BadRequestObjectResult>(await ctrl.TestConnection(id, CancellationToken.None));
	}

	[Fact]
	public async Task TestConnection_ReturnsFailed_ForRefusedConnection()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var ctrl = Build(ctx, TenantA);
		Assert.IsType<OkObjectResult>(await ctrl.Create(new CreateDataSourceRequest("Primary", "MYSQL", "Server=127.0.0.1;Port=1;User=root;"), CancellationToken.None));
		var id = await ctx.DataSources.Select(x => x.Id).FirstAsync();
		var result = await ctrl.TestConnection(id, CancellationToken.None);
		Assert.IsType<OkObjectResult>(result);
		Assert.Equal("Failed", ReadStatus(result));
		// 连接失败也会被记录到数据源测试状态字段。
		var saved = await ctx.DataSources.FirstAsync(x => x.Id == id);
		Assert.Equal("Failed", saved.LastTestStatus);
	}

	[Theory]
	[InlineData("MYSQL", "")]
	[InlineData("ORACLE", "Server=localhost;")]
	[InlineData("", "Server=localhost;")]
	public async Task Preview_RejectsInvalidInput_WithoutPersisting(string type, string value)
	{
		await using var ctx = CreateContext(out var connection);
		await using var lease = connection;
		Assert.IsType<BadRequestObjectResult>(await Build(ctx, TenantA).TestConnectionString(new(type, value), CancellationToken.None));
		Assert.Empty(await ctx.DataSources.ToListAsync());
		Assert.Empty(await ctx.DataSourceAccessGrants.ToListAsync());
	}

	[Fact]
	public async Task Preview_RejectsOversizedConnectionString()
	{
		await using var ctx = CreateContext(out var connection);
		await using var lease = connection;
		Assert.IsType<BadRequestObjectResult>(await Build(ctx, TenantA).TestConnectionString(new("MYSQL", new string('x', 2049)), CancellationToken.None));
	}

	[Theory]
	[InlineData("MYSQL")]
	[InlineData("SQLSERVER")]
	[InlineData("POSTGRESQL")]
	public async Task Preview_FailureIsSanitized_AndDoesNotPersist(string type)
	{
		await using var ctx = CreateContext(out var connection);
		await using var lease = connection;
		var result = await Build(ctx, TenantA).TestConnectionString(new(type, "UnsupportedSecretKey=never-echo-this-secret;"), CancellationToken.None);
		Assert.Equal("Failed", ReadStatus(result));
		var body = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value);
		Assert.DoesNotContain("never-echo", body);
		Assert.DoesNotContain("UnsupportedSecretKey", body);
		Assert.Empty(await ctx.DataSources.ToListAsync());
		Assert.Empty(await ctx.DataSourceAccessGrants.ToListAsync());
	}

	[Fact]
	public async Task Preview_RequiresIdentityAndMetadataEdit()
	{
		await using var ctx = CreateContext(out var connection);
		await using var lease = connection;
		Assert.IsType<UnauthorizedObjectResult>(await Build(ctx, 0).TestConnectionString(new("MYSQL", "x"), CancellationToken.None));
		var ctrl = new DataSourcesController(ctx, new PermissiveIdentity(false), new AesGcmSecretStore(new byte[32]), new NoopScanQueue());
		ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = AsTenant(TenantA) } };
		var denied = Assert.IsType<ObjectResult>(await ctrl.TestConnectionString(new("MYSQL", "x"), CancellationToken.None));
		Assert.Equal(403, denied.StatusCode);
	}

	private static string? ReadStatus(IActionResult result)
	{
		if (result is not OkObjectResult ok || ok.Value is null) return null;
		var prop = ok.Value.GetType().GetProperty("status");
		return prop?.GetValue(ok.Value)?.ToString();
	}

	private static long SeedDataSourceRow(SuperBIContext ctx, long tid, bool vectorsBackfilled = false)
	{
		var ds = new DataSource
		{
			TenantId = tid,
			Name = "ToDelete",
			NormalizedName = "todelete",
			DbType = "MYSQL",
			ConnectionString = "Server=127.0.0.1;",
			Enabled = true,
			VectorsBackfilled = vectorsBackfilled
		};
		ctx.DataSources.Add(ds);
		ctx.SaveChanges();
		return ds.Id;
	}

	// —— C1 保存并扫描（§L.6）——
	[Fact]
	public async Task Create_WithScanAfterCreate_EnqueuesScanJob()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx, TenantA);
		var result = await ctrl.Create(new CreateDataSourceRequest("Sales", "MYSQL", "Server=127.0.0.1;") { ScanAfterCreate = true }, CancellationToken.None);

		var created = Assert.IsType<CreatedAtActionResult>(result);
		Assert.Equal(201, created.StatusCode);
		var job = await ctx.MetadataScanJobs.SingleAsync();
		var dsId = await ctx.DataSources.Select(x => x.Id).FirstAsync();
		Assert.Equal(dsId, job.DataSourceId);
		Assert.Equal(MetadataScanJobStatus.Queued, job.Status);
	}

	[Fact]
	public async Task Create_WithoutScanAfterCreate_NoScanJob()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx, TenantA);
		Assert.IsType<OkObjectResult>(await ctrl.Create(new CreateDataSourceRequest("Sales", "MYSQL", "Server=127.0.0.1;"), CancellationToken.None));
		Assert.Equal(0, await ctx.MetadataScanJobs.CountAsync());
	}

	// —— C8 删除两档（§L.7 / §10.7）——
	[Fact]
	public async Task Delete_Disable_RequiresMetadataDeletePermission()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var id = SeedDataSourceRow(ctx, TenantA);

		var denied = new DataSourcesController(ctx, new PermissiveIdentity(false), new AesGcmSecretStore(new byte[32]), new NoopScanQueue());
		denied.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = AsTenant(TenantA) } };
		Assert.Equal(403, Assert.IsType<ObjectResult>(await denied.Delete(id, "disable", false, CancellationToken.None)).StatusCode);

		var ok = Build(ctx, TenantA);
		Assert.IsType<OkObjectResult>(await ok.Delete(id, "disable", false, CancellationToken.None));
		Assert.False((await ctx.DataSources.FindAsync(id))!.Enabled);
	}

	[Fact]
	public async Task Delete_Cleanup_RequiresBackfill()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var id = SeedDataSourceRow(ctx, TenantA, vectorsBackfilled: false);
		var ctrl = Build(ctx, TenantA);

		var result = await ctrl.Delete(id, "cleanup", true, CancellationToken.None);
		var obj = Assert.IsType<ObjectResult>(result);
		Assert.Equal(409, obj.StatusCode);
		Assert.Equal("vector_backfill_required_before_cleanup", obj.Value!.GetType().GetProperty("Code")!.GetValue(obj.Value)!.ToString());
	}

	[Fact]
	public async Task Delete_Cleanup_RemovesSource_And_WritesGcRequest()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var id = SeedDataSourceRow(ctx, TenantA, vectorsBackfilled: true);
		var ctrl = Build(ctx, TenantA);

		var result = await ctrl.Delete(id, "cleanup", true, CancellationToken.None);
		Assert.IsType<OkObjectResult>(result);
		Assert.Null(await ctx.DataSources.FindAsync(id));
		var gc = await ctx.MetadataVectorGcRequests.SingleAsync();
		Assert.Equal("DataSourceDeleted", gc.Reason);
		Assert.Equal(id, gc.DataSourceId);
	}
}
