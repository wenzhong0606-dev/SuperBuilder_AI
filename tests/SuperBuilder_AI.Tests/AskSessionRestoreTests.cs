using System;
using System.Reflection;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SuperBuilder_AI.Components.Components.Pages.Analysis;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class AskSessionRestoreTests : BunitContext
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Restore_after_first_render_leaves_loading_without_navigation(bool authenticated)
    {
        var state = new AppState();
        Services.AddSingleton(state);
        Services.AddSingleton(new LocalizationService(null!, null!));
        Services.AddSingleton(DispatchProxy.Create<IApiClient, UnavailableApi>());
        Services.AddSingleton(DispatchProxy.Create<IAppApiClient, UnavailableApi>());
        Services.AddSingleton(new AskSessionStore(JSInterop.JSRuntime));
        Services.AddSingleton(new FileDownloadService(JSInterop.JSRuntime));
        Services.AddSingleton(new ToastService());
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<Ask>();
        // T2 行为变更：Ask 现由 AuthGuard 包裹，首帧（会话未恢复）显示「正在校验会话…」而非旧手动文案。
        Assert.Contains("正在校验会话", cut.Markup);

        await cut.InvokeAsync(() =>
        {
            if (authenticated)
            {
                state.Token = "test-token";
                state.TenantId = 1;
                state.UserId = 1;
            }
            state.MarkSessionRestored();
        });

        cut.WaitForAssertion(() =>
        {
            // T2 行为变更：会话恢复后不再显示 AuthGuard 的校验文案。
            Assert.DoesNotContain("正在校验会话", cut.Markup);
            if (authenticated) Assert.Single(cut.FindAll("[data-testid=ask-input]"));
            else Assert.Contains("需要登录", cut.Markup); // AuthGuard 未登录内联提示
        });
    }

    public class UnavailableApi : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException("Test API unavailable");
    }
}
