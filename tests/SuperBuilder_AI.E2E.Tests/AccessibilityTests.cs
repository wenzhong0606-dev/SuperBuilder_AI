using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace SuperBuilder_AI.E2E;

[Collection("playwright")]
public sealed class AccessibilityTests
{
    private readonly PlaywrightFixture _fx;
    public AccessibilityTests(PlaywrightFixture fx) => _fx = fx;

    /// <summary>
    /// 在登录页注入 axe-core 并断言无 critical/serious 级违规（键盘可达、焦点、对比度、ARIA 等）。
    /// axe.min.js 未 vendoring 时自动跳过（见 README）。
    /// </summary>
    [SkippableFact]
    public async Task Login_Page_Has_No_Critical_Axe_Violations()
    {
        E2EConfig.Require(_fx.BaseUrl);
        var axePath = Path.Combine(System.AppContext.BaseDirectory, "axe.min.js");
        Skip.If(!File.Exists(axePath), "axe-core 未 vendoring：将 axe.min.js 置于 E2E 工程根目录后运行（见 README）。");

        var page = await _fx.NewPageAsync();
        await page.GotoAsync("/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.AddScriptTagAsync(new() { Path = axePath });

        var violationsJson = await page.EvaluateAsync<string>(
            "() => new Promise(res => { axe.run(document, { resultTypes: ['violations'] }).then(r => res(JSON.stringify(r.violations.filter(v => v.impact === 'critical' || v.impact === 'serious').map(v => ({ id: v.id, impact: v.impact })))))); })");

        var violations = JsonSerializer.Deserialize<List<AxeViolation>>(violationsJson) ?? new List<AxeViolation>();
        Assert.Empty(violations);
    }

    private sealed record AxeViolation(string Id, string? Impact);
}
