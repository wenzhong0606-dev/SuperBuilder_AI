using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// CACHE-01（M13-16）缓存与授权一致性：用户组授权变更 → 轮换受影响成员安全戳。
///
/// <para>
/// 背景：令牌内嵌签发时的权限声明（有效权限 = 直接 <see cref="UserRole"/> ∪ 组角色，见
/// <see cref="IdentityService.GetPermissionsAsync"/>）；<c>AuthMiddleware</c> 每请求以安全戳比对判定是否吊销。
/// 组角色/成员变更会改变用户的**有效权限**但不改动直接角色，若不同步轮换安全戳，旧令牌将携带
/// 过期权限继续生效（最长 60 分钟）。本测试固化「组角色修改 / 组停用启用 / 成员增删 / 删组」
/// 五类变更都必须轮换受影响成员的安全戳，且只轮换受影响者、不跨租户。
/// </para>
/// </summary>
public class IdentityDirectorySecurityStampTests
{
	private const long T5 = 5;
	private const long T6 = 6;
	private const string Key = "cache01-stamp-test-key";

	private static SuperBIContext NewContext(out SqliteConnection conn)
	{
		conn = new SqliteConnection("DataSource=:memory:");
		conn.Open();
		var ctx = new SuperBIContext(new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(conn).Options);
		ctx.Database.EnsureCreated();
		ctx.Tenants.AddRange(
			new Tenant { Id = T5, TenantCode = "t5", TenantName = "T5", Enabled = true },
			new Tenant { Id = T6, TenantCode = "t6", TenantName = "T6", Enabled = true });
		ctx.SaveChanges();
		// 幂等种子全局角色/权限目录（组→角色关联依赖该目录存在）。
		new IdentityService(ctx, new PasswordHasher()).SeedAsync(CancellationToken.None).GetAwaiter().GetResult();
		return ctx;
	}

	private static void AddUser(SuperBIContext db, long id, long tenantId, string stamp)
	{
		db.Users.Add(new User
		{
			Id = id,
			TenantId = tenantId,
			Username = $"u{id}",
			NormalizedUsername = $"u{id}",
			DisplayName = $"u{id}",
			Email = string.Empty,
			NormalizedEmail = string.Empty,
			SecurityStamp = stamp,
		});
		db.SaveChanges();
	}

	private static string StampOf(SuperBIContext db, long tenantId, long userId) =>
		db.Users.IgnoreQueryFilters()
			.Where(u => u.TenantId == tenantId && u.Id == userId)
			.Select(u => u.SecurityStamp)
			.First();

	private static async Task<long> CreateGroupAsync(SuperBIContext db, long tenantId, string code, params string[] roles)
	{
		var dir = new IdentityDirectoryService(db);
		var res = await dir.CreateUserGroupAsync(tenantId, code, code, null, roles, CancellationToken.None);
		Assert.True(res.Success, string.Join(";", res.Errors));
		return res.Id!.Value;
	}

	[Fact]
	public async Task SetUserGroupRoles_Changed_RotatesOnlyGroupMemberStamps()
	{
		using var db = NewContext(out _);
		var dir = new IdentityDirectoryService(db);

		AddUser(db, 101, T5, "s101");
		AddUser(db, 102, T5, "s102");
		AddUser(db, 103, T5, "s103"); // 非本组成员
		AddUser(db, 201, T6, "s201"); // 其它租户成员

		var g5 = await CreateGroupAsync(db, T5, "g5", IdentityRoles.Viewer);
		var g6 = await CreateGroupAsync(db, T6, "g6", IdentityRoles.Viewer);
		await dir.AddUserGroupMemberAsync(T5, g5, 101, CancellationToken.None);
		await dir.AddUserGroupMemberAsync(T5, g5, 102, CancellationToken.None);
		await dir.AddUserGroupMemberAsync(T6, g6, 201, CancellationToken.None);

		// 记录变更前戳（成员入组本身也会轮换，故重新取基线）
		var b101 = StampOf(db, T5, 101);
		var b102 = StampOf(db, T5, 102);
		var b103 = StampOf(db, T5, 103);
		var b201 = StampOf(db, T6, 201);

		var res = await dir.SetUserGroupRolesAsync(T5, g5, new[] { IdentityRoles.Member }, CancellationToken.None);
		Assert.True(res.Success, string.Join(";", res.Errors));

		Assert.NotEqual(b101, StampOf(db, T5, 101)); // 成员轮换
		Assert.NotEqual(b102, StampOf(db, T5, 102)); // 成员轮换
		Assert.Equal(b103, StampOf(db, T5, 103));    // 非成员不变
		Assert.Equal(b201, StampOf(db, T6, 201));    // 跨租户不变
	}

	[Fact]
	public async Task SetUserGroupRoles_SameSet_DoesNotRotate()
	{
		using var db = NewContext(out _);
		var dir = new IdentityDirectoryService(db);
		AddUser(db, 101, T5, "s101");
		var g5 = await CreateGroupAsync(db, T5, "g5", IdentityRoles.Viewer);
		await dir.AddUserGroupMemberAsync(T5, g5, 101, CancellationToken.None);
		var before = StampOf(db, T5, 101);

		var res = await dir.SetUserGroupRolesAsync(T5, g5, new[] { IdentityRoles.Viewer }, CancellationToken.None);
		Assert.True(res.Success);

		Assert.Equal(before, StampOf(db, T5, 101)); // 角色集未变 → 免无谓强制重登
	}

	[Fact]
	public async Task SetUserGroupEnabled_Toggle_RotatesMemberStamps()
	{
		using var db = NewContext(out _);
		var dir = new IdentityDirectoryService(db);
		AddUser(db, 101, T5, "s101");
		var g5 = await CreateGroupAsync(db, T5, "g5", IdentityRoles.Viewer);
		await dir.AddUserGroupMemberAsync(T5, g5, 101, CancellationToken.None);

		var b0 = StampOf(db, T5, 101);
		Assert.True((await dir.SetUserGroupEnabledAsync(T5, g5, false, CancellationToken.None)).Success);
		var b1 = StampOf(db, T5, 101);
		Assert.NotEqual(b0, b1); // 停用 → 轮换

		Assert.True((await dir.SetUserGroupEnabledAsync(T5, g5, true, CancellationToken.None)).Success);
		Assert.NotEqual(b1, StampOf(db, T5, 101)); // 启用 → 再次轮换
	}

	[Fact]
	public async Task RemoveUserGroupMember_RotatesOnlyRemovedUser()
	{
		using var db = NewContext(out _);
		var dir = new IdentityDirectoryService(db);
		AddUser(db, 101, T5, "s101");
		AddUser(db, 102, T5, "s102");
		var g5 = await CreateGroupAsync(db, T5, "g5", IdentityRoles.Viewer);
		await dir.AddUserGroupMemberAsync(T5, g5, 101, CancellationToken.None);
		await dir.AddUserGroupMemberAsync(T5, g5, 102, CancellationToken.None);
		var b101 = StampOf(db, T5, 101);
		var b102 = StampOf(db, T5, 102);

		Assert.True((await dir.RemoveUserGroupMemberAsync(T5, g5, 101, CancellationToken.None)).Success);

		Assert.NotEqual(b101, StampOf(db, T5, 101)); // 被移除者轮换
		Assert.Equal(b102, StampOf(db, T5, 102));    // 其它成员不变
	}

	[Fact]
	public async Task AddUserGroupMember_RotatesAddedUser()
	{
		using var db = NewContext(out _);
		var dir = new IdentityDirectoryService(db);
		AddUser(db, 101, T5, "s101");
		AddUser(db, 103, T5, "s103");
		var g5 = await CreateGroupAsync(db, T5, "g5", IdentityRoles.Viewer);
		await dir.AddUserGroupMemberAsync(T5, g5, 101, CancellationToken.None);
		var b101 = StampOf(db, T5, 101);
		var b103 = StampOf(db, T5, 103);

		Assert.True((await dir.AddUserGroupMemberAsync(T5, g5, 103, CancellationToken.None)).Success);

		Assert.NotEqual(b103, StampOf(db, T5, 103)); // 新成员轮换
		Assert.Equal(b101, StampOf(db, T5, 101));    // 既有成员不变
	}

	[Fact]
	public async Task DeleteUserGroup_RotatesMemberStamps()
	{
		using var db = NewContext(out _);
		var dir = new IdentityDirectoryService(db);
		AddUser(db, 101, T5, "s101");
		AddUser(db, 102, T5, "s102");
		var g5 = await CreateGroupAsync(db, T5, "g5", IdentityRoles.Viewer);
		await dir.AddUserGroupMemberAsync(T5, g5, 101, CancellationToken.None);
		await dir.AddUserGroupMemberAsync(T5, g5, 102, CancellationToken.None);
		var b101 = StampOf(db, T5, 101);
		var b102 = StampOf(db, T5, 102);

		Assert.True((await dir.DeleteUserGroupAsync(T5, g5, CancellationToken.None)).Success);

		Assert.NotEqual(b101, StampOf(db, T5, 101));
		Assert.NotEqual(b102, StampOf(db, T5, 102));
	}

	/// <summary>
	/// 端到端语义：成员经组角色获得 dashboard:view；撤销组角色后——
	/// ① 有效权限不再含 dashboard:view；② 安全戳已变 → 以旧戳签发的令牌被判为已吊销（不得复用）。
	/// </summary>
	[Fact]
	public async Task GroupRoleRevoked_InvalidatesOldToken_AndDropsPermission()
	{
		using var db = NewContext(out _);
		var dir = new IdentityDirectoryService(db);
		var identity = new IdentityService(db, new PasswordHasher());
		AddUser(db, 101, T5, "stamp-old");
		var g5 = await CreateGroupAsync(db, T5, "g5", IdentityRoles.Viewer);
		await dir.AddUserGroupMemberAsync(T5, g5, 101, CancellationToken.None);

		// 组角色 viewer 已授予 dashboard:view。
		var permsBefore = await identity.GetPermissionsAsync(T5, 101, CancellationToken.None);
		Assert.Contains(IdentityPermissions.DashboardView, permsBefore);

		// 以当前（旧）安全戳签发令牌。
		var tokens = new TokenService(Key);
		var oldStamp = StampOf(db, T5, 101);
		var token = tokens.Issue(T5, 101, "u101", permsBefore, oldStamp, T5);
		var principal = tokens.Validate(token);
		Assert.NotNull(principal);
		Assert.Equal(oldStamp, principal!.SecurityStamp);

		// 撤销组角色 → 有效权限应随即回落（组角色不再计入）。
		Assert.True((await dir.SetUserGroupRolesAsync(T5, g5, new string[0], CancellationToken.None)).Success);
		var permsAfter = await identity.GetPermissionsAsync(T5, 101, CancellationToken.None);
		Assert.DoesNotContain(IdentityPermissions.DashboardView, permsAfter);

		// AuthMiddleware 的吊销判定：令牌安全戳 != 当前库中安全戳 → 已吊销（401）。
		var currentStamp = StampOf(db, T5, 101);
		Assert.NotEqual(principal.SecurityStamp, currentStamp);
	}
}
