using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace SuperBuilder_AI.E2E;

[Collection("playwright")]
public sealed class VisualBaselineTests
{
    private readonly PlaywrightFixture _fx;
    public VisualBaselineTests(PlaywrightFixture fx) => _fx = fx;

    /// <summary>
    /// 在四个断点（桌面 1280 / 平板 991 / 小屏 560 / 移动 375）对登录页截图，生成视觉回归基线。
    /// 产物写入工程下 artifacts/visual-baselines/（已被仓库 .gitignore 忽略），供 CI 像素比对。
    /// 未配置集成环境时跳过。
    /// </summary>
    [SkippableFact]
    public async Task Capture_Login_Baselines_At_Four_Viewports()
    {
        E2EConfig.Require(_fx.BaseUrl);
        var dir = Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "artifacts", "visual-baselines");
        Directory.CreateDirectory(dir);

        var viewports = new (string Name, int W, int H)[]
        {
            ("desktop", 1280, 900),
            ("tablet", 991, 900),
            ("small", 560, 900),
            ("mobile", 375, 720)
        };

        foreach (var v in viewports)
        {
            var page = await _fx.NewPageAsync(new ViewportSize { Width = v.W, Height = v.H });
            await page.GotoAsync("/login");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.ScreenshotAsync(new() { Path = Path.Combine(dir, $"login-{v.Name}.png") });
        }
    }
}
