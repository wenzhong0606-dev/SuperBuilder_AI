using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace SuperBuilder_AI.E2E;

/// <summary>
/// 共享 Playwright 浏览器生命周期（集合夹具）。
/// 读取集成环境地址与开关：
/// <list type="bullet">
///   <item><description>SB_E2E_BASE_URL：被测 Web 应用基地址（默认 http://localhost:5080）。</description></item>
///   <item><description>SB_E2E_IGNORE_HTTPS_ERRORS：自签名证书场景置 true。</description></item>
/// </list>
/// 未配置集成环境时，测试通过 <see cref="E2EConfig.Require"/> 跳过，绝不谎报通过。
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private bool _skip;
    private string? _skipReason;

    public IBrowser? Browser { get; private set; }

    public string BaseUrl { get; } =
        (Environment.GetEnvironmentVariable("SB_E2E_BASE_URL") ?? "http://localhost:5080").TrimEnd('/');

    public bool IgnoreHttpsErrors { get; } =
        string.Equals(Environment.GetEnvironmentVariable("SB_E2E_IGNORE_HTTPS_ERRORS"), "true", StringComparison.OrdinalIgnoreCase);

    public async Task InitializeAsync()
    {
        // 未配置集成环境：不拉起浏览器，后续取页直接跳过，避免"假失败"。
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SB_E2E_BASE_URL")))
        {
            _skip = true;
            _skipReason = "SB_E2E_BASE_URL 未配置（集成环境未就绪），跳过。";
            return;
        }

        try
        {
            _playwright = await Playwright.CreateAsync();
            Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }
        catch (Exception ex)
        {
            // 浏览器不可用（未安装 / 系统依赖缺失 / 驱动不匹配）：降级为跳过，
            // 避免整组测试报红。封堵 M7-11 契约记载缺口："缺 Chromium 时 Chromium.LaunchAsync() 无兜底，夹具初始化失败"。
            _skip = true;
            _skipReason = $"Playwright 浏览器不可用，已降级跳过：{ex.GetType().Name}: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _playwright?.Dispose();
    }

    public async Task<IPage> NewPageAsync(ViewportSize? viewport = null)
    {
        if (_skip || Browser is null)
            Skip.If(true, _skipReason ?? "E2E 集成环境未配置（SB_E2E_BASE_URL 缺失或浏览器不可用），跳过取页。");
        var context = await Browser!.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            ViewportSize = viewport ?? new ViewportSize { Width = 1280, Height = 900 },
            IgnoreHTTPSErrors = IgnoreHttpsErrors
        });
        return await context.NewPageAsync();
    }
}
