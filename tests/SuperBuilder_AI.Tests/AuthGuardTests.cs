using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SuperBuilder_AI.Components.Components.Shared.Guard;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>S5-5 · AuthGuard 登录态分支单测（bUnit）。</summary>
public class AuthGuardTests : BunitContext
{
    /// <summary>bUnit 需要一个 NavigationManager 实现；本测试不触发导航。</summary>
    private sealed class FakeNav : NavigationManager
    {
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private IRenderedComponent<AuthGuard> Render(AppState state, bool autoRedirect = false)
    {
        Services.AddSingleton(state);
        Services.AddSingleton<NavigationManager>(new FakeNav());
        return Render<AuthGuard>(p => p
            .Add(x => x.AutoRedirect, autoRedirect)
            .AddChildContent("机密内容"));
    }

    [Fact]
    public void Not_Restored_Shows_Loading()
    {
        var state = new AppState();
        var cut = Render(state);
        Assert.Contains("正在校验会话", cut.Markup);
        Assert.DoesNotContain("机密内容", cut.Markup);
    }

    [Fact]
    public void Restored_And_Authenticated_Shows_Child()
    {
        var state = new AppState();
        state.Token = "valid-token";
        state.MarkSessionRestored();

        var cut = Render(state);
        Assert.Contains("机密内容", cut.Markup);
    }

    [Fact]
    public void Restored_But_Unauthenticated_Shows_Login_Prompt()
    {
        var state = new AppState();
        state.MarkSessionRestored(); // 无 token

        var cut = Render(state);
        Assert.Contains("需要登录", cut.Markup);
        Assert.DoesNotContain("机密内容", cut.Markup);
    }
}
