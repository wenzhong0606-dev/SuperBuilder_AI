using System.Threading.Tasks;
using Microsoft.Playwright;

namespace SuperBuilder_AI.E2E;

/// <summary>
/// 跨测试复用的登录流程：打开登录页 → 填写账户与口令 → 选择或填写租户 → 提交 → 等待进入 /ask。
/// 同时兼容"目录公开（select）"与"目录未公开（手动编码 input）"两种登录形态。
/// </summary>
internal static class LoginHelper
{
    public static async Task LoginAsync(IPage page, PlaywrightFixture fx, string user, string password, string tenant)
    {
        await page.GotoAsync("/login");
        await page.FillAsync("[data-testid=login-username]", user);
        await page.FillAsync("[data-testid=login-password]", password);

        var codeInput = page.Locator("[data-testid=login-tenant-code]");
        if (await codeInput.CountAsync() > 0)
            await codeInput.FillAsync(tenant);
        else
            await page.SelectOptionAsync("[data-testid=login-tenant]", new[] { tenant });

        await page.ClickAsync("[data-testid=login-submit]");
        await page.WaitForURLAsync("**/ask", new PageWaitForURLOptions { Timeout = 20000 });
    }
}
