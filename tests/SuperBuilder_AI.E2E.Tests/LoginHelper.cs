using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace SuperBuilder_AI.E2E;

/// <summary>
/// 跨测试复用的登录流程：打开登录页 → 填写账户与口令 → 选择或填写租户 → 提交 → 等待进入 /ask。
/// 同时兼容"目录公开（select）"与"目录未公开（手动编码 input）"两种登录形态。
/// <para>
/// <strong>Phase 1 起的重要变更：</strong>浏览器不再持有令牌，会话由服务端 <c>WebSessionStore</c> 承载、
/// 以 <c>sb_sess</c> cookie 间接引用（见 auth-token-hardening 计划）。因此本助手驱动真实表单登录，
/// 经由一次性交接码建立服务端会话并写入 HttpOnly cookie —— 等价于真实用户的登录完成态。
/// </para>
/// <para>
/// <strong>租户 id 解析走服务端：</strong>CI 下 Web 宿主（5080）不转发 <c>/api/**</c>，浏览器侧跨域 fetch
/// 受 CORS 约束；故租户编码→数字 id 的解析复用 <see cref="E2EApiHelper.GetAuthAsync"/> 的服务端
/// <c>HttpClient</c> 路径（与业务 API 驱动断言同源复用同一令牌缓存），避免依赖浏览器跨域 fetch。
/// </para>
/// </summary>
internal static class LoginHelper
{
    public static async Task LoginAsync(IPage page, PlaywrightFixture fx, string user, string password, string tenantCode)
    {
        // 服务端解析租户编码→数字 id（CI 跨域受限，走 HttpClient；同时取回令牌供后续 API 断言复用）。
        var (_, tenantId, _, _) = await E2EApiHelper.GetAuthAsync(user, password, tenantCode);

        await page.GotoAsync("/login");
        await page.FillAsync("[data-testid=login-username]", user);
        await page.FillAsync("[data-testid=login-password]", password);

        // 目录公开 → 渲染 [data-testid=login-tenant] 下拉框（option value 为数字 id）；
        // 目录未公开 → 渲染 [data-testid=login-tenant-code] 输入框（直接填编码）。二者择一。
        var codeInput = page.Locator("[data-testid=login-tenant-code]");
        if (await codeInput.CountAsync() > 0)
        {
            await codeInput.FillAsync(tenantCode);
        }
        else
        {
            await page.SelectOptionAsync("[data-testid=login-tenant]", new[] { tenantId.ToString() });
        }

        await page.ClickAsync("[data-testid=login-submit]");
        // 提交后经由一次性交接码 → /auth/session/start 写 cookie → 重定向 /ask；等待最终落到 /ask。
        await page.WaitForURLAsync("**/ask", new PageWaitForURLOptions { Timeout = 30000 });
    }

    /// <summary>
    /// 经租户编码登录：复用 <see cref="LoginAsync"/> 的真实表单登录（建立服务端会话 + sb_sess cookie）。
    /// 锚定恒定编码（如 e2eapp），避免依赖随库自增的数字租户 id。
    /// </summary>
    public static async Task ApiLoginByCodeAsync(IPage page, string user, string password, string tenantCode)
    {
        await LoginAsync(page, null!, user, password, tenantCode);
    }
}
