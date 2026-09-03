using System.Text.Json;
using Microsoft.JSInterop;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 会话态的持久化与生命周期管理（RCL 共享，两个 Head 各自注册为 Scoped）。
///
/// <para>
/// 职责：把 <see cref="AppState"/> 中的登录令牌持久化到 <c>localStorage</c>（刷新/重开浏览器不掉登录），
/// 应用启动时自举还原，并经 <c>GET /api/auth/me</c> 校验令牌仍有效；失效时统一清除。
/// 所有 JS 互操作都包在 try/catch 内（预渲染阶段无 JS 运行时、WebView 异常等均静默降级），不影响主流程。
/// </para>
/// </summary>
public sealed class AuthStore
{
    private readonly IJSRuntime _js;
    private readonly AppState _state;
    private readonly IApiClient _api;
    private const string StorageKey = "sb_auth_v1";

    public AuthStore(IJSRuntime js, AppState state, IApiClient api)
    {
        _js = js;
        _state = state;
        _api = api;
    }

    /// <summary>将当前 AppState 会话快照写入 localStorage。</summary>
    public async Task SaveAsync()
    {
        try
        {
            var snap = new AuthSnapshot
            {
                Token = _state.Token,
                TenantId = _state.TenantId,
                UserId = _state.UserId,
                Username = _state.Username,
                Permissions = _state.Permissions as System.Collections.Generic.List<string>
                    ?? new System.Collections.Generic.List<string>(_state.Permissions),
                AvailableCultures = new System.Collections.Generic.List<string>(_state.AvailableCultures),
                DefaultCulture = _state.DefaultCulture,
            };
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(snap));
        }
        catch
        {
            // 预渲染/JS 不可用时静默跳过
        }
    }

    /// <summary>从 localStorage 还原会话到 AppState（不触发网络校验）。</summary>
    public async Task RestoreAsync()
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(raw)) return;
            var snap = JsonSerializer.Deserialize<AuthSnapshot>(raw);
            if (snap is null || string.IsNullOrEmpty(snap.Token)) return;

            _state.Token = snap.Token;
            _state.TenantId = snap.TenantId;
            _state.UserId = snap.UserId;
            _state.Username = snap.Username ?? "";
            _state.Permissions = snap.Permissions ?? new System.Collections.Generic.List<string>();
            _state.AvailableCultures = snap.AvailableCultures ?? new System.Collections.Generic.List<string> { "zh-CN" };
            _state.DefaultCulture = snap.DefaultCulture ?? "zh-CN";
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

    /// <summary>清除会话：先清 AppState，再移除 localStorage 记录。</summary>
    public async Task ClearAsync()
    {
        _state.ClearSession();
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>
    /// 校验当前已还原的令牌是否仍有效：调用 <c>GET /api/auth/me</c>，
    /// 非 200（含 401 过期）则清除会话并返回 false。未登录直接返回 false。
    /// </summary>
    public async Task<bool> ValidateAsync()
    {
        if (!_state.IsAuthenticated) return false;
        var (_, status, _) = await _api.GetJsonAsync("api/auth/me");
        if (status != 200)
        {
            await ClearAsync();
            return false;
        }
        return true;
    }

    /// <summary>登录成功后写入 AppState 并持久化。</summary>
    public async Task SetFromLoginAsync(AuthResult r)
    {
        _state.Token = r.Token;
        _state.TenantId = r.TenantId;
        _state.UserId = r.UserId;
        _state.Username = r.Username;
        _state.Permissions = r.Permissions ?? new System.Collections.Generic.List<string>();
        _state.AvailableCultures = r.AvailableCultures ?? new System.Collections.Generic.List<string> { "zh-CN" };
        _state.DefaultCulture = r.DefaultCulture ?? "zh-CN";
        await SaveAsync();
    }

    private sealed class AuthSnapshot
    {
        public string? Token { get; set; }
        public long TenantId { get; set; }
        public long UserId { get; set; }
        public string? Username { get; set; }
        public System.Collections.Generic.List<string>? Permissions { get; set; }
        public System.Collections.Generic.List<string>? AvailableCultures { get; set; }
        public string? DefaultCulture { get; set; }
    }
}
