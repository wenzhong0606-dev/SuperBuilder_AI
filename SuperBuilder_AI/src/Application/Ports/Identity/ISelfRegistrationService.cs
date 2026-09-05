namespace SuperBuilder_AI.Interfaces.Identity;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>自助注册请求（M2-06）。</summary>
public sealed record SelfRegistrationRequest(
    string? TenantCode,
    string? TenantName,
    string? AdminUsername,
    string? AdminEmail,
    string? AdminPassword,
    string? AdminDisplayName = null,
    string? CaptchaToken = null);

/// <summary>自助注册结果（M2-06）。</summary>
public sealed record SelfRegistrationResult(
    bool Success,
    /// <summary>ok | disabled | feature_not_implemented | invalid | conflict</summary>
    string Status,
    string? Token = null,
    int ExpiresInSeconds = 0,
    long TenantId = 0,
    long UserId = 0,
    string? Username = null,
    IReadOnlyList<string>? Permissions = null,
    IReadOnlyList<string>? AvailableCultures = null,
    string? DefaultCulture = null,
    string? Error = null);

/// <summary>租户自助注册服务（M2-06 骨架）。</summary>
public interface ISelfRegistrationService
{
    /// <summary>
    /// 处理一次自助注册。开关关闭或待裁决特性开启时返回非 ok 结果，不会创建任何数据。
    /// 通过校验后以事务原子创建「租户 + 租户设置 + 首位租户管理员 + 口令」并重签令牌。
    /// </summary>
    Task<SelfRegistrationResult> RegisterAsync(SelfRegistrationRequest request, CancellationToken ct = default);
}
