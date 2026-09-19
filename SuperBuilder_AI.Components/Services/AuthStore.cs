using System.Collections.Generic;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 会话态的持久化与生命周期管理（RCL 共享，两个 Head 各自注册为 Scoped）。
///
/// <para>
/// Phase 1 重构（M8-05 加固）：本类不再直接触碰 <c>localStorage</c>，而是委托
/// <see cref="IAuthPersistence"/>。具体存储后端由宿主注入——Web 为服务端内存会话
/// （httpOnly cookie，浏览器不持有令牌），MAUI 为本地安全存储 / localStorage。
/// 这样浏览器端同源 XSS 无法读取令牌：令牌仅驻留服务端内存与 <see cref="AppState"/> 之间流转，
/// <see cref="AppState"/> 为 Blazor Server 服务端 DI 实例，不经 <c>[Parameter]</c> 序列化边界传给浏览器。
/// </para>
///
/// <para>
/// 职责：把 <see cref="AppState"/> 会话快照经 <see cref="IAuthPersistence"/> 持久化（刷新/重开不掉登录），
/// 应用启动时自举还原，并经 <c>GET /api/auth/me</c> 校验令牌仍有效；失效时统一清除。
/// 所有映射异常都静默降级，不影响主流程。
/// </para>
/// </summary>
public sealed class AuthStore
{
    private readonly IAuthPersistence _persistence;
    private readonly AppState _state;
    private readonly IApiClient _api;

    public AuthStore(IAuthPersistence persistence, AppState state, IApiClient api)
    {
        _persistence = persistence;
        _state = state;
        _api = api;
    }

    /// <summary>将当前 AppState 会话快照持久化到注入的 IAuthPersistence 后端。</summary>
    public async Task SaveAsync()
    {
        try
        {
            await _persistence.SaveAsync(SessionData.FromState(_state));
        }
        catch
        {
            // 持久化失败（存储不可用）不阻断主流程，仅不保存。
        }
    }

    /// <summary>从 IAuthPersistence 还原会话到 AppState（不触发网络校验）。</summary>
    public async Task RestoreAsync()
    {
        try
        {
            var data = await _persistence.LoadAsync();
            if (data is null) return;
            data.ApplyTo(_state);
        }
        catch
        {
            // 解析/读取失败时保持未登录
        }
        finally
        {
            // 无论还原成功与否都置位，避免守卫长时间停留在加载态
            _state.MarkSessionRestored();
        }
    }

    /// <summary>清除会话：先清 AppState，再清除持久化后端。</summary>
    public async Task ClearAsync()
    {
        _state.ClearSession();
        try
        {
            await _persistence.ClearAsync();
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>
    /// 校验当前已还原的令牌是否仍有效：调用 <c>GET /api/auth/me</c>，
    /// 只有服务端明确返回 401 时才清除会话。网络中断、TLS 故障或服务端临时异常
    /// 不能证明令牌失效，因此保留本地会话，避免页面刷新时被错误登出。
    /// </summary>
    public async Task<bool> ValidateAsync()
    {
        if (!_state.IsAuthenticated) return false;
        var (_, status, _, _) = await _api.GetJsonAsync("api/auth/me");
        if (status == 401)
        {
            await ClearAsync();
            return false;
        }

		// 200 表示已验证；其他状态属于暂时无法验证，但不能破坏已有登录态。
		return _state.IsAuthenticated;
    }

    /// <summary>登录成功后写入 AppState 并持久化。</summary>
    public async Task SetFromLoginAsync(AuthResult r)
    {
        _state.Token = r.Token;
        _state.TenantId = r.TenantId;
        _state.HomeTenantId = r.HomeTenantId;
        _state.UserId = r.UserId;
        _state.Username = r.Username;
        _state.Permissions = r.Permissions ?? new List<string>();
        _state.AvailableCultures = r.AvailableCultures ?? new List<string> { "zh-CN" };
        _state.DefaultCulture = r.DefaultCulture ?? "zh-CN";
        await SaveAsync();
    }
}
