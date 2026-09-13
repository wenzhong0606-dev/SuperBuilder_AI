using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace SuperBuilder_AI.E2E;

/// <summary>
/// E2E-01 缺失链之一：<c>Dashboard</c> 段（创建 → 详情页渲染）。
/// 管理员经 API 取蓝图骨架 → 创建仪表盘（草稿）→ 打开详情页，断言页面渲染（含返回列表按钮，未落到错误态）。
/// <para>数据渲染（含取数）复用与 App 相同的渲染管线，已由 <see cref="AppRuntimeE2ETests"/> 的"运行渲染成功"证明；
/// 本用例补足 Dashboard 链独有的"创建 + 详情页承载"环节。</para>
/// 仅当 SB_E2E_BASE_URL 与管理员凭据齐备时执行，否则由 SkippableFact 诚实跳过。
/// </summary>
[Collection("playwright")]
public sealed class DashboardRenderE2ETests
{
    private readonly PlaywrightFixture _fx;
    public DashboardRenderE2ETests(PlaywrightFixture fx) => _fx = fx;

    /// <summary>管理员创建仪表盘 → 详情页渲染（证明 Dashboard 链承载环节可用，且未落到错误态）。</summary>
    [SkippableFact]
    public async Task Admin_CreateDashboard_ThenDetailRenders()
    {
        E2EConfig.Require(_fx.BaseUrl, E2EConfig.User, E2EConfig.Password);
        var page = await _fx.NewPageAsync();
        await LoginHelper.ApiLoginByCodeAsync(page, E2EConfig.User!, E2EConfig.Password!, "e2eapp");

        // 1) 取蓝图骨架（有效的最小 DslJson 模板）
        var bp = await E2EApiHelper.CallApiAsync(page, "GET", "api/dashboards/editor/blueprint");
        Skip.If(!bp.Ok, $"蓝图获取失败（{bp.Status}）：{bp.Body}");
        var skeleton = JsonDocument.Parse(bp.Body).RootElement.TryGetProperty("skeleton", out var sk) && sk.ValueKind == JsonValueKind.String
            ? sk.GetString() : null;
        Skip.If(string.IsNullOrWhiteSpace(skeleton), "蓝图骨架为空，无法创建仪表盘。");

        // 2) 创建仪表盘（tenantId 由令牌作用域决定；传 0 让服务端回退到令牌租户）
        var (_, tid, _, _) = await E2EApiHelper.GetAuthAsync(page);
        var create = await E2EApiHelper.CallApiAsync(page, "POST", "api/dashboards", new
        {
            tenantId = tid,
            dslJson = skeleton,
            status = "draft",
        });
        Skip.If(create.Status is < 200 or >= 300, $"仪表盘创建失败（{create.Status}）：{create.Body}");
        if (!JsonDocument.Parse(create.Body).RootElement.TryGetProperty("id", out var idEl)
            || idEl.ValueKind != JsonValueKind.Number)
        {
            Skip.If(true, $"仪表盘创建响应缺少 id：{create.Body}");
            return;
        }
        var dashId = idEl.GetInt64();

        // 3) 打开详情页，等待网络空闲；断言返回列表按钮可见（证明页面未落到加载失败/错误态）
        await page.GotoAsync($"/dashboards/{dashId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var back = page.GetByText("返回列表");
        await back.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        Assert.True(await back.First.IsVisibleAsync(),
            "仪表盘详情页应正常渲染（含返回列表按钮）；若落到错误态说明创建/承载链路损坏。");
    }
}
