using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P10.2 IdentityController 单测（SQLite 内存库 + 真实 IdentityService，不依赖 Moq）。
/// 覆盖：租户作用域用户 CRUD、角色指派/撤销、权限解析、跨租户隔离、
/// 全局角色/权限目录可见不可改、租户角色创建与权限集替换。
/// </summary>
public class IdentityControllerTests
{
	private const long Tenant5 = 5;
	private const long Tenant6 = 6;

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private static IdentityController Build(SuperBIContext db)
	{
		var svc = new IdentityService(db);
		// 幂等种子全局目录（与启动期一致），使角色/权限解析可工作。
		svc.SeedAsync(CancellationToken.None).GetAwaiter().GetResult();
		return new IdentityController(db, svc);
	}

	private static async Task<long> CreateUserAsync(SuperBIContext db, long tenantId, string username, string[]? roles = null)
	{
		var ctrl = Build(db);
		var result = await ctrl.CreateUser(
			new IdentityController.CreateUserRequest(tenantId, username, DisplayName: username, Email: $"{username}@x.com", roles),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
		Assert.NotNull(result);
		var summary = Assert.IsType<IdentityController.UserSummary>(result!.Value);
		return summary.Id;
	}

	[Fact]
	public async Task CreateUser_Returns_201_With_Summary()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx);
		var result = await ctrl.CreateUser(
			new IdentityController.CreateUserRequest(Tenant5, "alice", "Alice", "alice@x.com", new[] { "viewer" }),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;

		Assert.NotNull(result);
		Assert.Equal((int)HttpStatusCode.Created, result!.StatusCode);
		var summary = Assert.IsType<IdentityController.UserSummary>(result.Value);
		Assert.Equal(Tenant5, summary.TenantId);
		Assert.Equal("alice", summary.Username);
		Assert.Equal("Active", summary.Status);
	}

	[Fact]
	public async Task CreateUser_DuplicateUsername_Returns_400()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		await CreateUserAsync(ctx, Tenant5, "bob");
		var ctrl = Build(ctx);
		var result = await ctrl.CreateUser(
			new IdentityController.CreateUserRequest(Tenant5, "bob", "Bob"),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;

		Assert.NotNull(result);
		Assert.Equal((int)HttpStatusCode.BadRequest, result!.StatusCode);
	}

	[Fact]
	public async Task CreateUser_GlobalTenant_Rejected_400()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx);
		var result = await ctrl.CreateUser(
			new IdentityController.CreateUserRequest(0, "sys", "Sys"),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;

		Assert.NotNull(result);
		Assert.Equal((int)HttpStatusCode.BadRequest, result!.StatusCode);
	}

	[Fact]
	public async Task GetUser_ByTenant_Returns_200()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateUserAsync(ctx, Tenant5, "carol");
		var ctrl = Build(ctx);
		var result = await ctrl.GetUser(id, Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;

		Assert.NotNull(result);
		var summary = Assert.IsType<IdentityController.UserSummary>(result!.Value);
		Assert.Equal("carol", summary.Username);
	}

	[Fact]
	public async Task GetUser_CrossTenant_Returns_404()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateUserAsync(ctx, Tenant5, "dave");
		var ctrl = Build(ctx);
		var result = await ctrl.GetUser(id, Tenant6, CancellationToken.None) as Microsoft.AspNetCore.Mvc.NotFoundResult;

		Assert.NotNull(result);
		Assert.Equal((int)HttpStatusCode.NotFound, result!.StatusCode);
	}

	[Fact]
	public async Task ListUsers_TenantScoped_OnlyOwn()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		await CreateUserAsync(ctx, Tenant5, "u5a");
		await CreateUserAsync(ctx, Tenant6, "u6a");
		var ctrl = Build(ctx);
		var result = await ctrl.ListUsers(Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;

		Assert.NotNull(result);
		var users = Assert.IsAssignableFrom<IEnumerable<IdentityController.UserSummary>>(result!.Value).ToList();
		Assert.Single(users);
		Assert.Equal("u5a", users[0].Username);
	}

	[Fact]
	public async Task AssignRole_Then_Permissions_Contain_RolePerms()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateUserAsync(ctx, Tenant5, "erin");
		var ctrl = Build(ctx);

		var assign = await ctrl.AssignRole(id, new IdentityController.AssignRoleRequest("member"), Tenant5, CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(assign);

		var perms = await ctrl.GetPermissions(id, Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(perms);
		var body = Assert.IsType<IdentityController.UserPermissionsResponse>(perms!.Value);
		var codes = body.Permissions.ToList();
		Assert.Contains("dashboard:view", codes);
		Assert.Contains("app:create", codes);
		// member 不应拥有管理类权限
		Assert.DoesNotContain("identity:manage", codes);
	}

	[Fact]
	public async Task RevokeRole_Removes_Permissions()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateUserAsync(ctx, Tenant5, "frank", new[] { "viewer" });
		var ctrl = Build(ctx);

		var revoke = await ctrl.RevokeRole(id, "viewer", Tenant5, CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.NoContentResult;
		Assert.NotNull(revoke);

		var perms = await ctrl.GetPermissions(id, Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(perms);
		var body = Assert.IsType<IdentityController.UserPermissionsResponse>(perms!.Value);
		var codes = body.Permissions.ToList();
		Assert.Empty(codes);
	}

	[Fact]
	public async Task AssignRole_CrossTenant_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateUserAsync(ctx, Tenant5, "grace");
		var ctrl = Build(ctx);
		// 用户在 Tenant5，但用 Tenant6 指派 → 用户在该租户不可见 → 失败
		var result = await ctrl.AssignRole(id, new IdentityController.AssignRoleRequest("viewer"), Tenant6, CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task RolesCatalog_Includes_Global_And_Tenant()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 创建租户角色
		var ctrl = Build(ctx);
		var create = await ctrl.CreateRole(
			new IdentityController.CreateRoleRequest(Tenant5, "ops", "运维", Permissions: new[] { "metadata:view" }),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
		Assert.NotNull(create);

		var list = await ctrl.ListRoles(Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(list);
		var roles = Assert.IsAssignableFrom<IEnumerable<IdentityController.RoleSummary>>(list!.Value).ToList();
		// 4 全局 + 1 租户
		Assert.Equal(5, roles.Count);
		Assert.Contains(roles, r => r.Code == "platform-admin" && r.TenantId == 0);
		Assert.Contains(roles, r => r.Code == "ops" && r.TenantId == Tenant5);
	}

	[Fact]
	public async Task GetRole_Returns_Permissions()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx);
		var result = await ctrl.GetRole("viewer", Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);
		var detail = Assert.IsType<IdentityController.RoleDetail>(result!.Value);
		Assert.Equal(0, detail.TenantId);
		Assert.Contains("dashboard:view", detail.Permissions);
		Assert.Contains("metadata:view", detail.Permissions);
		Assert.DoesNotContain("identity:manage", detail.Permissions);
	}

	[Fact]
	public async Task CreateRole_GlobalTenant_Rejected_400()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx);
		var result = await ctrl.CreateRole(
			new IdentityController.CreateRoleRequest(0, "evil", "Evil"),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
		Assert.Equal((int)HttpStatusCode.BadRequest, result!.StatusCode);
	}

	[Fact]
	public async Task UpdateRolePermissions_TenantRole_Replaces_Set()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx);
		await ctrl.CreateRole(
			new IdentityController.CreateRoleRequest(Tenant5, "ops2", "运维2", Permissions: new[] { "dashboard:view" }),
			CancellationToken.None);

		var update = await ctrl.UpdateRolePermissions(
			"ops2",
			new IdentityController.UpdateRolePermissionsRequest(new[] { "app:create", "app:view" }),
			Tenant5,
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(update);

		var detail = Assert.IsType<IdentityController.RoleDetail>(update!.Value);
		Assert.Equal(new[] { "app:create", "app:view" }.OrderBy(x => x), detail.Permissions.OrderBy(x => x));
	}

	[Fact]
	public async Task UpdateRolePermissions_GlobalRole_Rejected_400()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx);
		var result = await ctrl.UpdateRolePermissions(
			"viewer",
			new IdentityController.UpdateRolePermissionsRequest(new[] { "app:delete" }),
			Tenant5,
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
		Assert.Equal((int)HttpStatusCode.BadRequest, result!.StatusCode);
	}

	[Fact]
	public async Task PermissionsCatalog_Returns_All_Global()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx);
		var result = await ctrl.ListPermissions(Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);
		var perms = Assert.IsAssignableFrom<IEnumerable<IdentityController.PermissionSummary>>(result!.Value).ToList();
		// IdentityCatalog.Permissions 共 24 项（全部 TenantId=0）
		Assert.Equal(24, perms.Count);
		Assert.All(perms, p => Assert.Equal(0, p.TenantId));
		Assert.Contains(perms, p => p.Code == "dashboard:view");
		Assert.Contains(perms, p => p.Code == "identity:manage");
	}

	[Fact]
	public async Task CreateUser_WithRoleCodes_Grants_Permissions()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateUserAsync(ctx, Tenant5, "heidi", new[] { "tenant-admin" });
		var ctrl = Build(ctx);
		var perms = await ctrl.GetPermissions(id, Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(perms);
		var body = Assert.IsType<IdentityController.UserPermissionsResponse>(perms!.Value);
		var codes = body.Permissions.ToList();
		// tenant-admin 拥有 identity:manage，但不含平台账单管理 billing:manage
		Assert.Contains("identity:manage", codes);
		Assert.Contains("agent:manage", codes);
		Assert.DoesNotContain("billing:manage", codes);
	}
}
