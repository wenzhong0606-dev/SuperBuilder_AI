namespace SuperBuilder_AI.Services.Identity;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Auth;

/// <summary>
/// 租户自助注册服务（M2-06 骨架）。
/// <list type="bullet">
/// <item>默认关闭：<see cref="SelfRegistrationOptions.Enabled"/> 为 false 时一律拒绝。</item>
/// <item>审批 / 验证码为待裁决特性，一旦开启即拒绝注册（避免不安全开放）。</item>
/// <item>通过校验后以与 M2-04 一致的事务原子创建「租户 + 租户设置 + 首位租户管理员 + 口令」，并重签令牌实现注册即登录。</item>
/// </list>
/// </summary>
public sealed class SelfRegistrationService : ISelfRegistrationService
{
    private readonly SuperBIContext _db;
    private readonly IIdentityService _identity;
    private readonly ITokenService _token;
    private readonly IAuditLogService _audit;
    private readonly SelfRegistrationOptions _options;
    private readonly ITenantLanguageService _tenantLanguage;

    public SelfRegistrationService(
        SuperBIContext db,
        IIdentityService identity,
        ITokenService token,
        IAuditLogService audit,
        IOptions<SelfRegistrationOptions> options,
        ITenantLanguageService tenantLanguage)
    {
        _db = db;
        _identity = identity;
        _token = token;
        _audit = audit;
        _options = options.Value;
        _tenantLanguage = tenantLanguage;
    }

    public async Task<SelfRegistrationResult> RegisterAsync(SelfRegistrationRequest request, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return new SelfRegistrationResult(false, "disabled", Error: "租户自助注册当前未开放。");

        // 待裁决特性：审批 / 验证码工作流尚未实现，开启时拒绝，避免不安全地对外开放公网注册。
        if (_options.ApprovalRequired || _options.RequireCaptcha)
        {
            var which = _options.ApprovalRequired && _options.RequireCaptcha
                ? "审批与验证码"
                : _options.ApprovalRequired ? "审批" : "验证码";
            return new SelfRegistrationResult(false, "feature_not_implemented",
                Error: $"{which}功能尚未实现，暂不可开放自助注册。");
        }

        var code = Tenant.NormalizeCode(request.TenantCode);
        if (code.Length == 0 || code.Length > Tenant.MaxCodeLength)
            return new SelfRegistrationResult(false, "invalid", Error: $"TenantCode 必填且长度不超过 {Tenant.MaxCodeLength}。");
        var tenantName = (request.TenantName ?? string.Empty).Trim();
        if (tenantName.Length == 0 || tenantName.Length > Tenant.MaxNameLength)
            return new SelfRegistrationResult(false, "invalid", Error: $"TenantName 必填且长度不超过 {Tenant.MaxNameLength}。");
        var adminUsername = (request.AdminUsername ?? string.Empty).Trim();
        if (adminUsername.Length == 0)
            return new SelfRegistrationResult(false, "invalid", Error: "管理员用户名必填。");
        if (string.IsNullOrWhiteSpace(request.AdminPassword) || request.AdminPassword.Length < 8)
            return new SelfRegistrationResult(false, "invalid", Error: "管理员初始口令至少 8 位。");
        var email = (request.AdminEmail ?? string.Empty).Trim();
        if (email.Length == 0 || !email.Contains('@'))
            return new SelfRegistrationResult(false, "invalid", Error: "管理员邮箱必填且须合法。");

        if (_options.AllowedEmailDomains is { Length: > 0 } domains)
        {
            var domain = email.Contains('@') ? email.Split('@')[1] : string.Empty;
            if (!domains.Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase)))
                return new SelfRegistrationResult(false, "invalid", Error: $"邮箱域名须为：{string.Join(", ", domains)}。");
        }

        if (await _db.Tenants.AnyAsync(t => t.TenantCode == code, ct))
            return new SelfRegistrationResult(false, "conflict", Error: $"租户编码 {code} 已存在。");

        var tenant = new Tenant { TenantCode = code, TenantName = tenantName, Enabled = true };
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        var cultures = (_options.DefaultAvailableCultures ?? new[] { "zh-CN" })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (cultures.Length == 0) cultures = new[] { "zh-CN" };
        var defaultCulture = cultures.Contains(_options.DefaultCulture ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            ? _options.DefaultCulture! : cultures[0];
        // M3-01：语言授权写入关系模型 TenantUiLanguage（替代 localization:* JSON）。
        var supported = await _db.UiLanguages.AsNoTracking().Where(x => x.Enabled)
            .ToDictionaryAsync(x => x.Culture, x => x, StringComparer.OrdinalIgnoreCase, ct);
        var order = 0;
        var updates = cultures.Where(c => supported.TryGetValue(c, out _))
            .Select(c => new TenantLanguageUpdate(
                supported[c].Id, Enabled: true,
                IsDefault: string.Equals(c, defaultCulture, StringComparison.OrdinalIgnoreCase),
                SortOrder: order++)).ToList();
        if (updates.Count == 0)
            await _tenantLanguage.EnsureTenantLanguagesAsync(tenant.Id, ct);
        else
            await _tenantLanguage.SetLanguagesAsync(tenant.Id, updates, 0, ct);

        // 复用 M2-04 的原子创建链路：用户 + 角色 + 口令，任一失败整体回滚。
        var created = await _identity.CreateUserAsync(
            tenant.Id, adminUsername, request.AdminDisplayName ?? adminUsername, email,
            new[] { IdentityRoles.TenantAdmin }, ct);
        if (!created.Success || created.Id is null)
        {
            await tx.RollbackAsync(ct);
            return new SelfRegistrationResult(false, "invalid",
                Error: created.Errors.FirstOrDefault() ?? "租户管理员创建失败。");
        }

        var pwd = await _identity.SetPasswordAsync(tenant.Id, created.Id.Value, request.AdminPassword!, ct);
        if (!pwd.Success)
        {
            await tx.RollbackAsync(ct);
            return new SelfRegistrationResult(false, "invalid",
                Error: pwd.Errors.FirstOrDefault() ?? "租户管理员口令设置失败。");
        }

        await tx.CommitAsync(ct);

        // 读取新用户真实安全戳，签发承载 htid=主租户(=新租户) 的令牌，实现注册即登录。
        var newUser = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == created.Id.Value && u.TenantId == tenant.Id, ct);
        var stamp = newUser?.SecurityStamp;
        var perms = await _identity.GetPermissionsAsync(tenant.Id, created.Id.Value, ct);
        var token = _token.Issue(tenant.Id, created.Id.Value, adminUsername, perms, stamp, tenant.Id);

        await _audit.LogAsync(new AuditLogEntry(
            tenant.Id, "tenant.self-register", "Tenant",
            UserId: created.Id.Value, Actor: adminUsername,
            Result: "success", Message: $"自助注册新建租户 {code}（{tenantName}）"), ct);

        return new SelfRegistrationResult(
            true, "ok",
            Token: token, ExpiresInSeconds: 3600,
            TenantId: tenant.Id, UserId: created.Id.Value, Username: adminUsername,
            Permissions: perms, AvailableCultures: cultures, DefaultCulture: defaultCulture);
    }
}
