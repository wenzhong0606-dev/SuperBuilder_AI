using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>
/// 访问令牌临近过期时的静默续期协调器（验收 #4 的 Web 服务端实现）。
///
/// <para>
/// 在每次 API 请求发出前由 <see cref="ApiClientBase"/> 调用：若当前访问令牌落入临近过期窗口
/// （<see cref="AppState.AccessTokenExpiresAtUtc"/> 距现在 &lt;= <c>Auth:RefreshThresholdSeconds</c>，默认 300s），
/// 则原子地用 <see cref="AppState.RefreshToken"/> 赎回新令牌，写回 <see cref="AppState"/> 与服务端会话
/// （<see cref="IAuthPersistence"/>）。单飞（per-circuit <see cref="SemaphoreSlim"/>）保证并发请求不会重复刷新。
/// </para>
///
/// <para>返回语义：<c>true</c> 表示令牌可用（含已刷新或本就有效）；<c>false</c> 表示刷新不可用
/// （未登录 / 无刷新令牌 / 刷新令牌已过期 / 刷新调用失败），交由后续 API 401 路径统一触发登出。调用方照常继续本次请求。</para>
/// </summary>
public sealed class AuthRefreshCoordinator : IAuthRefreshCoordinator
{
    private readonly AppState _state;
    private readonly IAuthPersistence _persistence;
    private readonly IRefreshTokenCaller _caller;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _threshold;

    public AuthRefreshCoordinator(AppState state, IAuthPersistence persistence, IRefreshTokenCaller caller, IConfiguration config)
    {
        _state = state;
        _persistence = persistence;
        _caller = caller;
        var secs = config.GetValue("Auth:RefreshThresholdSeconds", 300);
        _threshold = TimeSpan.FromSeconds(secs);
    }

    public async Task<bool> EnsureFreshTokenAsync(CancellationToken ct = default)
    {
        // 未登录或无刷新令牌 → 无可续期
        if (!_state.IsAuthenticated || string.IsNullOrEmpty(_state.RefreshToken))
            return false;

        // 刷新令牌已过期 → 续期必失败，交由后续 401 触发登出（不主动清态，避免误杀有效会话）
        if (_state.RefreshTokenExpiresAtUtc is { } rtExp && rtExp <= DateTimeOffset.UtcNow)
            return false;

        // 访问令牌尚未临近过期 → 无需刷新，令牌可用
        if (_state.AccessTokenExpiresAtUtc is { } exp && exp - DateTimeOffset.UtcNow > _threshold)
            return true;

        // 单飞：同一电路并发请求只真正刷新一次
        await _gate.WaitAsync(ct);
        try
        {
            // 双重检查：进入临界区后可能已被其他并发请求刷新成功
            if (_state.AccessTokenExpiresAtUtc is { } exp2 && exp2 - DateTimeOffset.UtcNow > _threshold)
                return true;

            var result = await _caller.CallRefreshAsync(_state.RefreshToken, ct);
            if (result is null) return false; // 刷新失败（网络/401/复用），交由后续 401 路径触发登出

            // 原子写回：更新令牌与过期时间，并持久化到服务端会话。
            // 刷新令牌已轮换，其绝对过期时间由服务端权威决定，客户端不再臆测 → 置 null（让其依赖服务端 401 判定）。
            _state.Token = result.AccessToken;
            _state.RefreshToken = result.RefreshToken;
            _state.AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresInSeconds);
            _state.RefreshTokenExpiresAtUtc = null;
            await _persistence.SaveAsync(SessionData.FromState(_state));
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
