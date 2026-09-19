namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 访问令牌临近过期时的静默续期协调器（验收 #4）。
///
/// <para>
/// Web 端实现 <see cref="AuthRefreshCoordinator"/>：在每次 API 请求发出前由 <see cref="ApiClientBase"/> 调用，
/// 若当前访问令牌临近过期（<see cref="AppState.AccessTokenExpiresAtUtc"/> 落入阈值窗口），则原子地
/// 用 <see cref="AppState.RefreshToken"/> 换取新令牌并写回服务端会话（<see cref="IAuthPersistence"/>）。
/// 单飞（per-session SemaphoreSlim）保证并发请求不会重复刷新。
/// </para>
///
/// <para>返回语义：<c>true</c> 表示令牌可用（含已刷新）；<c>false</c> 表示刷新失败（应由后续 API 401 触发登出），调用方应照常继续本次请求。</para>
/// </summary>
public interface IAuthRefreshCoordinator
{
	/// <summary>确保当前访问令牌未临近过期；临近过期则原子刷新并写回服务端会话。</summary>
	Task<bool> EnsureFreshTokenAsync(CancellationToken ct = default);
}
