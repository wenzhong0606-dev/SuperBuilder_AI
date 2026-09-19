using System;
using System.Net.Http;
using System.Text;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Tests.Clients;

/// <summary>
/// M9-01 测试替身：记录请求并返回预设响应，使聚焦客户端可在完全隔离（无真实 API）下独立测试。
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    /// <summary>最近一次被调用时捕获的请求（用于断言 URL/方法/正文）。</summary>
    public HttpRequestMessage? CapturedRequest { get; private set; }

    protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
    {
        CapturedRequest = request;
        return System.Threading.Tasks.Task.FromResult(_responder(request));
    }
}

/// <summary>返回携带固定 handler 的 HttpClient 的工厂替身（满足 <see cref="IHttpClientFactory"/>）。</summary>
public sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
    // 设 BaseAddress，使聚焦客户端使用的相对 URI（如 "api/auth/login"）可被 HttpClient 解析。
    public HttpClient CreateClient(string name) => new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") };
}

/// <summary>
/// 统一测试工具（M9-01）：替身工厂 + 全新 AppState 构造聚焦/门面客户端，并提供预设响应构造器。
/// 所有方法均为静态，测试中以 <c>HttpTestDoubles.xxx</c> 调用。
/// </summary>
public static class HttpTestDoubles
{
    /// <summary>对任意请求返回固定响应。</summary>
    public static StubHttpMessageHandler RespondWith(HttpResponseMessage response)
        => new StubHttpMessageHandler(_ => response);

    /// <summary>构造单个聚焦客户端（共享 handler，独立 AppState）。</summary>
    public static TClient BuildFocused<TClient>(HttpMessageHandler handler, out AppState appState)
        where TClient : class
    {
        var factory = new StubHttpClientFactory(handler);
        var app = new AppState();
        // 注意：聚焦客户端的构造函数第 3 参 IAuthRefreshCoordinator? 是 C# 可选参数，
        // 反射的默认绑定器不会自动补默认值，故此处必须显式传 null（否则 MissingMethodException）。
        var client = (TClient)Activator.CreateInstance(typeof(TClient), factory, app, null)!;
        appState = app;
        return client;
    }

    /// <summary>
    /// 构造向后兼容门面 <see cref="ApiClient"/>：底层 7 个聚焦客户端全部指向同一替身 handler 与同一 AppState，
    /// 用于证明门面把每个 IApiClient 方法正确委派到对应域，且 401 会话回收在任一域均生效。
    /// </summary>
    public static ApiClient BuildFacade(HttpMessageHandler handler, out AppState appState)
    {
        var factory = new StubHttpClientFactory(handler);
        var app = new AppState();
        IIdentityApiClient identity = new IdentityApiClient(factory, app);
        IAdminApiClient admin = new AdminApiClient(factory, app);
        IBiApiClient bi = new BiApiClient(factory, app);
        IAppApiClient appClient = new AppApiClient(factory, app);
        IDashboardApiClient dashboard = new DashboardApiClient(factory, app);
        IAgentApiClient agent = new AgentApiClient(factory, app);
        IDataSourceApiClient dataSource = new DataSourceApiClient(factory, app);
        appState = app;
        return new ApiClient(identity, admin, bi, appClient, dashboard, agent, dataSource);
    }

    /// <summary>构造一个指定状态与 JSON 正文的响应。</summary>
    public static HttpResponseMessage JsonResponse(System.Net.HttpStatusCode status, string json)
        => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    public static HttpResponseMessage StatusResponse(System.Net.HttpStatusCode status)
        => new HttpResponseMessage(status);
}
