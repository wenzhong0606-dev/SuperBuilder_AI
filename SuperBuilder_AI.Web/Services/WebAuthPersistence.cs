using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>
/// Web 端 <see cref="IAuthPersistence"/> 实现（Phase 1）：以服务端内存会话（cookie 会话 id）承载令牌。
///
/// <para>
/// 会话 id 来自 <see cref="CircuitSessionContext.SessionId"/>——该值由 <c>App.razor</c> 在初始 HTTP 请求期
/// 经 Blazor 组件参数（<c>param-SessionId</c>）服务端注入电路（<c>_Host.cshtml</c> 读取 httpOnly cookie 后传入），
/// 不再经 JS 读取 <c>window.__sbSessionId</c>，浏览器端 JavaScript 永远接触不到会话 id（验收 #2）。
/// </para>
///
/// <para><see cref="SaveAsync"/> 用于租户切换等已登录场景：更新当前会话数据，不写 cookie。<see cref="ClearAsync"/> 删除服务端会话（cookie 清除由 /auth/session/end 处理）。</para>
/// </summary>
public sealed class WebAuthPersistence : IAuthPersistence
{
    private readonly CircuitSessionContext _ctx;
    private readonly IWebSessionStore _store;
    private readonly AppState _state;

    public WebAuthPersistence(CircuitSessionContext ctx, IWebSessionStore store, AppState state)
    {
        _ctx = ctx;
        _store = store;
        _state = state;
        // 验收 #5：令牌被拒（改密/停用经 SecurityStamp 轮换 → API 401）时，吊销该用户在服务端的所有 Web 会话，
        // 立即强制全电路重新登录（而非仅清当前电路）。userId 由 AppState.NotifySessionExpired 携带，避免被 ClearSession 清零。
        state.SessionExpired += OnSessionExpired;
    }

    public Task<SessionData?> LoadAsync()
    {
        var id = _ctx.SessionId;
        if (id is null) return Task.FromResult<SessionData?>(null);
        return Task.FromResult(_store.Get(id));
    }

    public Task SaveAsync(SessionData data)
    {
        var id = _ctx.SessionId;
        if (id is not null) _store.Set(id, data);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        var id = _ctx.SessionId;
        if (id is not null) _store.Remove(id);
        return Task.CompletedTask;
    }

    private void OnSessionExpired(long userId)
    {
        try
        {
            if (userId != 0) _store.RemoveByUserId(userId);
        }
        catch
        {
            // 吊销失败（存储不可用）不阻断主流程
        }
    }
}
