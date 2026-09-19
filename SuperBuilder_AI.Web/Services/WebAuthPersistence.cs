using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>
/// Web 端 <see cref="IAuthPersistence"/> 实现（Phase 1）：以服务端内存会话（cookie 会话 id）承载令牌。
///
/// <para>
/// 会话 id 来自 <see cref="CircuitSessionContext.SessionId"/>——该值由 <see cref="AuthGate"/> 在电路建立后
/// 经 JS 读取 <c>window.__sbSessionId</c>（由 <c>_Host.cshtml</c> 初始 GET 期注入）后写入。
/// 组件事件内 <c>IHttpContextAccessor.HttpContext</c> 通常为 null（Blazor Server 电路脱离初始 HTTP 请求上下文），
/// 故不能在此直接读 cookie，必须经由电路级上下文桥接（见 hardening §8.1(j)）。
/// </para>
///
/// <para><see cref="SaveAsync"/> 用于租户切换等已登录场景：更新当前会话数据，不写 cookie。<see cref="ClearAsync"/> 删除服务端会话（cookie 清除由 /auth/session/end 处理）。</para>
/// </summary>
public sealed class WebAuthPersistence : IAuthPersistence
{
    private readonly CircuitSessionContext _ctx;
    private readonly IWebSessionStore _store;

    public WebAuthPersistence(CircuitSessionContext ctx, IWebSessionStore store)
    {
        _ctx = ctx;
        _store = store;
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
}
