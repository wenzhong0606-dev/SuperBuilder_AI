using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace SuperBuilder_AI.E2E;

[Collection("playwright")]
public sealed class PermissionDenyTests
{
    private readonly PlaywrightFixture _fx;
    public PermissionDenyTests(PlaywrightFixture fx) => _fx = fx;

    /// <summary>
    /// 低权限（只读）用户访问租户管理页（/admin/tenants）必须被拒绝，
    /// 重定向到 /forbidden 或展示禁止内容——验证字段授权与跨租户隔离协作生效。
    /// </summary>
    [SkippableFact]
    public async Task Reader_Cannot_Access_Admin_Tenants()
    {
        E2EConfig.Require(_fx.BaseUrl, E2EConfig.ReaderUser, E2EConfig.ReaderPassword, E2EConfig.ReaderTenant);
        var page = await _fx.NewPageAsync();
        await LoginHelper.LoginAsync(page, _fx, E2EConfig.ReaderUser!, E2EConfig.ReaderPassword!, E2EConfig.ReaderTenant!);
        await page.GotoAsync("/admin/tenants");
        await page.WaitForURLAsync("**/forbidden", new PageWaitForURLOptions { Timeout = 20000 });
        Assert.Contains("/forbidden", page.Url);
    }
}
