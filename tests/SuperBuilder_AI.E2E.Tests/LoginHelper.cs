using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace SuperBuilder_AI.E2E;

/// <summary>
/// 跨测试复用的登录流程：打开登录页 → 填写账户与口令 → 选择或填写租户 → 提交 → 等待进入 /ask。
/// 同时兼容"目录公开（select）"与"目录未公开（手动编码 input）"两种登录形态。
/// <para>
/// <strong>Phase 1 起的重要变更：</strong>浏览器不再持有令牌，会话由服务端 <c>WebSessionStore</c> 承载、
/// 以 <c>sb_sess</c> cookie 间接引用（见 auth-token-hardening 计划）。因此本助手不再向
/// <c>localStorage['sb_auth_v1']</c> 注入快照（该键已不再被 <c>AuthStore</c> 读取），而是驱动真实表单登录，
/// 经由一次性交接码建立服务端会话并写入 HttpOnly cookie —— 等价于真实用户的登录完成态。
/// </para>
/// </summary>
internal static class LoginHelper
{
    public static async Task LoginAsync(IPage page, PlaywrightFixture fx, string user, string password, string tenant)
    {
        await page.GotoAsync("/login");
        await page.FillAsync("[data-testid=login-username]", user);
        await page.FillAsync("[data-testid=login-password]", password);

        // tenant 入参是租户编码（如 e2eapp），而非数字 id：
        // - 目录未公开 → 渲染 [data-testid=login-tenant-code] 输入框，直接填编码；
        // - 目录公开 → 渲染 [data-testid=login-tenant] 下拉框，其 option value 为数字 id，
        //   需先把编码解析成数字 id 再选（否则 SelectOptionAsync 因无匹配值而失败，登录卡在 /login 超时）。
        var codeInput = page.Locator("[data-testid=login-tenant-code]");
        if (await codeInput.CountAsync() > 0)
        {
            await codeInput.FillAsync(tenant);
        }
        else
        {
            long id = await ResolveTenantCodeToIdAsync(page, tenant);
            await page.SelectOptionAsync("[data-testid=login-tenant]", new[] { id.ToString() });
        }

        await page.ClickAsync("[data-testid=login-submit]");
        // 提交后经由一次性交接码 → /auth/session/start 写 cookie → 重定向 /ask；等待最终落到 /ask。
        await page.WaitForURLAsync("**/ask", new PageWaitForURLOptions { Timeout = 30000 });
    }

    /// <summary>
    /// 经后端 <c>GET /api/auth/tenant-by-code?code=</c> 把租户编码解析为数字 id。
    /// 跨域 fetch 在 Development 下被 CORS 放行（浏览器同源到 Web 宿主，再打到 API 绝对地址）。
    /// </summary>
    private static async Task<long> ResolveTenantCodeToIdAsync(IPage page, string tenantCode)
    {
        var apiBase = E2EConfig.ApiUrl.TrimEnd('/');
        var json = await page.EvaluateAsync<string>(
            "async (c) => {" +
            "  const r = await fetch(c.apiBase + '/api/auth/tenant-by-code?code=' + encodeURIComponent(c.code));" +
            "  const t = await r.text();" +
            "  if (!r.ok) throw new Error('tenant-by-code ' + r.status + ' ' + t);" +
            "  return t;" +
            "}",
            new { apiBase, code = tenantCode });
        using var doc = JsonDocument.Parse(json);
        // API 采用 camelCase 序列化，返回 { id, tenantCode, name }。
        return doc.RootElement.GetProperty("id").GetInt64();
    }

    /// <summary>
    /// 经租户编码登录：复用 <see cref="LoginAsync"/> 的真实表单登录（建立服务端会话 + sb_sess cookie）。
    /// 锚定恒定编码（如 e2eapp），避免依赖随库自增的数字租户 id。
    /// <para>跨域 fetch 在 Development 下被 CORS 放行（同 <see cref="ResolveTenantCodeToIdAsync"/>）。</para>
    /// </summary>
    public static async Task ApiLoginByCodeAsync(IPage page, string user, string password, string tenantCode)
    {
        await LoginAsync(page, null!, user, password, tenantCode);
    }
}
