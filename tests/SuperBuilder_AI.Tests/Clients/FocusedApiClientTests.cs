using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Components.Models;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests.Clients;

/// <summary>
/// M9-01：七个聚焦域客户端各自独立测试（仅依赖测试替身，不触达真实 API）。
/// 证明拆分后 Identity/Admin/BI/App/Dashboard/Agent/DataSource 均可被单独验证，互不牵连。
/// </summary>
public class FocusedApiClientTests
{
    [Theory]
    [InlineData(true, "api/localization/admin/languages")]
    [InlineData(false, "api/localization/languages")]
    public async Task Language_catalog_uses_permission_scope_and_preserves_failure(bool manage, string endpoint)
    {
        var handler = new StubHttpMessageHandler(_ => HttpTestDoubles.JsonResponse(
            HttpStatusCode.Forbidden, "{\"code\":\"SB_FORBIDDEN\",\"message\":\"access denied\"}"));
        var client = HttpTestDoubles.BuildFocused<AdminApiClient>(handler, out var state);
        state.Permissions = manage ? new List<string> { "localization:manage" } : new List<string>();
        var (result, error) = await client.GetAdminLanguagesAsync();
        Assert.Null(result);
        Assert.Equal("access denied", error);
        Assert.EndsWith(endpoint, handler.CapturedRequest!.RequestUri!.ToString());
    }
    #region Identity
    [Fact]
    public async Task Identity_LoginAsync_200_Returns_AuthResult()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.OK,
                "{\"token\":\"t1\",\"tenantId\":7,\"userId\":1,\"username\":\"u\"}"));
        var client = HttpTestDoubles.BuildFocused<IdentityApiClient>(handler, out _);

        var (result, error, _) = await client.LoginAsync("u", 7, "p");

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("t1", result!.Token);
        Assert.Equal(7, result.TenantId);
        Assert.EndsWith("api/auth/login", handler.CapturedRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Identity_LoginAsync_401_Returns_Error()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.Unauthorized, "{\"code\":\"AUTH\",\"message\":\"无效凭据\"}"));
        var client = HttpTestDoubles.BuildFocused<IdentityApiClient>(handler, out _);

        var (result, error, _) = await client.LoginAsync("u", 7, "bad");

        Assert.Null(result);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Identity_ResolveTenantByCodeAsync_EmptyCode_Returns_Error()
    {
        var handler = HttpTestDoubles.RespondWith(HttpTestDoubles.StatusResponse(HttpStatusCode.OK));
        var client = HttpTestDoubles.BuildFocused<IdentityApiClient>(handler, out _);

        var (id, code, name, error) = await client.ResolveTenantByCodeAsync("  ");

        Assert.Equal(0L, id);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Identity_ResolveTenantByCodeAsync_Object_Parses_Fields()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"id\":42,\"tenantCode\":\"demo\",\"name\":\"Demo\"}"));
        var client = HttpTestDoubles.BuildFocused<IdentityApiClient>(handler, out _);

        var (id, code, name, error) = await client.ResolveTenantByCodeAsync("demo");

        Assert.Equal(42L, id);
        Assert.Equal("demo", code);
        Assert.Equal("Demo", name);
        Assert.Null(error);
    }

    [Fact]
    public async Task Identity_SetUserLanguageAsync_Returns_EffectiveCulture()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"culture\":\"fr-FR\"}"));
        var client = HttpTestDoubles.BuildFocused<IdentityApiClient>(handler, out _);

        var (culture, error, _) = await client.SetUserLanguageAsync("fr-FR");

        Assert.Equal("fr-FR", culture);
        Assert.Null(error);
    }

    [Fact]
    public async Task Identity_401_Clears_Session_And_Notifies()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.StatusResponse(HttpStatusCode.Unauthorized));
        var client = HttpTestDoubles.BuildFocused<IdentityApiClient>(handler, out var app);
        app.Token = "existing";
        var expiredRaised = false;
        app.SessionExpired += (_) => expiredRaised = true;

        await client.SwitchTenantAsync(7);

        Assert.False(app.IsAuthenticated);
        Assert.True(expiredRaised);
    }

    /// <summary>
    /// 回归（硬加载/F5 被登出）：请求在**会话自举完成前**发出（未携带令牌），
    /// 其 401 在自举完成之后才被处理时，**不得**被误判为"已登录会话过期"。
    /// 旧逻辑只看 <c>AppState.IsAuthenticated</c>，会清掉刚还原成功的有效会话并强制跳登录页。
    /// </summary>
    [Fact]
    public async Task Tokenless_Request_401_Does_Not_Expire_Concurrently_Restored_Session()
    {
        AppState? appRef = null;
        var handler = new StubHttpMessageHandler(_ =>
        {
            // 精确复现竞态：请求发出时无令牌；响应返回前，MainLayout 的自举已完成并写入令牌。
            appRef!.Token = "restored-by-bootstrap";
            return HttpTestDoubles.StatusResponse(HttpStatusCode.Unauthorized);
        });
        var client = HttpTestDoubles.BuildFocused<IdentityApiClient>(handler, out var app);
        appRef = app;
        var expiredRaised = false;
        app.SessionExpired += (_) => expiredRaised = true;

        await client.SwitchTenantAsync(7);

        Assert.True(app.IsAuthenticated);   // 有效会话未被误清
        Assert.False(expiredRaised);        // 未误报会话过期（不会跳登录页）
    }

    /// <summary>
    /// 反向保障：携带令牌的 401 仍然必须视为真实会话过期（不可因上面的修复而漏回收）。
    /// 本用例走<b>基类</b>的 <c>GetJsonAsync</c> 路径（上面那条走派生客户端的原生 HttpClient 路径），两条路径都要覆盖。
    /// </summary>
    [Fact]
    public async Task Token_Carrying_401_On_Base_GetJson_Path_Still_Expires_Session()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.StatusResponse(HttpStatusCode.Unauthorized));
        var client = HttpTestDoubles.BuildFocused<AdminApiClient>(handler, out var app);
        app.Token = "expired-token";
        var expiredRaised = false;
        app.SessionExpired += (_) => expiredRaised = true;

        var (result, _) = await client.GetAdminLanguagesAsync();

        Assert.Null(result);
        Assert.True(expiredRaised);
    }

    /// <summary>
    /// 验收 #5：携带令牌的 401 必须携带 userId 通知 SessionExpired，供宿主（Web）吊销该用户全部服务端会话（改密/停用即时强踢）。
    /// </summary>
    [Fact]
    public async Task Token_Carrying_401_Carries_UserId_To_SessionExpired()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.StatusResponse(HttpStatusCode.Unauthorized));
        var client = HttpTestDoubles.BuildFocused<AdminApiClient>(handler, out var app);
        app.Token = "expired-token";
        app.UserId = 7;
        long? capturedUserId = null;
        app.SessionExpired += (long id) => capturedUserId = id;

        var (result, _) = await client.GetAdminLanguagesAsync();

        Assert.Null(result);
        Assert.True(capturedUserId.HasValue);
        Assert.Equal(7, capturedUserId.Value);
    }
    #endregion

    #region Admin
    [Fact]
    public async Task Admin_GetAdminLanguagesAsync_Returns_List()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "[{\"id\":1,\"culture\":\"zh-CN\",\"displayName\":\"中文\"}]"));
        var client = HttpTestDoubles.BuildFocused<AdminApiClient>(handler, out _);

        var (result, error) = await client.GetAdminLanguagesAsync();

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal("zh-CN", result![0].Culture);
    }

    [Fact]
    public async Task Admin_CreateLanguageAsync_200_Ok()
    {
        var handler = new StubHttpMessageHandler(_ => HttpTestDoubles.StatusResponse(HttpStatusCode.OK));
        var client = HttpTestDoubles.BuildFocused<AdminApiClient>(handler, out _);

        var (ok, error) = await client.CreateLanguageAsync(new AdminLanguageCreate { Culture = "en-US", DisplayName = "English" });

        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public async Task Admin_CreateLanguageAsync_400_False_With_Error()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.BadRequest, "{\"message\":\"文化码重复\"}"));
        var client = HttpTestDoubles.BuildFocused<AdminApiClient>(handler, out _);

        var (ok, error) = await client.CreateLanguageAsync(new AdminLanguageCreate { Culture = "zh-CN" });

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Admin_GetDemoDataPlanAsync_Parses_Plan()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.OK,
                "{\"alreadyInstalled\":false,\"demoTenantCode\":\"demo\",\"items\":[{\"entityType\":\"User\",\"count\":3}]}"));
        var client = HttpTestDoubles.BuildFocused<AdminApiClient>(handler, out _);

        var (result, error) = await client.GetDemoDataPlanAsync();

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.False(result!.AlreadyInstalled);
        Assert.Equal("demo", result.DemoTenantCode);
        Assert.Single(result.Items!);
    }
    #endregion

    #region BI
    [Fact]
    public async Task Bi_AskAsync_200_Returns_Response()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"conversationId\":\"c1\",\"question\":\"q\",\"sql\":\"SELECT 1\"}"));
        var client = HttpTestDoubles.BuildFocused<BiApiClient>(handler, out _);

        var outcome = await client.AskAsync("q", 1);

        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Response);
        Assert.Equal("c1", outcome.Response!.ConversationId);
    }

    [Fact]
    public async Task Bi_AskAsync_400_Parses_ApiError()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.BadRequest, "{\"code\":\"BAD_Q\",\"message\":\"问题无效\"}"));
        var client = HttpTestDoubles.BuildFocused<BiApiClient>(handler, out _);

        var outcome = await client.AskAsync("q", 1);

        Assert.Null(outcome.Response);
        Assert.Equal("BAD_Q", outcome.Code);
        Assert.NotNull(outcome.Error);
    }

    [Fact]
    public async Task Bi_AskRawAsync_200_Returns_Raw()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"raw\":\"ok\"}"));
        var client = HttpTestDoubles.BuildFocused<BiApiClient>(handler, out _);

        var raw = await client.AskRawAsync("q", 1);

        Assert.Equal("{\"raw\":\"ok\"}", raw);
    }

    [Fact]
    public async Task Bi_AskRawAsync_401_Prefixes_Error()
    {
        var handler = new StubHttpMessageHandler(_ => HttpTestDoubles.StatusResponse(HttpStatusCode.Unauthorized));
        var client = HttpTestDoubles.BuildFocused<BiApiClient>(handler, out _);

        var raw = await client.AskRawAsync("q", 1);

        Assert.StartsWith("ERROR 401", raw);
    }

    [Fact]
    public async Task Bi_RefineAsync_200_Returns_Response()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"conversationId\":\"r1\"}"));
        var client = HttpTestDoubles.BuildFocused<BiApiClient>(handler, out _);

        var outcome = await client.RefineAsync("q", "更精确", new[] { RefineTurn.User("q") }, 1);

        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Response);
        Assert.Equal("r1", outcome.Response!.ConversationId);
    }
    #endregion

    #region Dashboard (read primitives)
    [Fact]
    public async Task Dashboard_GetAsync_Typed_Deserializes()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "[1,2,3]"));
        var client = HttpTestDoubles.BuildFocused<DashboardApiClient>(handler, out _);

        var list = await client.GetAsync<List<int>>("x");

        Assert.NotNull(list);
        Assert.Equal(3, list!.Count);
    }

    [Fact]
    public async Task Dashboard_GetAsync_Failure_Returns_Null()
    {
        var handler = new StubHttpMessageHandler(_ => HttpTestDoubles.StatusResponse(HttpStatusCode.InternalServerError));
        var client = HttpTestDoubles.BuildFocused<DashboardApiClient>(handler, out _);

        var list = await client.GetAsync<List<int>>("x");

        Assert.Null(list);
    }

    [Fact]
    public async Task Dashboard_GetJsonAsync_200_Returns_Element()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"a\":1}"));
        var client = HttpTestDoubles.BuildFocused<DashboardApiClient>(handler, out _);

        var (data, status, error, _) = await client.GetJsonAsync("x");

        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.NotNull(data);
    }

    [Fact]
    public async Task Dashboard_GetJsonAsync_404_Returns_Status()
    {
        var handler = new StubHttpMessageHandler(_ => HttpTestDoubles.StatusResponse(HttpStatusCode.NotFound));
        var client = HttpTestDoubles.BuildFocused<DashboardApiClient>(handler, out _);

        var (data, status, error, code) = await client.GetJsonAsync("x");

        Assert.Equal(404, status);
        Assert.NotNull(error);
        Assert.Null(data);
    }
    #endregion

    #region Agent (write primitives)
    [Fact]
    public async Task Agent_PostAsync_200_Ok()
    {
        var handler = new StubHttpMessageHandler(_ => HttpTestDoubles.StatusResponse(HttpStatusCode.OK));
        var client = HttpTestDoubles.BuildFocused<AgentApiClient>(handler, out _);

        var (ok, status, error, _) = await client.PostAsync("x", new { a = 1 });

        Assert.True(ok);
        Assert.Equal(200, status);
    }

    [Fact]
    public async Task Agent_SendAsync_Delete_NoBody_Ok()
    {
        var handler = new StubHttpMessageHandler(_ => HttpTestDoubles.StatusResponse(HttpStatusCode.OK));
        var client = HttpTestDoubles.BuildFocused<AgentApiClient>(handler, out _);

        var (ok, status, _, _) = await client.SendAsync(HttpMethod.Delete, "x");

        Assert.True(ok);
        Assert.Equal(200, status);
        Assert.Equal(HttpMethod.Delete, handler.CapturedRequest!.Method);
    }

    [Fact]
    public async Task Agent_SendAsync_500_False_With_Error()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.InternalServerError, "{\"message\":\"boom\"}"));
        var client = HttpTestDoubles.BuildFocused<AgentApiClient>(handler, out _);

        var (ok, status, error, code) = await client.SendAsync(HttpMethod.Post, "x", new { a = 1 });

        Assert.False(ok);
        Assert.Equal(500, status);
        Assert.NotNull(error);
    }
    #endregion

    #region DataSource (M4-05 scan)
    [Fact]
    public async Task DataSource_StartScanAsync_202_Parses_JobId()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse((HttpStatusCode)202, "{\"jobId\":99}"));
        var client = HttpTestDoubles.BuildFocused<DataSourceApiClient>(handler, out _);

        var (ok, status, jobId, error, _) = await client.StartScanAsync(1);

        Assert.True(ok);
        Assert.Equal(202, status);
        Assert.Equal(99L, jobId);
        Assert.Null(error);
    }

    [Fact]
    public async Task DataSource_StartScanAsync_500_False()
    {
        var handler = new StubHttpMessageHandler(_ => HttpTestDoubles.StatusResponse(HttpStatusCode.InternalServerError));
        var client = HttpTestDoubles.BuildFocused<DataSourceApiClient>(handler, out _);

        var (ok, _, jobId, error, _) = await client.StartScanAsync(1);

        Assert.False(ok);
        Assert.Null(jobId);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task DataSource_GetScanJobAsync_Parses_View()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"jobId\":99,\"status\":\"Running\",\"progressPercent\":50}"));
        var client = HttpTestDoubles.BuildFocused<DataSourceApiClient>(handler, out _);

        var (job, status, error, _) = await client.GetScanJobAsync(1, 99);

        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.NotNull(job);
        Assert.Equal("Running", job!.Status);
        Assert.Equal(50, job.ProgressPercent);
    }
    #endregion

    #region App (publish DSL)
    [Fact]
    public async Task App_PublishAppAsync_200_Returns_Code()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"code\":\"APP-1\"}"));
        var client = HttpTestDoubles.BuildFocused<AppApiClient>(handler, out _);

        var (ok, code, error) = await client.PublishAppAsync(7, "{\"dsl\":1}", null);

        Assert.True(ok);
        Assert.Equal("APP-1", code);
        Assert.Null(error);
    }

    [Fact]
    public async Task App_PublishAppAsync_400_False()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.BadRequest, "{\"message\":\"dsl 非法\"}"));
        var client = HttpTestDoubles.BuildFocused<AppApiClient>(handler, out _);

        var (ok, code, error) = await client.PublishAppAsync(7, "bad", null);

        Assert.False(ok);
        Assert.Null(code);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task App_PublishExistingAsync_200_Returns_Version()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.OK, "{\"appId\":1,\"tenantId\":7,\"version\":3,\"publishedAt\":null,\"publishedBy\":null}"));
        var client = HttpTestDoubles.BuildFocused<AppApiClient>(handler, out _);

        var (ok, version, error, status) = await client.PublishExistingAsync(7, "APP-1");

        Assert.True(ok);
        Assert.Equal(3, version);
        Assert.Null(error);
        Assert.Equal(200, status);
    }

    [Fact]
    public async Task App_PublishExistingAsync_400_False_With_Error()
    {
        var handler = new StubHttpMessageHandler(_ =>
            HttpTestDoubles.JsonResponse(HttpStatusCode.BadRequest, "{\"errors\":[\"草稿 DSL 为空，无法发布。\"]}"));
        var client = HttpTestDoubles.BuildFocused<AppApiClient>(handler, out _);

        var (ok, version, error, status) = await client.PublishExistingAsync(7, "APP-1");

        Assert.False(ok);
        Assert.Null(version);
        Assert.NotNull(error);
        Assert.Equal(400, status);
    }
    #endregion
}
