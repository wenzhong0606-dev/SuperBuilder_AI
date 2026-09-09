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
        Assert.Contains("正在恢复登录会话", cut.Markup);

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
            Assert.DoesNotContain("正在恢复登录会话", cut.Markup);
            if (authenticated) Assert.Single(cut.FindAll("[data-testid=ask-input]"));
            else Assert.Contains("请先", cut.Markup);
        });
    }

    public class UnavailableApi : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException("Test API unavailable");
    }
}
