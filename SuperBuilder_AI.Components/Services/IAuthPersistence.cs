namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 会话持久化抽象（Phase 1，M8-05 加固）。
///
/// <para>
/// 解耦 <see cref="AuthStore"/> 与具体存储后端，使 Web（服务端内存会话 + httpOnly cookie）
/// 与 MAUI（本地安全存储 / localStorage）能够各自实现而不互相影响。存储后端只负责
/// <see cref="SessionData"/> 形态的纯数据存取，不依赖 <see cref="AppState"/>；
/// <see cref="AuthStore"/> 负责在 <see cref="SessionData"/> 与 <see cref="AppState"/> 间映射。
/// </para>
///
/// <para>
/// 契约要点：
/// <list type="bullet">
///   <item><see cref="LoadAsync"/> 在无法还原（无会话 / 已过期 / 存储不可用）时返回 <c>null</c>，
///   不得抛异常——还原失败应等价于「未登录」而非阻断启动。</item>
///   <item><see cref="SaveAsync"/> 仅持久化给定数据，是否写 httpOnly cookie 由 Web 实现在
///   普通 HTTP 端点内完成（Blazor 组件事件不得写 cookie，见 hardening §8.1(j)）。</item>
///   <item><see cref="ClearAsync"/> 清除持久化数据，但不负责清除浏览器 cookie（那必须由
///   登出 HTTP 端点处理）。</item>
/// </list>
/// </para>
/// </summary>
public interface IAuthPersistence
{
    /// <summary>持久化会话数据（登录成功 / 租户切换后调用）。</summary>
    Task SaveAsync(SessionData data);

    /// <summary>
    /// 读取已持久化的会话数据。无会话、已过期或存储不可用时返回 <c>null</c>（不得抛异常）。
    /// </summary>
    Task<SessionData?> LoadAsync();

    /// <summary>清除持久化数据（不清除浏览器 cookie）。</summary>
    Task ClearAsync();
}
