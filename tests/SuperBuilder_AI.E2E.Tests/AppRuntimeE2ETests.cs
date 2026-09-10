using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace SuperBuilder_AI.E2E;

/// <summary>
/// M7-11 应用闭环端到端验收（P0-D2）：覆盖 C1→C4 业务闭合——
/// 管理员登录 → Ask 生成结果 → 两段式发布（创建草稿 → publish）→ 运行按访问者权限取数。
/// 另含权限收口用例：无 app:publish 的读者无法看到发布按钮。
/// 仅当 SB_E2E_BASE_URL 与对应凭据齐备时执行，否则由 SkippableFact 诚实跳过。
/// </summary>
[Collection("playwright")]
public sealed class AppRuntimeE2ETests
{
    private readonly PlaywrightFixture _fx;
    public AppRuntimeE2ETests(PlaywrightFixture fx) => _fx = fx;

    private static string AskQuestion =>
        Environment.GetEnvironmentVariable("SB_E2E_ASK_QUESTION") ?? "本月各品类销售额 Top 10";

    /// <summary>管理员：Ask → 两段式发布（草稿+发布）→ 运行渲染成功。核心验收路径零跳过。</summary>
    [SkippableFact]
    public async Task Admin_Ask_Publish_TwoStage_Run_Renders()
    {
        E2EConfig.Require(_fx.BaseUrl, E2EConfig.User, E2EConfig.Password, E2EConfig.Tenant);
        var page = await _fx.NewPageAsync();
        // 管理员 = e2eadmin（TenantId=4，e2eapp 租户，含 app:create/app:edit/app:publish），走 API 登录规避限流与表单形态差异
        await LoginHelper.ApiLoginAsync(page, E2EConfig.User!, E2EConfig.Password!, long.Parse(E2EConfig.Tenant!));

        // 1) Ask 提问，等待出现可发布的轮次卡片
        await page.FillAsync("[data-testid=ask-input]", AskQuestion);
        await page.ClickAsync("[data-testid=ask-submit]");
        var box = page.Locator("[data-testid=ask-publish-box]");
        await box.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 60000 });

        // 2) 两段式发布：点击发布，等待阶段进入 Published（创建草稿成功 + publish 成功）
        await page.ClickAsync("[data-testid=ask-publish]");
        var published = page.Locator("[data-testid=ask-publish-box][data-stage=\"Published\"]");
        await published.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 60000 });
        Assert.Equal("Published", await box.First.GetAttributeAsync("data-stage"));

        // 3) 读取草稿编码并运行：运行页必须渲染成功（data-state=ok），而非 409/错误
        var code = await box.First.GetAttributeAsync("data-code");
        Assert.False(string.IsNullOrWhiteSpace(code), "发布后 publish-box 应携带 data-code");
        await page.GotoAsync($"/apps/{Uri.EscapeDataString(code!)}/run");
        var run = page.Locator("[data-testid=app-run]");
        await run.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        var runOk = page.Locator("[data-testid=app-run][data-state=\"ok\"]");
        await runOk.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 60000 });
        Assert.Equal("ok", await run.First.GetAttributeAsync("data-state"));
    }

    /// <summary>权限收口：无 app:publish 的读者即使进入 Ask 结果，也看不到「发布为应用」按钮。</summary>
    [SkippableFact]
    public async Task Reader_Without_AppPublish_Cannot_SeePublishButton()
    {
        E2EConfig.Require(_fx.BaseUrl, E2EConfig.ReaderUser, E2EConfig.ReaderPassword, E2EConfig.ReaderTenant);
        var page = await _fx.NewPageAsync();
        // 读者 = e2ereader（TenantId=4，e2eapp 租户，viewer 角色仅含 app:view）：
        // 必须与管理员共用同一租户/数据源，否则 Ask 无可用数据源 → 0 行 → publish-box 不渲染；
        // 其角色不含 app:publish，用于验证 PermissionGuard 隐藏发布按钮。
        await LoginHelper.ApiLoginAsync(page, E2EConfig.ReaderUser!, E2EConfig.ReaderPassword!, long.Parse(E2EConfig.ReaderTenant!));

        await page.FillAsync("[data-testid=ask-input]", AskQuestion);
        await page.ClickAsync("[data-testid=ask-submit]");
        var box = page.Locator("[data-testid=ask-publish-box]");
        await box.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 60000 });

        // 发布按钮被 PermissionGuard(app:publish) 隐藏，不渲染
        Assert.Equal(0, await page.Locator("[data-testid=ask-publish]").CountAsync());
    }
}
