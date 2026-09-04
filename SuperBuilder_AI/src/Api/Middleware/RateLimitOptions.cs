namespace SuperBuilder_AI.Middleware;

/// <summary>M0-08 限流阈值配置（绑定自配置节 "RateLimit"）。</summary>
public sealed class RateLimitOptions
{
    /// <summary>全局 /api 请求阈值（固定窗口内允许的最大请求数）。</summary>
    public int GlobalLimit { get; set; } = 120;

    /// <summary>全局窗口秒数。</summary>
    public int GlobalWindowSeconds { get; set; } = 60;

    /// <summary>登录路径（/api/auth/login*）阈值，用于防爆破，应显著低于全局阈值。</summary>
    public int LoginLimit { get; set; } = 10;

    /// <summary>登录窗口秒数。</summary>
    public int LoginWindowSeconds { get; set; } = 60;
}
