using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Audit;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M2-01：多平台管理员的常态化治理（列表/新增/停用/启用/重置密码/审计 + 至少保留一名有效管理员）。
/// </summary>
public class M2_01_PlatformAdminTests
{
    private const long PlatformTenantId = 1L;
    private const long PlatformAdminRoleId = 100L;

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        public System.Collections.Generic.List<AuditLogEntry> Entries { get; } = new();
        public Task<long> LogAsync(AuditLogEntry entry, CancellationToken ct = default)
        {
            Entries.Add(entry);
            return Task.FromResult((long)Entries.Count);
        }
        public Task<IReadOnlyList<AuditLog>> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
    }

    private static SuperBIContext CreateContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var ctx = new SuperBIContext(new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static void SeedPlatformAdminScope(SuperBIContext db)
    {
        db.Tenants.Add(new Tenant
        {
            Id = PlatformTenantId,
            TenantCode = IdentityService.PlatformTenantCode,
            TenantName = "Platform Governance",
            Enabled = true,
        });
        db.Roles.Add(new Role
        {
            Id = PlatformAdminRoleId,
            TenantId = 0,
            Code = IdentityRoles.PlatformAdmin,
            Name = "平台治理管理员",
        });
        db.Users.Add(new User
        {
            Id = 10,
            TenantId = PlatformTenantId,
            Username = "admin1",
            NormalizedUsername = "admin1",
            DisplayName = "Admin One",
            Email = "a1@x.io",
            PasswordHash = "legacy",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Status = UserStatus.Active,
        });
        db.Users.Add(new User
        {
            Id = 11,
            TenantId = PlatformTenantId,
            Username = "admin2",
            NormalizedUsername = "admin2",
            DisplayName = "Admin Two",
            Email = "a2@x.io",
            PasswordHash = "legacy",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Status = UserStatus.Active,
        });
        // 平台租户内的非管理员账号（用于验证列表排除）
        db.Users.Add(new User
        {
            Id = 12,
            TenantId = PlatformTenantId,
            Username = "viewer1",
            NormalizedUsername = "viewer1",
            DisplayName = "Viewer",
            Email = "v@x.io",
            PasswordHash = "legacy",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Status = UserStatus.Active,
        });
        db.UserRoles.Add(new UserRole { TenantId = PlatformTenantId, UserId = 10, RoleId = PlatformAdminRoleId });
        db.UserRoles.Add(new UserRole { TenantId = PlatformTenantId, UserId = 11, RoleId = PlatformAdminRoleId });
        db.SaveChanges();
    }

    private static PlatformAdminService BuildService(SuperBIContext db, out RecordingAuditLogService audit)
    {
        audit = new RecordingAuditLogService();
        var identity = new IdentityService(db, new PasswordHasher());
        return new PlatformAdminService(db, identity, new PasswordHasher(), audit);
    }

    [Fact]
    public async Task List_返回全部平台管理员_排除非管理员()
    {
        await using var db = CreateContext(out var connection);
        SeedPlatformAdminScope(db);
        var svc = BuildService(db, out _);

        var list = await svc.ListAsync();
        Assert.Equal(2, list.Count);
        Assert.Contains(list, v => v.Username == "admin1");
        Assert.Contains(list, v => v.Username == "admin2");
        Assert.DoesNotContain(list, v => v.Username == "viewer1");
    }

    [Fact]
    public async Task Add_创建新管理员并授予角色与口令()
    {
        await using var db = CreateContext(out var connection);
        SeedPlatformAdminScope(db);
        var svc = BuildService(db, out _);

        var created = await svc.AddAsync(
            new AddPlatformAdminRequest("admin3", "Str0ngPass!", "Admin Three", "a3@x.io"), "tester");

        Assert.Equal("admin3", created.Username);
        var list = await svc.ListAsync();
        Assert.Equal(3, list.Count);

        var persisted = await db.Users.SingleAsync(u => u.Username == "admin3");
        Assert.True(new PasswordHasher().Verify("Str0ngPass!", persisted.PasswordHash));
        Assert.True(await db.UserRoles.AnyAsync(ur =>
            ur.TenantId == PlatformTenantId && ur.UserId == persisted.Id && ur.RoleId == PlatformAdminRoleId));
    }

    [Fact]
    public async Task Add_已存在账号幂等复用且不新增重复()
    {
        await using var db = CreateContext(out var connection);
        SeedPlatformAdminScope(db);
        var svc = BuildService(db, out _);

        var reused = await svc.AddAsync(
            new AddPlatformAdminRequest("admin1", "Str0ngPass!"), "tester");

        Assert.Equal(10, reused.Id);
        var list = await svc.ListAsync();
        Assert.Equal(2, list.Count); // 不重复创建
        Assert.Equal(2, await db.UserRoles.CountAsync(ur => ur.RoleId == PlatformAdminRoleId));
    }

    [Fact]
    public async Task Add_复用已禁用账号则重新激活()
    {
        await using var db = CreateContext(out var connection);
        SeedPlatformAdminScope(db);
        var svc = BuildService(db, out _);
        await svc.DisableAsync(10, "tester");

        var reused = await svc.AddAsync(new AddPlatformAdminRequest("admin1", "Str0ngPass!"), "tester");

        Assert.Equal(10, reused.Id);
        Assert.Equal(UserStatus.Active, (await db.Users.FindAsync(10L))!.Status);
    }

    [Fact]
    public async Task Disable_停用一名后仍保留至少一名有效管理员()
    {
        await using var db = CreateContext(out var connection);
        SeedPlatformAdminScope(db);
        var svc = BuildService(db, out _);

        await svc.DisableAsync(10, "tester");
        Assert.Equal(UserStatus.Disabled, (await db.Users.FindAsync(10L))!.Status);

        // 此时仅剩 admin2 有效，停用它将触发守卫
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DisableAsync(11, "tester"));
        Assert.Equal(UserStatus.Active, (await db.Users.FindAsync(11L))!.Status);
    }

    [Fact]
    public async Task Enable_重新激活已停用管理员()
    {
        await using var db = CreateContext(out var connection);
        SeedPlatformAdminScope(db);
        var svc = BuildService(db, out _);
        await svc.DisableAsync(10, "tester");

        await svc.EnableAsync(10, "tester");

        Assert.Equal(UserStatus.Active, (await db.Users.FindAsync(10L))!.Status);
    }

    [Fact]
    public async Task ResetPassword_变更哈希并轮换安全戳()
    {
        await using var db = CreateContext(out var connection);
        SeedPlatformAdminScope(db);
        var svc = BuildService(db, out _);
        var before = await db.Users.FindAsync(10L);
        var oldStamp = before!.SecurityStamp;

        await svc.ResetPasswordAsync(10, "BrandNewPass1", "tester");

        var after = await db.Users.FindAsync(10L);
        Assert.NotEqual(oldStamp, after!.SecurityStamp);
        Assert.True(new PasswordHasher().Verify("BrandNewPass1", after.PasswordHash));
    }

    [Fact]
    public async Task Add_短口令被拒()
    {
        await using var db = CreateContext(out var connection);
        SeedPlatformAdminScope(db);
        var svc = BuildService(db, out _);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.AddAsync(new AddPlatformAdminRequest("admin3", "123"), "tester"));
    }

    [Fact]
    public async Task Add_写入审计日志()
    {
        await using var db = CreateContext(out var connection);
        SeedPlatformAdminScope(db);
        var svc = BuildService(db, out var audit);

        await svc.AddAsync(new AddPlatformAdminRequest("admin3", "Str0ngPass!"), "tester");

        var entry = Assert.Single(audit.Entries, e => e.Action == "platform.admin.add");
        Assert.Equal("tester", entry.Actor);
        Assert.Equal("PlatformAdmin", entry.EntityType);
    }

    [Fact]
    public async Task CreateUserAsync_刻意排除platform_admin角色防止越权()
    {
        await using var db = CreateContext(out var connection);
        SeedPlatformAdminScope(db);
        var identity = new IdentityService(db, new PasswordHasher());

        // 即便显式传入 platform-admin，普通身份流也不得授予该治理角色
        var result = await identity.CreateUserAsync(
            PlatformTenantId, "shouldNotBeAdmin", "X", "x@x.io", new[] { IdentityRoles.PlatformAdmin });

        Assert.True(result.Success);
        var granted = await db.UserRoles.AnyAsync(ur =>
            ur.TenantId == PlatformTenantId && ur.UserId == result.Id && ur.RoleId == PlatformAdminRoleId);
        Assert.False(granted);
    }
}
