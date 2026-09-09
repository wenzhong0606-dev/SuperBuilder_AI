using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SuperBuilder_AI.Components.Components.Layout;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class NavMenuPermissionTests : BunitContext
{
    [Fact]
    public async Task Menus_follow_permissions_including_empty_and_revocation()
    {
        var state = new AppState { Token = "test" };
        Services.AddSingleton(state);
        Services.AddSingleton(new LocalizationService(null!, null!));
        var cut = Render<NavMenu>();
        Assert.Empty(cut.FindAll("a[href='apps'],a[href='dashboards'],a[href='admin/tenants']"));
        await cut.InvokeAsync(() => state.Permissions = new[] { "app:view" });
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("a[href='apps']")));
        Assert.Single(cut.FindAll("a[href='components']"));
        Assert.Empty(cut.FindAll("a[href='dashboards'],a[href='admin/tenants']"));
        await cut.InvokeAsync(() => state.Permissions = System.Array.Empty<string>());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("a[href='apps'],a[href='components']")));
    }

    [Fact]
    public void Every_business_menu_declares_permission()
    {
        Assert.All(NavMenuItems.Groups.SelectMany(g => g.Items).Where(i => i.Href.Length > 0),
            item => Assert.False(string.IsNullOrWhiteSpace(item.Permission), item.Href));
    }
}
