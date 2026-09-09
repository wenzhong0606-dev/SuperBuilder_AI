using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace SuperBuilder_AI.E2E;

[Collection("playwright")]
public sealed class AuthFlowTests
{
    private readonly PlaywrightFixture _fx;
    public AuthFlowTests(PlaywrightFixture fx) => _fx = fx;

    /// <summary>未登录访问受保护页（/ask）必须重定向到 /login。</summary>
    [SkippableFact]
    public async Task Unauthenticated_Access_Protected_RedirectsToLogin()
    {
        E2EConfig.Require(_fx.BaseUrl);
        var page = await _fx.NewPageAsync();
        await page.GotoAsync("/ask");
        // 鉴权跳转为 Blazor 客户端路由（AuthGuard.NavigateTo，无整页 Load），
        // 不能用 WaitForURL(waitUntil:Load)，改为轮询 location 判定客户端导航完成。
        await page.WaitForFunctionAsync(
            "() => window.location.pathname.endsWith('/login')",
            null,
            new PageWaitForFunctionOptions { Timeout = 20000 });
        Assert.Contains("/login", page.Url);
    }

    /// <summary>有效凭据登录后应进入 /ask 工作台。</summary>
    [SkippableFact]
    public async Task Valid_Login_ReachesAsk()
    {
        E2EConfig.Require(_fx.BaseUrl, E2EConfig.User, E2EConfig.Password, E2EConfig.Tenant);
        var page = await _fx.NewPageAsync();
        await LoginHelper.LoginAsync(page, _fx, E2EConfig.User!, E2EConfig.Password!, E2EConfig.Tenant!);
        Assert.Contains("/ask", page.Url);
    }

    /// <summary>无效凭据登录必须停留在登录页并显示错误提示（role=alert）。</summary>
    [SkippableFact]
    public async Task Invalid_Login_ShowsError()
    {
        E2EConfig.Require(_fx.BaseUrl);
        var page = await _fx.NewPageAsync();
        await page.GotoAsync("/login");
        await page.FillAsync("[data-testid=login-username]", "no-such-user");
        await page.FillAsync("[data-testid=login-password]", "wrong-pass");
        await page.ClickAsync("[data-testid=login-submit]");
        var error = page.Locator("[data-testid=login-error]");
        await error.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await error.IsVisibleAsync());
    }
}
