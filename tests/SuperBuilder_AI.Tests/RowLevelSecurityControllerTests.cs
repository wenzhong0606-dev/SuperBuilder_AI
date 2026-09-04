using System.Collections.Generic;
using System.Net;
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
/// M0-06：RowLevelSecurityPolicy 创建/更新须校验 Tenant → DataSource → Table → Column 完整链。
/// 覆盖跨租户列、跨租户用户主体两类负向场景（已有链校验逻辑，本测试为其回归护栏）。
/// </summary>
public class RowLevelSecurityControllerTests
{
	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private static RowLevelSecurityController Build(SuperBIContext db, long authTenantId)
	{
		var controller = new RowLevelSecurityController(db, new FakeIdentityService());
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext
			{
				User = new ClaimsPrincipal(new ClaimsIdentity(new[]
				{
					new Claim("tid", authTenantId.ToString()),
					new Claim(ClaimTypes.NameIdentifier, "1"),
				})),
			},
		};
		return controller;
	}

	[Fact]
	public async Task Save_CrossTenantColumn_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 租户 7 的数据源/表/列
		ctx.Tenants.Add(new Tenant { Id = 7 });
		ctx.DataSources.Add(new DataSource { Id = 70, TenantId = 7, Name = "ds7" });
		ctx.MetadataTables.Add(new MetadataTable { Id = 71, TenantId = 7, DataSourceId = 70, TableName = "t7" });
		ctx.MetadataColumns.Add(new MetadataColumn { Id = 72, MetadataTableId = 71, ColumnName = "c7" });
		await ctx.SaveChangesAsync();

		// 认证租户 5 试图在该列上建立策略 → 列不属于租户 5 → 403
		var controller = Build(ctx, 5);
		var request = new RowLevelSecurityPolicyRequest
		{
			DataSourceId = 70,
			MetadataTableId = 71,
			MetadataColumnId = 72,
			SubjectType = RowPolicySubjectType.Everyone,
			Effect = RowPolicyEffect.Allow,
			Operator = "=",
			Value = "x",
		};
		var result = await controller.Save(request, CancellationToken.None);
		var obj = Assert.IsType<ObjectResult>(result);
		Assert.Equal((int)HttpStatusCode.Forbidden, obj.StatusCode);
	}

	[Fact]
	public async Task Save_CrossTenantUserSubject_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 租户 5 自己的列
		ctx.Tenants.Add(new Tenant { Id = 5 });
		ctx.Tenants.Add(new Tenant { Id = 7 });
		ctx.DataSources.Add(new DataSource { Id = 50, TenantId = 5, Name = "ds5" });
		ctx.MetadataTables.Add(new MetadataTable { Id = 51, TenantId = 5, DataSourceId = 50, TableName = "t5" });
		ctx.MetadataColumns.Add(new MetadataColumn { Id = 52, MetadataTableId = 51, ColumnName = "c5" });
		// 租户 7 的用户
		ctx.Users.Add(new User { Id = 99, TenantId = 7, Username = "u7", Email = "u7@x" });
		await ctx.SaveChangesAsync();

		// 认证租户 5 试图以租户 7 的用户作为策略主体 → 主体不属于租户 5 → 403
		var controller = Build(ctx, 5);
		var request = new RowLevelSecurityPolicyRequest
		{
			DataSourceId = 50,
			MetadataTableId = 51,
			MetadataColumnId = 52,
			SubjectType = RowPolicySubjectType.User,
			SubjectId = 99,
			Effect = RowPolicyEffect.Allow,
			Operator = "=",
			Value = "x",
		};
		var result = await controller.Save(request, CancellationToken.None);
		var obj = Assert.IsType<ObjectResult>(result);
		Assert.Equal((int)HttpStatusCode.Forbidden, obj.StatusCode);
	}

	private sealed class FakeIdentityService : IIdentityService
	{
		public Task SeedAsync(CancellationToken ct = default) => throw new System.NotImplementedException();
		public Task<IdentityResult> CreateUserAsync(long tenantId, string username, string displayName, string email, string[]? roleCodes, CancellationToken ct = default) => throw new System.NotImplementedException();
		public Task<IdentityResult> AssignRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default) => throw new System.NotImplementedException();
		public Task<IdentityResult> RevokeRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default) => throw new System.NotImplementedException();
		public Task<IdentityResult> SetPasswordAsync(long tenantId, long userId, string password, CancellationToken ct = default) => throw new System.NotImplementedException();
		public Task<IReadOnlyList<string>> GetPermissionsAsync(long tenantId, long userId, CancellationToken ct = default) => throw new System.NotImplementedException();
		public Task<bool> HasPermissionAsync(long tenantId, long userId, string permissionCode, CancellationToken ct = default) => Task.FromResult(true);
	}
}
