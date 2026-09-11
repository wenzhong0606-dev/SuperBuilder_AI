using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Interfaces.Seed;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Services.Seed;

/// <summary>
/// E2E 沙箱种子实现（env-gated，仅 E2E_SEED=true 时由 API 启动序列调用）。
/// <para>
/// 复用 <see cref="IIdentityService.CreateUserAsync"/> / <see cref="IIdentityService.SetPasswordAsync"/>（与 DemoDataInstaller 同模式），
/// 创建 <c>e2eapp</c> 租户（自动 id，CI 下通常为 2；code 恒定故矩阵按 code 解析 id，不依赖具体数字）+ 两个账号。
/// e2eadmin 赋 <c>tenant-admin</c>（含 theme:edit/metadata:edit/dashboard:*/app:*/agent:*，满足管理员正向断言）；
/// e2ereader 赋 <c>viewer</c>（仅 view 权限或无此角色则零权限，创建/编辑按钮必隐藏，满足读者负向断言）。
/// </para>
/// </summary>
public sealed class E2ESandboxSeedService : IE2ESandboxSeedService
{
    private const string E2ETenantCode = "e2eapp";
    private const string E2ETenantName = "E2E App";
    private const string AdminUser = "e2eadmin";
    private const string ReaderUser = "e2ereader";
    private const string E2EPassword = "longping00";

    private readonly SuperBIContext _db;
    private readonly IIdentityService _identity;
    private readonly ITenantLanguageService _tenantLanguage;

    public E2ESandboxSeedService(SuperBIContext db, IIdentityService identity, ITenantLanguageService tenantLanguage)
    {
        _db = db;
        _identity = identity;
        _tenantLanguage = tenantLanguage;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existing = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantCode == E2ETenantCode, ct);
        if (existing is not null) return; // 幂等

        var tenant = new Tenant { TenantCode = E2ETenantCode, TenantName = E2ETenantName, Enabled = true };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        // M3-01：语言授权关系，确保 e2eapp 至少一种启用语言（与 DemoDataInstaller 同模式）
        await _tenantLanguage.EnsureTenantLanguagesAsync(tenant.Id, ct);

        var admin = await _identity.CreateUserAsync(
            tenant.Id, AdminUser, "E2E Admin", $"{AdminUser}@{E2ETenantCode}.local",
            new[] { IdentityRoles.TenantAdmin }, ct);
        if (admin.Success && admin.Id.HasValue)
            await _identity.SetPasswordAsync(tenant.Id, admin.Id.Value, E2EPassword, ct);

        var reader = await _identity.CreateUserAsync(
            tenant.Id, ReaderUser, "E2E Reader", $"{ReaderUser}@{E2ETenantCode}.local",
            new[] { IdentityRoles.Viewer }, ct);
        if (reader.Success && reader.Id.HasValue)
            await _identity.SetPasswordAsync(tenant.Id, reader.Id.Value, E2EPassword, ct);
    }
}
