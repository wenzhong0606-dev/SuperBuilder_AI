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
    public System.Collections.Generic.IReadOnlyList<string> Permissions { get; set; }
        = System.Array.Empty<string>();
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

    /// <summary>清空本地会话态（不触碰持久化存储；存储清理由 <see cref="AuthStore"/> 负责）。</summary>
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
        SessionRestored = false;
    }

    /// <summary>通知监听方会话已失效。</summary>
    public void NotifySessionExpired() => SessionExpired?.Invoke();
}
