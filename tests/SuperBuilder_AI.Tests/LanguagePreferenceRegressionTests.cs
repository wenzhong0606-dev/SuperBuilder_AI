using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Bunit;
using SuperBuilder_AI.Components.Models;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class LanguagePreferenceRegressionTests : BunitContext
{
    [Fact]
    public async Task Server_preference_equal_to_tenant_default_wins_over_stale_local_cache()
    {
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult("en-US");
        var api = DispatchProxy.Create<IApiClient, LanguageApi>();
        var service = new LocalizationService(JSInterop.JSRuntime, api);
        await service.InitializeAsync(1, 1, new[] { "zh-CN", "en-US" }, "zh-CN");
        Assert.Equal("zh-CN", service.CurrentCulture);
    }

    public class LanguageApi : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "GetUserLanguageAsync" => Task.FromResult<(string?, string?)>(("zh-cn", null)),
            "GetPublicLanguagesAsync" => Task.FromResult<(IReadOnlyList<PublicLanguageView>?, string?)>((Array.Empty<PublicLanguageView>(), null)),
            "GetJsonAsync" => Task.FromResult<(JsonElement?, int, string?, string?)>((null, 200, null, null)),
            _ => throw new NotSupportedException(method.Name)
        };
    }
}
