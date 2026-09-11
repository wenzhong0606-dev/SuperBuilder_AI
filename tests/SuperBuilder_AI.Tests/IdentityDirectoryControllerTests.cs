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
/// M12-17 IdentityDirectoryController 单测（SQLite 内存库 + 真实服务，不依赖 Moq）。
/// 覆盖：组织/部门/用户组创建与列举、跨租户 403、缺权限 403、
/// 部门须属本租户组织、用户组承载角色 → 成员经组获得权限（RBAC 关联）、
/// 成员增删、角色集替换、用户部门归属设置/清除。
/// </summary>
public class IdentityDirectoryControllerTests
{
	private const long Tenant5 = 5;
	private const long Tenant6 = 6;
	private const string Manage = IdentityPermissions.IdentityManage;

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
		// 幂等种子全局角色/权限目录（与启动期一致）：组织目录「组→角色」关联依赖该目录存在。
		new IdentityService(ctx, new PasswordHasher()).SeedAsync(CancellationToken.None).GetAwaiter().GetResult();
		return ctx;
	}

	private static IdentityService BuildIdentity(SuperBIContext db)
	{
		var svc = new IdentityService(db, new PasswordHasher());
		svc.SeedAsync(CancellationToken.None).GetAwaiter().GetResult();
		return svc;
	}

	private static IdentityDirectoryController Build(SuperBIContext db, long tenantId, bool withPermission = true)
	{
		var ctrl = new IdentityDirectoryController(new IdentityDirectoryService(db));
		var claims = new List<Claim> { new("tid", tenantId.ToString()) };
		if (withPermission) claims.Add(new Claim("perm", Manage));
		ctrl.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) }
		};
		return ctrl;
	}

	private static async Task<long> CreateUserAsync(SuperBIContext db, long tenantId, string username)
	{
		var svc = BuildIdentity(db);
		var result = await svc.CreateUserAsync(tenantId, username, username, $"{username}@x.com", null, CancellationToken.None);
		Assert.True(result.Success, string.Join(";", result.Errors));
		return result.Id!.Value;
	}

	private static async Task<long> CreateOrgAsync(SuperBIContext db, long tenantId, string code)
	{
		var ctrl = Build(db, tenantId);
		var res = await ctrl.CreateOrganization(
			new IdentityDirectoryController.CreateOrganizationRequest(tenantId, code, code.ToUpperInvariant()),
			CancellationToken.None);
		var ok = Assert.IsType<ObjectResult>(res);
		Assert.Equal(201, ok.StatusCode);
		return (long)ok.Value!.GetType().GetProperty("id")!.GetValue(ok.Value)!;
	}

	// ---------------- 组织 ----------------

	[Fact]
	public async Task CreateOrganization_Then_List_Returns_It()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateOrgAsync(ctx, Tenant5, "hq");
		var ctrl = Build(ctx, Tenant5);
		var list = await ctrl.ListOrganizations(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;

		Assert.NotNull(list);
		var items = Assert.IsType<DirectoryPage<OrganizationView>>(list!.Value).Items.ToList();
		Assert.Single(items);
		Assert.Equal(id, items[0].Id);
		Assert.Equal("hq", items[0].Code);
		Assert.True(items[0].IsEnabled);
	}

	[Fact]
	public async Task CreateOrganization_DuplicateCode_Returns_400()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		await CreateOrgAsync(ctx, Tenant5, "hq");
		var ctrl = Build(ctx, Tenant5);
		var res = await ctrl.CreateOrganization(
			new IdentityDirectoryController.CreateOrganizationRequest(Tenant5, "HQ"), CancellationToken.None);

		var bad = Assert.IsType<BadRequestObjectResult>(res);
		Assert.Equal((int)HttpStatusCode.BadRequest, bad.StatusCode);
	}

	[Fact]
	public async Task ListOrganizations_CrossTenant_Rejected_403()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		await CreateOrgAsync(ctx, Tenant6, "hq6");
		var ctrl = Build(ctx, Tenant5); // 令牌租户 5，请求租户 6
		var ex = await Assert.ThrowsAsync<SuperBuilderException>(
			() => ctrl.ListOrganizations(Tenant6, 0, 0, CancellationToken.None));
		Assert.Equal(403, ex.StatusCode);
		Assert.Equal(ErrorCodes.TenantIsolated, ex.ErrorCode);
	}

	[Fact]
	public async Task MissingIdentityManagePermission_Returns_403()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx, Tenant5, withPermission: false);
		var res = await ctrl.ListOrganizations(Tenant5, 0, 0, CancellationToken.None) as ObjectResult;
		Assert.NotNull(res);
		Assert.Equal(403, res!.StatusCode);
	}

	[Fact]
	public async Task ListOrganizations_Excludes_OtherTenant()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		await CreateOrgAsync(ctx, Tenant5, "hq5");
		await CreateOrgAsync(ctx, Tenant6, "hq6");
		var ctrl = Build(ctx, Tenant5);
		var list = await ctrl.ListOrganizations(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;

		var items = Assert.IsType<DirectoryPage<OrganizationView>>(list!.Value).Items.ToList();
		Assert.Single(items);
		Assert.Equal("hq5", items[0].Code);
	}

	// ---------------- 部门 ----------------

	[Fact]
	public async Task CreateDepartment_Under_Tenant_Organization_Then_List()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var orgId = await CreateOrgAsync(ctx, Tenant5, "hq");
		var ctrl = Build(ctx, Tenant5);
		var res = await ctrl.CreateDepartment(
			new IdentityDirectoryController.CreateDepartmentRequest(Tenant5, orgId, null, "sales", "销售部"),
			CancellationToken.None);
		var ok = Assert.IsType<ObjectResult>(res);
		Assert.Equal(201, ok.StatusCode);

		var list = await ctrl.ListDepartments(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;
		var items = Assert.IsType<DirectoryPage<DepartmentView>>(list!.Value).Items.ToList();
		Assert.Single(items);
		Assert.Equal("sales", items[0].Code);
		Assert.Equal(orgId, items[0].OrganizationId);
		Assert.Equal("HQ", items[0].OrganizationName);
	}

	[Fact]
	public async Task CreateDepartment_WithForeignOrganization_Returns_400()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var foreignOrg = await CreateOrgAsync(ctx, Tenant6, "hq6");
		var ctrl = Build(ctx, Tenant5);
		var res = await ctrl.CreateDepartment(
			new IdentityDirectoryController.CreateDepartmentRequest(Tenant5, foreignOrg, null, "sales"),
			CancellationToken.None);

		Assert.IsType<BadRequestObjectResult>(res);
	}

	[Fact]
	public async Task SetUserDepartment_Then_Clear()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var orgId = await CreateOrgAsync(ctx, Tenant5, "hq");
		var userId = await CreateUserAsync(ctx, Tenant5, "deptuser");
		var ctrl = Build(ctx, Tenant5);

		var dept = await ctrl.CreateDepartment(
			new IdentityDirectoryController.CreateDepartmentRequest(Tenant5, orgId, null, "ops"), CancellationToken.None);
		var deptId = (long)((ObjectResult)dept).Value!.GetType().GetProperty("id")!.GetValue(((ObjectResult)dept).Value)!;

		var set = await ctrl.SetUserDepartment(
			userId, new IdentityDirectoryController.SetUserDepartmentRequest(deptId), Tenant5, CancellationToken.None);
		Assert.IsType<OkObjectResult>(set);

		var list = await ctrl.ListDepartments(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;
		Assert.Equal(1, Assert.IsType<DirectoryPage<DepartmentView>>(list!.Value).Items.Single().MemberCount);

		var clear = await ctrl.SetUserDepartment(
			userId, new IdentityDirectoryController.SetUserDepartmentRequest(null), Tenant5, CancellationToken.None);
		Assert.IsType<OkObjectResult>(clear);

		var list2 = await ctrl.ListDepartments(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;
		Assert.Equal(0, Assert.IsType<DirectoryPage<DepartmentView>>(list2!.Value).Items.Single().MemberCount);
	}

	// ---------------- 用户组（RBAC 关联）----------------

	[Fact]
	public async Task CreateUserGroup_WithRoles_Then_List_Shows_RoleCodes()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx, Tenant5);
		var res = await ctrl.CreateUserGroup(
			new IdentityDirectoryController.CreateUserGroupRequest(Tenant5, "analysts", "分析师", null, new[] { "viewer", "member" }),
			CancellationToken.None);
		var ok = Assert.IsType<ObjectResult>(res);
		Assert.Equal(201, ok.StatusCode);

		var list = await ctrl.ListUserGroups(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;
		var items = Assert.IsType<DirectoryPage<UserGroupView>>(list!.Value).Items.ToList();
		Assert.Single(items);
		Assert.Equal(new[] { "member", "viewer" }, items[0].RoleCodes.OrderBy(x => x).ToArray());
		Assert.Equal(0, items[0].MemberCount);
	}

	[Fact]
	public async Task GroupRole_Grants_Permissions_To_Member_On_Join_And_Revokes_On_Leave()
	{
		// M12-17 核心价值：用户组承载角色 → 成员经组获得权限（与直接 UserRole 并集）。
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var userId = await CreateUserAsync(ctx, Tenant5, "member1");
		var identity = BuildIdentity(ctx);
		var ctrl = Build(ctx, Tenant5);

		var created = await ctrl.CreateUserGroup(
			new IdentityDirectoryController.CreateUserGroupRequest(Tenant5, "viewers", "只读组", null, new[] { "viewer" }),
			CancellationToken.None);
		var groupId = (long)((ObjectResult)created).Value!.GetType().GetProperty("id")!.GetValue(((ObjectResult)created).Value)!;

		// 入组前：无任何权限。
		Assert.Empty(await identity.GetPermissionsAsync(Tenant5, userId, CancellationToken.None));

		var add = await ctrl.AddUserGroupMember(
			groupId, new IdentityDirectoryController.AddGroupMemberRequest(userId), Tenant5, CancellationToken.None);
		Assert.IsType<OkObjectResult>(add);

		var perms = await identity.GetPermissionsAsync(Tenant5, userId, CancellationToken.None);
		Assert.Contains("dashboard:view", perms);
		Assert.Contains("metadata:view", perms);
		Assert.DoesNotContain("identity:manage", perms);

		var remove = await ctrl.RemoveUserGroupMember(groupId, userId, Tenant5, CancellationToken.None);
		Assert.IsType<NoContentResult>(remove);

		Assert.Empty(await identity.GetPermissionsAsync(Tenant5, userId, CancellationToken.None));
	}

	[Fact]
	public async Task SetUserGroupRoles_Replaces_Effective_Permissions()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var userId = await CreateUserAsync(ctx, Tenant5, "member2");
		var identity = BuildIdentity(ctx);
		var ctrl = Build(ctx, Tenant5);

		var created = await ctrl.CreateUserGroup(
			new IdentityDirectoryController.CreateUserGroupRequest(Tenant5, "ops", "运维组", null, new[] { "viewer" }),
			CancellationToken.None);
		var groupId = (long)((ObjectResult)created).Value!.GetType().GetProperty("id")!.GetValue(((ObjectResult)created).Value)!;
		await ctrl.AddUserGroupMember(groupId, new IdentityDirectoryController.AddGroupMemberRequest(userId), Tenant5, CancellationToken.None);

		var replace = await ctrl.SetUserGroupRoles(
			groupId, new IdentityDirectoryController.SetUserGroupRolesRequest(new[] { "member" }), Tenant5, CancellationToken.None);
		Assert.IsType<OkObjectResult>(replace);

		var perms = await identity.GetPermissionsAsync(Tenant5, userId, CancellationToken.None);
		Assert.Contains("app:create", perms);  // member 具备
		Assert.DoesNotContain("identity:manage", perms);
	}

	[Fact]
	public async Task AddGroupMember_WithForeignUser_Returns_400()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var foreignUser = await CreateUserAsync(ctx, Tenant6, "outsider");
		var ctrl = Build(ctx, Tenant5);
		var created = await ctrl.CreateUserGroup(
			new IdentityDirectoryController.CreateUserGroupRequest(Tenant5, "grp"), CancellationToken.None);
		var groupId = (long)((ObjectResult)created).Value!.GetType().GetProperty("id")!.GetValue(((ObjectResult)created).Value)!;

		var res = await ctrl.AddUserGroupMember(
			groupId, new IdentityDirectoryController.AddGroupMemberRequest(foreignUser), Tenant5, CancellationToken.None);
		Assert.IsType<BadRequestObjectResult>(res);
	}

	[Fact]
	public async Task ListUserGroups_Excludes_OtherTenant()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var c5 = Build(ctx, Tenant5);
		var c6 = Build(ctx, Tenant6);
		await c5.CreateUserGroup(new IdentityDirectoryController.CreateUserGroupRequest(Tenant5, "g5"), CancellationToken.None);
		await c6.CreateUserGroup(new IdentityDirectoryController.CreateUserGroupRequest(Tenant6, "g6"), CancellationToken.None);

		var list = await c5.ListUserGroups(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;
		var items = Assert.IsType<DirectoryPage<UserGroupView>>(list!.Value).Items.ToList();
		Assert.Single(items);
		Assert.Equal("g5", items[0].Code);
	}

	// ---------------- M12 增量：重命名 / 启停 / 删除 / 分页 ----------------

	[Fact]
	public async Task UpdateOrganization_Renames_And_Updates_Description()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateOrgAsync(ctx, Tenant5, "hq");
		var ctrl = Build(ctx, Tenant5);
		var res = await ctrl.UpdateOrganization(
			id, new IdentityDirectoryController.UpdateOrganizationRequest("总部", "集团总部"), Tenant5, CancellationToken.None);
		Assert.IsType<OkObjectResult>(res);

		var list = await ctrl.ListOrganizations(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;
		var items = Assert.IsType<DirectoryPage<OrganizationView>>(list!.Value).Items.ToList();
		Assert.Equal("总部", items[0].Name);
		Assert.Equal("集团总部", items[0].Description);
		Assert.Equal("hq", items[0].Code); // 编码不可变
	}

	[Fact]
	public async Task SetOrganizationEnabled_Flips_IsEnabled()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var id = await CreateOrgAsync(ctx, Tenant5, "hq");
		var ctrl = Build(ctx, Tenant5);
		Assert.IsType<OkObjectResult>(await ctrl.SetOrganizationEnabled(
			id, new IdentityDirectoryController.SetEnabledRequest(false), Tenant5, CancellationToken.None));

		var list = await ctrl.ListOrganizations(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;
		Assert.False(Assert.IsType<DirectoryPage<OrganizationView>>(list!.Value).Items.Single().IsEnabled);
	}

	[Fact]
	public async Task DeleteOrganization_WithDepartments_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var orgId = await CreateOrgAsync(ctx, Tenant5, "hq");
		var ctrl = Build(ctx, Tenant5);
		await ctrl.CreateDepartment(
			new IdentityDirectoryController.CreateDepartmentRequest(Tenant5, orgId, null, "it"), CancellationToken.None);

		var res = await ctrl.DeleteOrganization(orgId, Tenant5, CancellationToken.None);
		Assert.IsType<BadRequestObjectResult>(res);
	}

	[Fact]
	public async Task DeleteOrganization_Empty_Succeeds()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var orgId = await CreateOrgAsync(ctx, Tenant5, "hq");
		var ctrl = Build(ctx, Tenant5);
		Assert.IsType<NoContentResult>(await ctrl.DeleteOrganization(orgId, Tenant5, CancellationToken.None));

		var list = await ctrl.ListOrganizations(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;
		Assert.Empty(Assert.IsType<DirectoryPage<OrganizationView>>(list!.Value).Items);
	}

	[Fact]
	public async Task ListOrganizations_Paged_Returns_Total_And_Slice()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx, Tenant5);
		foreach (var code in new[] { "a", "b", "c", "d", "e" })
			await ctrl.CreateOrganization(new IdentityDirectoryController.CreateOrganizationRequest(Tenant5, code), CancellationToken.None);

		var page2 = await ctrl.ListOrganizations(Tenant5, 2, 2, CancellationToken.None) as OkObjectResult;
		var paged = Assert.IsType<DirectoryPage<OrganizationView>>(page2!.Value);
		Assert.Equal(5, paged.Total);
		Assert.Equal(2, paged.Page);
		Assert.Equal(2, paged.PageSize);
		Assert.Equal(new[] { "c", "d" }, paged.Items.Select(x => x.Code).ToArray());

		var all = await ctrl.ListOrganizations(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;
		var unpaged = Assert.IsType<DirectoryPage<OrganizationView>>(all!.Value);
		Assert.Equal(5, unpaged.Items.Count);
		Assert.Equal(5, unpaged.Total);
	}

	[Fact]
	public async Task UpdateDepartment_Moves_To_Other_Organization()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var orgA = await CreateOrgAsync(ctx, Tenant5, "org-a");
		var orgB = await CreateOrgAsync(ctx, Tenant5, "org-b");
		var ctrl = Build(ctx, Tenant5);
		var created = await ctrl.CreateDepartment(
			new IdentityDirectoryController.CreateDepartmentRequest(Tenant5, orgA, null, "it"), CancellationToken.None);
		var deptId = (long)Assert.IsType<ObjectResult>(created).Value!.GetType().GetProperty("id")!.GetValue(Assert.IsType<ObjectResult>(created).Value)!;

		var res = await ctrl.UpdateDepartment(
			deptId,
			new IdentityDirectoryController.UpdateDepartmentRequest("IT 部", null, orgB, null),
			Tenant5, CancellationToken.None);
		Assert.IsType<OkObjectResult>(res);

		var list = await ctrl.ListDepartments(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;
		var item = Assert.IsType<DirectoryPage<DepartmentView>>(list!.Value).Items.Single();
		Assert.Equal(orgB, item.OrganizationId);
		Assert.Equal("IT 部", item.Name);
	}

	[Fact]
	public async Task UpdateDepartment_CrossTenant_Organization_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var orgA = await CreateOrgAsync(ctx, Tenant5, "org-a");
		var orgB = await CreateOrgAsync(ctx, Tenant6, "org-b");
		var ctrl = Build(ctx, Tenant5);
		var created = await ctrl.CreateDepartment(
			new IdentityDirectoryController.CreateDepartmentRequest(Tenant5, orgA, null, "it"), CancellationToken.None);
		var deptId = (long)Assert.IsType<ObjectResult>(created).Value!.GetType().GetProperty("id")!.GetValue(Assert.IsType<ObjectResult>(created).Value)!;

		var res = await ctrl.UpdateDepartment(
			deptId, new IdentityDirectoryController.UpdateDepartmentRequest(null, null, orgB, null), Tenant5, CancellationToken.None);
		Assert.IsType<BadRequestObjectResult>(res);
	}

	[Fact]
	public async Task DeleteDepartment_WithMembers_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var orgId = await CreateOrgAsync(ctx, Tenant5, "hq");
		var userId = await CreateUserAsync(ctx, Tenant5, "u1");
		var ctrl = Build(ctx, Tenant5);
		var created = await ctrl.CreateDepartment(
			new IdentityDirectoryController.CreateDepartmentRequest(Tenant5, orgId, null, "it"), CancellationToken.None);
		var deptId = (long)Assert.IsType<ObjectResult>(created).Value!.GetType().GetProperty("id")!.GetValue(Assert.IsType<ObjectResult>(created).Value)!;
		await ctrl.SetUserDepartment(userId, new IdentityDirectoryController.SetUserDepartmentRequest(deptId), Tenant5, CancellationToken.None);

		Assert.IsType<BadRequestObjectResult>(await ctrl.DeleteDepartment(deptId, Tenant5, CancellationToken.None));

		// 移出成员后可删
		await ctrl.SetUserDepartment(userId, new IdentityDirectoryController.SetUserDepartmentRequest(null), Tenant5, CancellationToken.None);
		Assert.IsType<NoContentResult>(await ctrl.DeleteDepartment(deptId, Tenant5, CancellationToken.None));
	}

	[Fact]
	public async Task SetDepartmentEnabled_Flips_IsEnabled()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var orgId = await CreateOrgAsync(ctx, Tenant5, "hq");
		var ctrl = Build(ctx, Tenant5);
		var created = await ctrl.CreateDepartment(
			new IdentityDirectoryController.CreateDepartmentRequest(Tenant5, orgId, null, "it"), CancellationToken.None);
		var deptId = (long)Assert.IsType<ObjectResult>(created).Value!.GetType().GetProperty("id")!.GetValue(Assert.IsType<ObjectResult>(created).Value)!;

		await ctrl.SetDepartmentEnabled(deptId, new IdentityDirectoryController.SetEnabledRequest(false), Tenant5, CancellationToken.None);
		var list = await ctrl.ListDepartments(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;
		Assert.False(Assert.IsType<DirectoryPage<DepartmentView>>(list!.Value).Items.Single().IsEnabled);
	}

	[Fact]
	public async Task UpdateUserGroup_Renames_And_Toggles_Enabled()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx, Tenant5);
		var created = await ctrl.CreateUserGroup(new IdentityDirectoryController.CreateUserGroupRequest(Tenant5, "analysts"), CancellationToken.None);
		var groupId = (long)Assert.IsType<ObjectResult>(created).Value!.GetType().GetProperty("id")!.GetValue(Assert.IsType<ObjectResult>(created).Value)!;

		Assert.IsType<OkObjectResult>(await ctrl.UpdateUserGroup(
			groupId, new IdentityDirectoryController.UpdateUserGroupRequest("分析师组", "只读分析"), Tenant5, CancellationToken.None));
		await ctrl.SetUserGroupEnabled(groupId, new IdentityDirectoryController.SetEnabledRequest(false), Tenant5, CancellationToken.None);

		var list = await ctrl.ListUserGroups(Tenant5, 0, 0, CancellationToken.None) as OkObjectResult;
		var item = Assert.IsType<DirectoryPage<UserGroupView>>(list!.Value).Items.Single();
		Assert.Equal("分析师组", item.Name);
		Assert.Equal("只读分析", item.Description);
		Assert.False(item.IsEnabled);
	}

	[Fact]
	public async Task DeleteUserGroup_Cascades_Members_And_Roles()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var userId = await CreateUserAsync(ctx, Tenant5, "u1");
		var ctrl = Build(ctx, Tenant5);
		var created = await ctrl.CreateUserGroup(
			new IdentityDirectoryController.CreateUserGroupRequest(Tenant5, "analysts", null, null, new[] { "viewer" }), CancellationToken.None);
		var groupId = (long)Assert.IsType<ObjectResult>(created).Value!.GetType().GetProperty("id")!.GetValue(Assert.IsType<ObjectResult>(created).Value)!;
		await ctrl.AddUserGroupMember(groupId, new IdentityDirectoryController.AddGroupMemberRequest(userId), Tenant5, CancellationToken.None);

		Assert.IsType<NoContentResult>(await ctrl.DeleteUserGroup(groupId, Tenant5, CancellationToken.None));

		Assert.Empty(await ctx.UserGroupMembers.IgnoreQueryFilters().ToListAsync());
		Assert.Empty(await ctx.UserGroupRoles.IgnoreQueryFilters().ToListAsync());
		Assert.Empty(await ctx.UserGroups.IgnoreQueryFilters().ToListAsync());
	}

	[Fact]
	public async Task ListUserGroups_Paged_Returns_Total()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var ctrl = Build(ctx, Tenant5);
		foreach (var code in new[] { "g1", "g2", "g3" })
			await ctrl.CreateUserGroup(new IdentityDirectoryController.CreateUserGroupRequest(Tenant5, code), CancellationToken.None);

		var first = await ctrl.ListUserGroups(Tenant5, 1, 2, CancellationToken.None) as OkObjectResult;
		var paged = Assert.IsType<DirectoryPage<UserGroupView>>(first!.Value);
		Assert.Equal(3, paged.Total);
		Assert.Equal(2, paged.Items.Count);
		Assert.Equal(new[] { "g1", "g2" }, paged.Items.Select(x => x.Code).ToArray());
	}
}
