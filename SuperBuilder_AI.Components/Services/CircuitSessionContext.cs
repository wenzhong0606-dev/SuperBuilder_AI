namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 电路级会话 id 桥接（Phase 1，M8-05 加固；验收 #2 修订）。
///
/// <para>
/// Blazor Server 组件运行在 SignalR 电路内，组件生命周期中 <c>IHttpContextAccessor.HttpContext</c>
/// 通常为 null（电路不处于初始 HTTP 请求上下文），因此无法在组件内直接读取请求 cookie。
/// 解决方案：在 <c>_Host.cshtml</c> 初始 GET 期间（HttpContext 可用）读取 <c>sb_sess</c> 会话 id，
/// 由 <c>App.razor</c> 经 Blazor 组件参数（<c>param-SessionId</c>）服务端注入本 Scoped 上下文，
/// 存入后供 <see cref="WebAuthPersistence"/> 在还原 / 保存时定位服务端会话。
/// 会话 id 不进入任何 <c>window</c> 全局变量，浏览器端 JavaScript 永远接触不到（消除 XSS 暴露通道）。
/// </para>
///
/// <para>MAUI Hybrid 无 httpOnly cookie 概念，<see cref="MauiAuthPersistence"/> 不使用本桥接
/// （CircuitSessionContext.SessionId 保持 null，无副作用）。</para>
/// </summary>
public sealed class CircuitSessionContext
{
    /// <summary>当前电路对应的服务端会话 id（来自 httpOnly cookie 的脱敏副本，仅电路内使用）。未登录为 null。</summary>
    public string? SessionId { get; set; }
}
