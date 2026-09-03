using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.Auth;

namespace SuperBuilder_AI.Services.Identity;

/// <summary>从安全配置幂等创建首个平台治理管理员；已有治理管理员时不做任何修改。</summary>
public sealed class PlatformAdminBootstrapper
{
    private readonly SuperBIContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IConfiguration _configuration;

    public PlatformAdminBootstrapper(SuperBIContext db, IPasswordHasher hasher, IConfiguration configuration)
    {
        _db = db;
        _hasher = hasher;
        _configuration = configuration;
    }

    public async Task<bool> EnsureAsync(CancellationToken ct = default)
    {
        if (await HasAdministratorAsync(ct)) return false;

        var username = (_configuration["PlatformBootstrap:Username"] ?? string.Empty).Trim();
        var password = _configuration["PlatformBootstrap:Password"] ?? string.Empty;
        var displayName = (_configuration["PlatformBootstrap:DisplayName"] ?? "平台系统管理员").Trim();
        if (username.Length == 0 || password.Length < 8) return false;
        await CreateAsync(username, password, displayName, ct);
        return true;
    }

    public async Task<bool> HasAdministratorAsync(CancellationToken ct = default)
    {
        var platformTenantId = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.TenantCode == IdentityService.PlatformTenantCode)
            .Select(t => t.Id).SingleAsync(ct);
        var platformRoleId = await _db.Roles
            .Where(r => r.TenantId == 0 && r.Code == IdentityRoles.PlatformAdmin)
            .Select(r => r.Id).SingleAsync(ct);
        return await _db.UserRoles.AnyAsync(ur =>
            ur.TenantId == platformTenantId && ur.RoleId == platformRoleId, ct);
    }

    public Task<long> GetPlatformTenantIdAsync(CancellationToken ct = default) =>
        _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.TenantCode == IdentityService.PlatformTenantCode)
            .Select(t => t.Id).SingleAsync(ct);

    public async Task<(long TenantId, long UserId)> CreateAsync(
        string username, string password, string? displayName, CancellationToken ct = default)
    {
        username = (username ?? string.Empty).Trim();
        password ??= string.Empty;
        displayName = (displayName ?? "平台系统管理员").Trim();
        if (username.Length == 0 || password.Length < 8)
            throw new ArgumentException("用户名必填，口令至少 8 位。");

        var platformTenant = await _db.Tenants.IgnoreQueryFilters()
            .SingleAsync(t => t.TenantCode == IdentityService.PlatformTenantCode, ct);
        var platformRole = await _db.Roles
            .SingleAsync(r => r.TenantId == 0 && r.Code == IdentityRoles.PlatformAdmin, ct);
        if (await _db.UserRoles.AnyAsync(ur => ur.TenantId == platformTenant.Id && ur.RoleId == platformRole.Id, ct))
            throw new InvalidOperationException("平台管理员已经存在，初始化入口已关闭。");
        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username, ct))
            throw new InvalidOperationException($"初始化平台管理员失败：用户名 {username} 已被其他账号占用。");

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var user = new User
        {
            TenantId = platformTenant.Id,
            Username = username,
            DisplayName = displayName.Length == 0 ? username : displayName,
            Email = string.Empty,
            PasswordHash = _hasher.Hash(password),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Status = UserStatus.Active,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        _db.UserRoles.Add(new UserRole
        {
            TenantId = platformTenant.Id,
            UserId = user.Id,
            RoleId = platformRole.Id,
        });
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (platformTenant.Id, user.Id);
    }
}
