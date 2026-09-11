using System;
using System.Collections.Generic;
using System.Text.Json;
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

    /// <summary>
    /// 经后端 <c>POST /api/auth/login</c> 直接登录并注入会话快照。
    /// <para>
    /// 用于绕开 UI 登录表单的形态差异与登录限流：UI 的 <c>login-options</c> / <c>tenant-by-code</c>
    /// 两个 GET 辅助端点都排除 <c>platform</c> 租户，但登录 API 仅按数字 <c>TenantId</c> 校验、不排除 platform；
    /// 且 <c>RateLimit:LoginLimit</c>（默认 10/60s）在 localhost 同 IP 下由多用例共用同一桶，易触发 429。
    /// 注入 <c>sb_auth_v1</c> 快照后导航到 <c>/ask</c>，等价于 UI 登录完成态
    /// （MainLayout 自举还原 → ValidateAsync 调 /api/auth/me 通过）。
    /// </para>
    /// <para>
    /// <strong>为什么用绝对地址：</strong>前端 Web 宿主（默认 5080）只承载 Blazor UI，
    /// <em>不</em>转发 <c>/api/**</c>——Blazor 组件通过注入的 HttpClient 打 API 绝对地址。
    /// 而 Playwright 的 <c>page.evaluate(fetch)</c> 是<em>浏览器</em>发出的同源请求，
    /// 用相对路径 <c>/api/auth/login</c> 会命中 Web 宿主并返回 400。
    /// 因此这里必须显式指向 API 地址（<c>SB_E2E_API_URL</c>，默认 http://localhost:5032）。
    /// </para>
    /// </summary>
    public static async Task ApiLoginAsync(IPage page, string user, string password, long tenantId)
    {
        // 先落到同源（/ 或重定向后的 /login），保证 localStorage 可写
        await page.GotoAsync("/");

        // Web 宿主不转发 /api/**，浏览器侧 fetch 必须打 API 绝对地址
        var apiBase = E2EConfig.ApiUrl.TrimEnd('/');

        // 经后端登录 API 取令牌；platform 租户仅被 UI 入口排除，API 层按数字 TenantId 校验，不排除
        var loginJson = await page.EvaluateAsync<string>(
            "async (c) => {" +
            "  const r = await fetch(c.apiBase + '/api/auth/login', {" +
            "    method: 'POST'," +
            "    headers: { 'Content-Type': 'application/json' }," +
            "    body: JSON.stringify({ Username: c.user, TenantId: c.tenantId, Password: c.password })" +
            "  });" +
            "  const t = await r.text();" +
            "  if (!r.ok) throw new Error('login ' + r.status + ' ' + t);" +
            "  return t;" +
            "}",
            new { apiBase, user, password, tenantId });

        using var doc = JsonDocument.Parse(loginJson);
        var root = doc.RootElement;
        var token = root.GetProperty("token").GetString()
            ?? throw new System.InvalidOperationException("登录响应缺少 token 字段");
        var tid = root.GetProperty("tenantId").GetInt64();
        var uid = root.GetProperty("userId").GetInt64();
        var uname = root.GetProperty("username").GetString() ?? user;
        var perms = root.GetProperty("permissions").EnumerateArray()
            .Select(p => p.GetString() ?? string.Empty).ToList();

        // 必须与前端 AuthStore.AuthSnapshot 的字段（PascalCase）严格一致，否则还原失败
        var snapshot = new Dictionary<string, object?>
        {
            ["Token"] = token,
            ["TenantId"] = tid,
            ["HomeTenantId"] = tid,
            ["UserId"] = uid,
            ["Username"] = uname,
            ["Permissions"] = perms,
            ["AvailableCultures"] = new List<string> { "zh-CN" },
            ["DefaultCulture"] = "zh-CN",
        };
        var json = JsonSerializer.Serialize(snapshot);

        // 注入会话快照，再进入 /ask（MainLayout 自举还原会话）
        await page.EvaluateAsync("snap => localStorage.setItem('sb_auth_v1', snap)", json);
        await page.GotoAsync("/ask");
    }

    /// <summary>
    /// 经租户编码登录：先以匿名 <c>GET /api/auth/tenant-by-code?code=</c> 解析数字租户 id，
    /// 再复用 <see cref="ApiLoginAsync"/>。避免依赖具体数字 id（e2e 种子租户 id 随库自增），
    /// 仅锚定恒定编码 <c>e2eapp</c>（与 <c>E2ESandboxSeedService.E2ETenantCode</c> 一致）。
    /// <para>跨域 fetch 在 Development 下被 <c>P11Cors</c> 放行（同 <see cref="ApiLoginAsync"/> 的登录 fetch）。</para>
    /// </summary>
    public static async Task ApiLoginByCodeAsync(IPage page, string user, string password, string tenantCode)
    {
        await page.GotoAsync("/");
        var apiBase = E2EConfig.ApiUrl.TrimEnd('/');
        var codeJson = await page.EvaluateAsync<string>(
            "async (c) => {" +
            "  const r = await fetch(c.apiBase + '/api/auth/tenant-by-code?code=' + encodeURIComponent(c.code));" +
            "  const t = await r.text();" +
            "  if (!r.ok) throw new Error('tenant-by-code ' + r.status + ' ' + t);" +
            "  return t;" +
            "}",
            new { apiBase, code = tenantCode });
        using var doc = JsonDocument.Parse(codeJson);
        var id = doc.RootElement.GetProperty("Id").GetInt64();
        await ApiLoginAsync(page, user, password, id);
    }
}
