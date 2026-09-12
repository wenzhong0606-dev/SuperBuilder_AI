using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Interfaces.Seed;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Models.Audit;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Services.Seed;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Localization;
using SuperBuilder_AI.Services.BI.Dashboard;
using SuperBuilder_AI.Services.Identity;
using SuperBuilder_AI.Infrastructure.Security;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M2-07 演示数据安装器（DEC-05：独立安装器，不进生产默认种子）。
/// 验证：预览计划、事务原子安装（租户+管理员+数据源+元数据+字段语义+业务实体+仪表盘）、重复安装幂等。
/// SQLite 内存库 + 真实 DbContext + 真实 IdentityService（含角色目录种子）。
/// </summary>
public sealed class DemoDataInstallerTests
{
    private static SuperBIContext CreateContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        var ctx = new SuperBIContext(options);
        ctx.Database.EnsureCreated();
        // M3-01：平台语言目录（演示安装器据此为演示租户播种默认语言关系）
        ctx.UiLanguages.AddRange(
            new UiLanguage { Id = 1, Culture = "zh-CN", DisplayName = "中文", NativeName = "简体中文", Enabled = true, SortOrder = 0 },
            new UiLanguage { Id = 2, Culture = "en-US", DisplayName = "English", NativeName = "English", Enabled = true, SortOrder = 1 });
        ctx.SaveChanges();
        return ctx;
    }

    private static DemoDataInstaller Build(SuperBIContext ctx, IAuditLogService? audit = null)
    {
        var identity = new IdentityService(ctx, new PasswordHasher());
        identity.SeedAsync().GetAwaiter().GetResult();
        return new DemoDataInstaller(ctx, identity, audit ?? new NoopAuditService(), new DashboardDslSerializer(), new TenantLanguageService(ctx, new NoopAuditService()), new AesGcmSecretStore(new byte[32]));
    }

    [Fact]
    public async Task Preview_NotInstalled_ReturnsPlanWithEightItems()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;

        var plan = await Build(ctx).PreviewAsync(CancellationToken.None);

        Assert.False(plan.AlreadyInstalled);
        Assert.Equal("demo", plan.DemoTenantCode);
        Assert.Equal(8, plan.Items.Count);
    }

    [Fact]
    public async Task Install_CreatesDemoTenantAdminAndSampleData_Atomically()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;

        var audit = new RecordingAuditService();
        var installer = Build(ctx, audit);
        var result = await installer.InstallAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("installed", result.Status);
        Assert.False(string.IsNullOrEmpty(result.AdminPassword));

        var tenant = await ctx.Tenants.SingleAsync(t => t.TenantCode == "demo");
        Assert.True(tenant.Enabled);

        // M3-01：本地化配置以关系模型 TenantUiLanguage 落库（默认 zh-CN），不再写 localization:* JSON。
        // T4 行为变更：安装器按平台全语言集播种（本 fixture 启用 2 种），默认语言仍为 zh-CN。
        Assert.Equal(0, await ctx.TenantSettings.CountAsync(s => s.TenantId == tenant.Id));
        var demoLang = await ctx.TenantUiLanguages.SingleAsync(x => x.TenantId == tenant.Id && x.IsDefault);
        Assert.True(demoLang.IsDefault);
        Assert.Equal("zh-CN", (await ctx.UiLanguages.FindAsync(demoLang.UiLanguageId))!.Culture);

        // 管理员（TenantAdmin）
        var admin = await ctx.Users.SingleAsync(u => u.TenantId == tenant.Id && u.Username == "demo_admin");
        Assert.StartsWith("pbkdf2:", admin.PasswordHash);
        Assert.False(string.IsNullOrEmpty(admin.SecurityStamp));
        Assert.True(await ctx.UserRoles.AnyAsync(r => r.UserId == admin.Id));

        // 数据源 / 元数据 / 字段 / 业务实体 / 仪表盘
        Assert.Equal(1, await ctx.DataSources.CountAsync(d => d.TenantId == tenant.Id));
        Assert.Equal(1, await ctx.MetadataTables.CountAsync(m => m.TenantId == tenant.Id));
        var demoTableIds = await ctx.MetadataTables.Where(m => m.TenantId == tenant.Id).Select(m => m.Id).ToListAsync();
        Assert.Equal(5, await ctx.MetadataColumns.CountAsync(c => demoTableIds.Contains(c.MetadataTableId)));

        var colIds = await ctx.MetadataColumns.Where(c => demoTableIds.Contains(c.MetadataTableId)).Select(c => c.Id).ToListAsync();
        Assert.Equal(5, await ctx.MetadataSemantics.CountAsync(s => colIds.Contains(s.MetadataColumnId ?? 0)));

        Assert.Equal(1, await ctx.BusinessEntities.CountAsync(b => b.TenantId == tenant.Id));

        var dash = await ctx.Dashboards.SingleAsync(x => x.TenantId == tenant.Id);
        Assert.False(string.IsNullOrEmpty(dash.DslJson));
        using var doc = JsonDocument.Parse(dash.DslJson);
        // 序列化器按 RFC8259 转义非 ASCII（\uXXXX），故断言解码后的 title 属性而非字面子串。
        Assert.Equal("演示销售概览", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("published", dash.Status);

        // 审计：验证安装器确实写入了 demo.seed 事件（测试使用记录桩，不落库）。
        var seedAudit = audit.Entries.SingleOrDefault(e => e.Action == "demo.seed");
        Assert.NotNull(seedAudit);
        Assert.Equal(tenant.Id, seedAudit!.TenantId);
        Assert.Equal("Tenant", seedAudit.EntityType);
    }

    [Fact]
    public async Task Install_WhenAlreadyInstalled_ReturnsAlreadyInstalled_NoDuplicates()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;

        var installer = Build(ctx);
        var first = await installer.InstallAsync(CancellationToken.None);
        Assert.Equal("installed", first.Status);

        var second = await installer.InstallAsync(CancellationToken.None);
        Assert.Equal("already_installed", second.Status);

        // 重复安装不得产生重复数据
        Assert.Equal(1, await ctx.Tenants.CountAsync(t => t.TenantCode == "demo"));
        Assert.Equal(1, await ctx.Users.CountAsync(u => u.Username == "demo_admin"));
        Assert.Equal(1, await ctx.Dashboards.CountAsync(d => d.TenantId == first.TenantId));
    }

    /// <summary>记录型审计桩：捕获写入的事件以便断言，不落库（与 NoopAuditService 互补）。</summary>
    private sealed class RecordingAuditService : IAuditLogService
    {
        public List<AuditLogEntry> Entries { get; } = new();
        public Task<long> LogAsync(AuditLogEntry entry, CancellationToken ct = default)
        {
            Entries.Add(entry);
            return Task.FromResult((long)Entries.Count);
        }
        public Task<IReadOnlyList<AuditLog>> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AuditLog>>(new List<AuditLog>());
    }
}
