using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P10.2 IdentityController 单测（SQLite 内存库 + 真实 IdentityService，不依赖 Moq）。
/// 覆盖：租户作用域用户 CRUD、角色指派/撤销、权限解析、跨租户隔离、
/// 全局角色/权限目录可见不可改、租户角色创建与权限集替换。
/// AUTH-01.2/3 增补：身份治理权限门禁（identity:manage）与自查豁免的判定与无副作用保证。
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
		ctx.Tenants.AddRange(
			new Tenant { Id = Tenant5, TenantCode = "t5", TenantName = "Tenant 5", Enabled = true },
			new Tenant { Id = Tenant6, TenantCode = "t6", TenantName = "Tenant 6", Enabled = true });
		ctx.SaveChanges();
		return ctx;
	}

	private static IdentityController Build(SuperBIContext db)
	{
		var svc = new IdentityService(db, new PasswordHasher());
		// 幂等种子全局目录（与启动期一致），使角色/权限解析可工作。
		svc.SeedAsync(CancellationToken.None).GetAwaiter().GetResult();
		return new IdentityController(db, svc);
	}

	private static ClaimsPrincipal Principal(long tenantId) =>
		new(new ClaimsIdentity(new[]
		{
			new Claim("tid", tenantId.ToString()),
			new Claim("perm", IdentityPermissions.IdentityManage),
		}, "Bearer"));

	private static ClaimsPrincipal AdminPrincipal(long tenantId) =>
		new(new ClaimsIdentity(new[]
		{
			new Claim("tid", tenantId.ToString()),
			new Claim("perm", IdentityPermissions.IdentityManage),
		}, "Bearer"));

	private static IdentityController BuildAdmin(SuperBIContext db, long tenantId)
	{
		var ctrl = Build(db);
		ctrl.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = AdminPrincipal(tenantId) }
		};
		return ctrl;
	}

	private static ClaimsPrincipal MemberPrincipal(long tenantId) =>
		new(new ClaimsIdentity(new[] { new Claim("tid", tenantId.ToString()) }, "Bearer"));

	private static IdentityController BuildMember(SuperBIContext db, long tenantId)
	{
		var ctrl = Build(db);
		ctrl.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = MemberPrincipal(tenantId) }
		};
		return ctrl;
	}

	private static async Task<long> CreateUserAsync(SuperBIContext db, long tenantId, string username, string[]? roles = null)
	{
		var ctrl = BuildAdmin(db, tenantId);
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

		var ctrl = BuildAdmin(ctx, Tenant5);
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
		var ctrl = BuildAdmin(ctx, Tenant5);
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

		var ctrl = BuildAdmin(ctx, Tenant5);
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
		var ctrl = BuildAdmin(ctx, Tenant5);
		var result = await ctrl.GetUser(id, Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;

		Assert.NotNull(result);
		var summary = Assert.IsType<IdentityController.UserSummary>(result!.Value);
		Assert.Equal("carol", summary.Username);
	}

	[Fact]
	public async Task GetUser_WithoutIdentityManage_Returns_403()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateUserAsync(ctx, Tenant5, "dave");
		// 未携带 identity:manage 的主体（即便同租户）读取他人 → 403。
		var ctrl = Build(ctx);
		var result = await ctrl.GetUser(id, Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.ObjectResult;

		Assert.NotNull(result);
		Assert.Equal(403, result!.StatusCode);
	}

	[Fact]
	public async Task GetUser_CrossTenant_WithAuthenticatedToken_Rejected403()
	{
		// 生产路径：携带租户 A 令牌、却以请求参数指定租户 B 访问 → 数据面单租户恒等拒绝（403）。
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateUserAsync(ctx, Tenant6, "dave");
		var ctrl = Build(ctx);
		ctrl.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = Principal(Tenant5) }
		};

		var ex = await Assert.ThrowsAsync<SuperBuilderException>(() => ctrl.GetUser(id, Tenant6, CancellationToken.None));
		Assert.Equal(403, ex.StatusCode);
		Assert.Equal(ErrorCodes.TenantIsolated, ex.ErrorCode);
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
		var ctrl = BuildAdmin(ctx, Tenant5);

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
		var ctrl = BuildAdmin(ctx, Tenant5);

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
		// 即便具备 identity:manage，跨租户指派仍被数据面单租户恒等拒绝（403 TenantIsolated）。
		var ctrl = BuildAdmin(ctx, Tenant5);
		var ex = await Assert.ThrowsAsync<SuperBuilderException>(() => ctrl.AssignRole(id, new IdentityController.AssignRoleRequest("viewer"), Tenant6, CancellationToken.None));
		Assert.Equal(403, ex.StatusCode);
		Assert.Equal(ErrorCodes.TenantIsolated, ex.ErrorCode);
	}

	[Fact]
	public async Task RolesCatalog_Includes_Global_And_Tenant()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 创建租户角色
		var ctrl = BuildAdmin(ctx, Tenant5);
		var create = await ctrl.CreateRole(
			new IdentityController.CreateRoleRequest(Tenant5, "ops", "运维", Permissions: new[] { "metadata:view" }),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
		Assert.NotNull(create);

		var list = await ctrl.ListRoles(Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(list);
		var roles = Assert.IsAssignableFrom<IEnumerable<IdentityController.RoleSummary>>(list!.Value).ToList();
		// platform-admin 对租户目录隐藏：3 个业务全局角色 + 1 个租户角色。
		Assert.Equal(4, roles.Count);
		Assert.DoesNotContain(roles, r => r.Code == "platform-admin");
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

		var ctrl = BuildAdmin(ctx, Tenant5);
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

		var ctrl = BuildAdmin(ctx, Tenant5);
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

		var ctrl = BuildAdmin(ctx, Tenant5);
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
		// 普通租户目录隐藏 8 项 platform:* 治理权限；新增 localization:* 亦属治理面，对租户不可见。
		Assert.Equal(26, perms.Count);
		Assert.All(perms, p => Assert.Equal(0, p.TenantId));
		Assert.Contains(perms, p => p.Code == "dashboard:view");
		Assert.Contains(perms, p => p.Code == "identity:manage");
		Assert.DoesNotContain(perms, p => p.Code.StartsWith("platform:"));
	}

	[Fact]
	public async Task CreateUser_WithRoleCodes_Grants_Permissions()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateUserAsync(ctx, Tenant5, "heidi", new[] { "tenant-admin" });
		var ctrl = BuildAdmin(ctx, Tenant5);
		var perms = await ctrl.GetPermissions(id, Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(perms);
		var body = Assert.IsType<IdentityController.UserPermissionsResponse>(perms!.Value);
		var codes = body.Permissions.ToList();
		// tenant-admin 拥有 identity:manage，但不含平台账单管理 billing:manage
		Assert.Contains("identity:manage", codes);
		Assert.Contains("agent:manage", codes);
		Assert.DoesNotContain("billing:manage", codes);
	}

	[Fact]
	public async Task Write_WithoutIdentityManage_Returns_403_NoSideEffects()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 预置：管理员创建用户并指派角色，记录关键表计数与安全戳。
		var id = await CreateUserAsync(ctx, Tenant5, "target");
		var admin = BuildAdmin(ctx, Tenant5);
		await admin.AssignRole(id, new IdentityController.AssignRoleRequest("viewer"), Tenant5, CancellationToken.None);

		var before = await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == id, CancellationToken.None);
		var userCountBefore = await ctx.Users.CountAsync(CancellationToken.None);
		var userRoleBefore = await ctx.UserRoles.CountAsync(CancellationToken.None);
		var rolePermBefore = await ctx.RolePermissions.CountAsync(CancellationToken.None);

		// 成员（无 identity:manage）逐一调用 6 个写接口 → 全部 403。
		var member = BuildMember(ctx, Tenant5);
		Assert.Equal(403, (await member.CreateUser(new IdentityController.CreateUserRequest(Tenant5, "x"), CancellationToken.None) as Microsoft.AspNetCore.Mvc.ObjectResult)!.StatusCode);
		Assert.Equal(403, (await member.AssignRole(id, new IdentityController.AssignRoleRequest("member"), Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.ObjectResult)!.StatusCode);
		Assert.Equal(403, (await member.RevokeRole(id, "viewer", Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.ObjectResult)!.StatusCode);
		Assert.Equal(403, (await member.SetUserStatus(id, new IdentityController.SetUserStatusRequest(UserStatus.Disabled), Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.ObjectResult)!.StatusCode);
		Assert.Equal(403, (await member.CreateRole(new IdentityController.CreateRoleRequest(Tenant5, "xr"), CancellationToken.None) as Microsoft.AspNetCore.Mvc.ObjectResult)!.StatusCode);
		Assert.Equal(403, (await member.UpdateRolePermissions("viewer", new IdentityController.UpdateRolePermissionsRequest(new[] { "dashboard:view" }), Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.ObjectResult)!.StatusCode);

		// 断言无副作用：Users / UserRoles / RolePermissions 计数与 SecurityStamp 均不变。
		var after = await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == id, CancellationToken.None);
		Assert.Equal(userCountBefore, await ctx.Users.CountAsync(CancellationToken.None));
		Assert.Equal(userRoleBefore, await ctx.UserRoles.CountAsync(CancellationToken.None));
		Assert.Equal(rolePermBefore, await ctx.RolePermissions.CountAsync(CancellationToken.None));
		Assert.Equal(before.SecurityStamp, after.SecurityStamp);
	}

	[Fact]
	public async Task GetUser_SelfRead_Allowed_WithoutManage()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateUserAsync(ctx, Tenant5, "selfuser");
		// 本人主体：NameIdentifier == id，但不携带 identity:manage → 自查豁免允许读取自身。
		var self = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim("tid", Tenant5.ToString()),
			new Claim(ClaimTypes.NameIdentifier, id.ToString()),
		}, "Bearer"));
		var ctrl = Build(ctx);
		ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = self } };

		var result = await ctrl.GetUser(id, Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);
		var summary = Assert.IsType<IdentityController.UserSummary>(result!.Value);
		Assert.Equal("selfuser", summary.Username);
	}

	[Fact]
	public async Task GetPermissions_SelfRead_Allowed_WithoutManage()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateUserAsync(ctx, Tenant5, "selfperm", new[] { "viewer" });
		var self = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim("tid", Tenant5.ToString()),
			new Claim(ClaimTypes.NameIdentifier, id.ToString()),
		}, "Bearer"));
		var ctrl = Build(ctx);
		ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = self } };

		var result = await ctrl.GetPermissions(id, Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);
	}
}
