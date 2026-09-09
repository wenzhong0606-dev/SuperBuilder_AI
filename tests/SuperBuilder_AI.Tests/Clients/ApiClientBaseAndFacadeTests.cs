using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests.Clients;

/// <summary>
/// M9-01：共享基类错误解析（多形态统一错误体）+ 向后兼容门面（IApiClient）委派正确性。
/// </summary>
public class ApiClientBaseAndFacadeTests
{
    /// <summary>暴露受保护静态方法以直接单测。</summary>
    private sealed class ProbeClient : ApiClientBase
    {
        public ProbeClient()
            : base(new StubHttpClientFactory(new StubHttpMessageHandler(_ => HttpTestDoubles.StatusResponse(HttpStatusCode.OK))), new AppState())
        {
        }

        public new static (string? Code, string? Message, string? TraceId) ParseApiErrorPublic(string body)
            => ParseApiError(body);
    }

    [Fact]
    public void ParseApiError_Standard_ApiError()
    {
        var (code, msg, trace) = ProbeClient.ParseApiErrorPublic("{\"code\":\"C1\",\"message\":\"m1\",\"traceId\":\"t1\"}");
        Assert.Equal("C1", code);
        Assert.Equal("m1", msg);
        Assert.Equal("t1", trace);
    }

    [Fact]
    public void ParseApiError_Legacy_ErrorShape()
    {
        var (code, msg, trace) = ProbeClient.ParseApiErrorPublic("{\"error\":\"legacy\"}");
        Assert.Null(code);
        Assert.Equal("legacy", msg);
        Assert.Null(trace);
    }

    [Fact]
    public void ParseApiError_ErrorsArray_Joined()
    {
        var (code, msg, trace) = ProbeClient.ParseApiErrorPublic("{\"errors\":[\"a\",\"b\"]}");
        Assert.Null(code);
        Assert.Equal("a；b", msg);
    }

    [Fact]
    public void ParseApiError_PlainText_Fallback()
    {
        var (code, msg, trace) = ProbeClient.ParseApiErrorPublic("just text");
        Assert.Null(code);
        Assert.Equal("just text", msg);
    }

    [Fact]
    public void ParseApiError_Empty_Returns_AllNull()
    {
        var (code, msg, trace) = ProbeClient.ParseApiErrorPublic("");
        Assert.Null(code);
        Assert.Null(msg);
        Assert.Null(trace);
    }

    /// <summary>向后兼容门面：七个域经 IApiClient 调用均正确路由到各自聚焦客户端。</summary>
    [Fact]
    public async Task Facade_AllSevenDomains_Reachable_Through_IApiClient()
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("api/auth/login")) return HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"token\":\"t\"}");
            if (url.Contains("api/apps")) return HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"code\":\"APP-1\"}");
            if (url.Contains("api/localization/admin/languages")) return HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "[{\"id\":1,\"culture\":\"zh-CN\"}]");
            if (url.Contains("api/localization/languages")) return HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "[{\"id\":1,\"culture\":\"zh-CN\"}]");
            if (url.Contains("api/ask")) return HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"conversationId\":\"c1\"}");
            if (url.Contains("api/data-sources/1/metadata/scan")) return HttpTestDoubles.JsonResponse((HttpStatusCode)202, "{\"jobId\":5}");
            if (url.Contains("api/demo-data/preview")) return HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"alreadyInstalled\":true}");
            return HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "[1]");
        });
        IApiClient facade = HttpTestDoubles.BuildFacade(handler, out _);

        var login = await facade.LoginAsync("u", 7, "p");
        Assert.Equal("t", login.Result!.Token);

        var pub = await facade.PublishAppAsync(7, "dsl", null);
        Assert.True(pub.Ok);
        Assert.Equal("APP-1", pub.Code);

        var langs = await facade.GetAdminLanguagesAsync();
        Assert.Single(langs.Result!);

        var ask = await facade.AskAsync("q", 1);
        Assert.Equal("c1", ask.Response!.ConversationId);

        var scan = await facade.StartScanAsync(1);
        Assert.Equal(5L, scan.JobId);

        var plan = await facade.GetDemoDataPlanAsync();
        Assert.True(plan.Result!.AlreadyInstalled);

        var read = await facade.GetAsync<List<int>>("x");
        Assert.Single(read!);

        var write = await facade.PostAsync("x", new { a = 1 });
        Assert.True(write.Ok);

        var demo = await facade.GetDemoDataPlanAsync();
        Assert.NotNull(demo.Result);
    }

    /// <summary>门面将 PublishApp 路由到 AppApiClient 的 api/apps 端点（URI 级证明委派正确）。</summary>
    [Fact]
    public async Task Facade_PublishApp_Routes_To_Apps_Endpoint()
    {
        var handler = new StubHttpMessageHandler(_ => HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"code\":\"X\"}"));
        var facade = HttpTestDoubles.BuildFacade(handler, out _);

        await facade.PublishAppAsync(7, "dsl", null);

        Assert.EndsWith("api/apps", handler.CapturedRequest!.RequestUri!.ToString());
    }

    /// <summary>任一域经门面收到 401，都会触发 AppState 会话回收（零回归：旧 ApiClient 行为保留）。</summary>
    [Fact]
    public async Task Facade_401_Through_AnyDomain_Clears_Session()
    {
        var handler = new StubHttpMessageHandler(_ => HttpTestDoubles.StatusResponse(HttpStatusCode.Unauthorized));
        var facade = HttpTestDoubles.BuildFacade(handler, out var app);
        app.Token = "x";
        var expired = false;
        app.SessionExpired += () => expired = true;

        await facade.SwitchTenantAsync(7);

        Assert.False(app.IsAuthenticated);
        Assert.True(expired);
    }
}
