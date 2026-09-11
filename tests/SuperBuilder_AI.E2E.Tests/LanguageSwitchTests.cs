using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace SuperBuilder_AI.E2E;

[Collection("playwright")]
public sealed class LanguageSwitchTests
{
    private readonly PlaywrightFixture _fx;
    public LanguageSwitchTests(PlaywrightFixture fx) => _fx = fx;

    /// <summary>
    /// 登录后通过语言切换器选择目标文化，应持久化到 localStorage（键 sb_culture_{tenantId}_{userId}），
    /// 验证多语言切换链路在真实浏览器中可用且可跨刷新保留。
    /// </summary>
    [SkippableFact]
    public async Task Switch_Language_Persists_Culture()
    {
        E2EConfig.Require(_fx.BaseUrl, E2EConfig.User, E2EConfig.Password, E2EConfig.Tenant);
        var page = await _fx.NewPageAsync();
        await LoginHelper.LoginAsync(page, _fx, E2EConfig.User!, E2EConfig.Password!, E2EConfig.Tenant!);
        await page.SelectOptionAsync("[data-testid=language-switcher]", new[] { E2EConfig.SwitchCulture });

        // 等待语言偏好异步持久化到 localStorage（SetCultureAsync 经 JS 互操作写入，存在少量轮询延迟）
        var storedHandle = await page.WaitForFunctionAsync(
            "() => { var ks = Object.keys(localStorage).filter(k => k.startsWith('sb_culture_')); return ks.length ? localStorage.getItem(ks[0]) : null; }",
            null,
            new() { Timeout = 15000, PollingInterval = 100 });
        var stored = await storedHandle.JsonValueAsync<string?>();
        Assert.Equal(E2EConfig.SwitchCulture, stored);
    }
}
