using System.Collections.Generic;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 会话数据载体（Phase 1）：在 <see cref="IAuthPersistence"/> 后端与 <see cref="AppState"/> 间传递的纯数据。
///
/// <para>
/// Web 端此对象仅驻留服务端内存（<c>WebSessionStore</c>），绝不进入浏览器；
/// 浏览器仅持有不透明的 <c>sb_sess</c> 会话 id。MAUI 端则落本地安全存储 / localStorage。
/// </para>
/// </summary>
public sealed record SessionData(
    string? Token,
    long TenantId,
    long HomeTenantId,
    long UserId,
    string? Username,
    List<string> Permissions,
    List<string> AvailableCultures,
    string? DefaultCulture,
    DateTimeOffset? ExpiresAtUtc = null,
    string? RefreshToken = null,
    DateTimeOffset? AccessTokenExpiresAtUtc = null,
    DateTimeOffset? RefreshTokenExpiresAtUtc = null)
{
    /// <summary>从登录结果构造会话数据（Web 侧签发服务端会话用）。</summary>
    public static SessionData FromLoginResult(AuthResult r)
    {
        return new SessionData(
            Token: r.Token,
            TenantId: r.TenantId,
            HomeTenantId: r.HomeTenantId,
            UserId: r.UserId,
            Username: r.Username,
            Permissions: r.Permissions as List<string> ?? new List<string>(r.Permissions ?? new List<string>()),
            AvailableCultures: r.AvailableCultures as List<string> ?? new List<string> { "zh-CN" },
            DefaultCulture: r.DefaultCulture ?? "zh-CN",
            ExpiresAtUtc: null,
            RefreshToken: r.RefreshToken,
            AccessTokenExpiresAtUtc: r.AccessTokenExpiresAtUtc
                ?? (r.ExpiresInSeconds > 0 ? DateTimeOffset.UtcNow.AddSeconds(r.ExpiresInSeconds) : (DateTimeOffset?)null),
            RefreshTokenExpiresAtUtc: r.RefreshTokenExpiresAtUtc);
    }

    /// <summary>从已登录的 AppState 构造会话数据。</summary>
    public static SessionData FromState(AppState state)
    {
        return new SessionData(
            Token: state.Token,
            TenantId: state.TenantId,
            HomeTenantId: state.HomeTenantId,
            UserId: state.UserId,
            Username: state.Username,
            Permissions: state.Permissions as List<string> ?? new List<string>(state.Permissions),
            AvailableCultures: new List<string>(state.AvailableCultures),
            DefaultCulture: state.DefaultCulture,
            ExpiresAtUtc: null,
            RefreshToken: state.RefreshToken,
            AccessTokenExpiresAtUtc: state.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc: state.RefreshTokenExpiresAtUtc);
    }

    /// <summary>将本数据应用到 AppState（还原路径）。</summary>
    public void ApplyTo(AppState state)
    {
        state.Token = Token;
        state.TenantId = TenantId;
        state.HomeTenantId = HomeTenantId;
        state.UserId = UserId;
        state.Username = Username ?? "";
        state.Permissions = Permissions ?? new List<string>();
        state.AvailableCultures = AvailableCultures ?? new List<string> { "zh-CN" };
        state.DefaultCulture = DefaultCulture ?? "zh-CN";
        state.RefreshToken = RefreshToken;
        state.AccessTokenExpiresAtUtc = AccessTokenExpiresAtUtc;
        state.RefreshTokenExpiresAtUtc = RefreshTokenExpiresAtUtc;
    }
}
