namespace SuperBuilder_AI.Components.Services;

/// <summary>跨页面共享的会话态：登录令牌、租户、用户。两个 Head 各自注册为 Scoped。</summary>
public sealed class AppState
{
    public string? Token { get; set; }
    public long TenantId { get; set; }
    /// <summary>M2-05：用户归属的主租户（令牌 htid）。切换后 TenantId=生效租户、HomeTenantId=主租户。</summary>
    public long HomeTenantId { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = "";
    private System.Collections.Generic.IReadOnlyList<string> _permissions = System.Array.Empty<string>();
    public event Action? PermissionsChanged;
    public System.Collections.Generic.IReadOnlyList<string> Permissions
    {
        get => _permissions;
        set { _permissions = value ?? System.Array.Empty<string>(); PermissionsChanged?.Invoke(); }
    }
    public System.Collections.Generic.IReadOnlyList<string> AvailableCultures { get; set; } = new[] { "zh-CN" };
    public string DefaultCulture { get; set; } = "zh-CN";

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    /// <summary>
    /// 会话自举是否已完成（localStorage 还原结束）。
    /// 页面级守卫必须等待该标志为 true 后再判定登录态，否则预渲染/首帧会误判为未登录。
    /// </summary>
    public bool SessionRestored { get; private set; }

    public event Action? SessionRestoredChanged;

    /// <summary>会话失效（如 token 过期/被服务端拒绝）时由 ApiClient 触发，供壳层回收并跳登录。</summary>
    public event Action? SessionExpired;

    /// <summary>由 <see cref="AuthStore"/> 在还原结束后调用，通知守卫可以判定登录态。</summary>
    public void MarkSessionRestored()
    {
        if (SessionRestored) return;
        SessionRestored = true;
        SessionRestoredChanged?.Invoke();
    }

    /// <summary>
    /// 等待会话自举完成（localStorage 还原进内存）。用于消除首帧数据加载与自举的竞态：
    /// 在令牌可用之后再发起首个带鉴权的请求，避免 F5 后出现"未授权"伪错误。
    /// 若已还原则立即返回；否则订阅 <see cref="SessionRestoredChanged"/> 一次性完成。
    /// </summary>
    public Task WhenSessionRestoredAsync()
    {
        if (SessionRestored) return Task.CompletedTask;
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        Action? handler = null;
        handler = () =>
        {
            SessionRestoredChanged -= handler;
            tcs.TrySetResult(true);
        };
        SessionRestoredChanged += handler;
        return tcs.Task;
    }

    /// <summary>清空本地会话态（不触碰持久化存储；存储清理由 <see cref="AuthStore"/> 负责）。
    /// 注意：不重置 <see cref="SessionRestored"/>——该标志仅表示"本电路生命周期内是否已检查过 localStorage 自举"，
    /// 一旦置位应保持 true，否则 F5 后 ValidateAsync 触发 401→ClearAsync 会把它复位为 false，
    /// 使页面永久退回"恢复中"分支（OnAfterRenderAsync(firstRender) 已消费、不会再跑）。登出后由
    /// <see cref="IsAuthenticated"/> 驱动 UI 显示登录提示，而非卡在加载态。</summary>
    public void ClearSession()
    {
        Token = null;
        TenantId = 0;
        HomeTenantId = 0;
        UserId = 0;
        Username = "";
        Permissions = System.Array.Empty<string>();
        AvailableCultures = new[] { "zh-CN" };
        DefaultCulture = "zh-CN";
    }

    /// <summary>通知监听方会话已失效。</summary>
    public void NotifySessionExpired() => SessionExpired?.Invoke();
}
