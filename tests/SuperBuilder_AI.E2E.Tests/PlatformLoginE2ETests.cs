using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace SuperBuilder_AI.E2E;

/// <summary>
/// 平台专用登录入口 <c>/admin/login</c> 的端到端验收（P0-D2 补充）：
/// 平台管理员经专属入口（绕过业务租户选择器对 platform 的刻意排除）登录后，应直达 <c>/admin/tenants</c> 租户管理页。
/// 仅当 SB_E2E_BASE_URL 与平台管理员凭据齐备时执行，否则由 SkippableFact 诚实跳过。
/// </summary>
[Collection("playwright")]
public sealed class PlatformLoginE2ETests
{
    private readonly PlaywrightFixture _fx;
    public PlatformLoginE2ETests(PlaywrightFixture fx) => _fx = fx;

    /// <summary>平台管理员经 /admin/login 登录 → 跳转 /admin/tenants（证明专属入口打通且平台权限守卫生效）。</summary>
    [SkippableFact]
    public async Task PlatformAdmin_Login_ByDedicatedEntry_ReachesTenantManagement()
    {
        E2EConfig.Require(_fx.BaseUrl, E2EConfig.User, E2EConfig.Password, E2EConfig.Tenant);
        var page = await _fx.NewPageAsync();

        // 平台管理员专属入口：业务登录页刻意排除 platform 租户，故走 /admin/login
        await page.GotoAsync("/admin/login");
        await page.FillAsync("[data-testid=platform-login-username]", E2EConfig.User!);
        await page.FillAsync("[data-testid=platform-login-password]", E2EConfig.Password!);
        await page.ClickAsync("[data-testid=platform-login-submit]");

        // 登录成功应跳转至平台治理区租户管理页
        await page.WaitForURLAsync("**/admin/tenants", new PageWaitForURLOptions { Timeout = 30000 });
        Assert.EndsWith("/admin/tenants", page.Url);
    }
}
