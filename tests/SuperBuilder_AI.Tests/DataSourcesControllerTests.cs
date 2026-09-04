using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
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
		var controller = new DataSourcesController(db, new PermissiveIdentity());
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
}
