using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SuperBuilder_AI.Components.Components.Pages.Platform;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

public class DataSourcePreviewUiTests : BunitContext
{
    [Theory]
    [InlineData("Ok")]
    [InlineData("Failed")]
    public void Preview_UsesResponseStatus_AndNeverSaves(string status)
    {
        var api = DispatchProxy.Create<IApiClient, PreviewApi>();
        var proxy = (PreviewApi)(object)api;
        proxy.Status = status;
        var state = new AppState { Token = "test", Permissions = new[] { "metadata:edit" } };
        state.MarkSessionRestored();
        var toast = new ToastService();
        Services.AddSingleton(state);
        Services.AddSingleton(api);
        Services.AddSingleton(toast);
        Services.AddSingleton(new LocalizationService(null!, null!));
        var page = Render<DataSources>();
        page.Find(".page-head button, .btn-primary").Click();
        page.FindAll(".connector-option").First(b => b.TextContent.Contains("SQL Server")).Click();
        page.Find("input[type=password]").Change("Server=example;Password=secret;");
        page.Find(".sb-modal-foot .btn-outline-secondary[disabled], .sb-modal-foot button:nth-child(2)").Click();
        Assert.Equal("api/data-sources/test-connection", proxy.Url);
        Assert.Equal("SQLSERVER", proxy.Body.GetProperty("dbType").GetString());
        Assert.Equal(1, proxy.Posts);
        if (status == "Ok") Assert.Contains(toast.Items, x => x.Tone == "success");
        else
        {
            Assert.DoesNotContain(toast.Items, x => x.Tone == "success");
            Assert.Contains("Timeout", page.Find(".alert-danger").TextContent);
        }
    }

    public class PreviewApi : DispatchProxy
    {
        public string Status = "Ok";
        public string? Url;
        public JsonElement Body;
        public int Posts;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "GetJsonAsync")
                return Task.FromResult<(JsonElement?, int, string?, string?)>((JsonSerializer.SerializeToElement(Array.Empty<object>()), 200, null, null));
            if (method.Name == "PostJsonAsync")
            {
                Posts++;
                Url = (string)args![0]!;
                Body = JsonSerializer.SerializeToElement(args[1]);
                return Task.FromResult<(JsonElement?, int, string?, string?)>((JsonSerializer.SerializeToElement(new { status = Status, errorCode = "Timeout" }), 200, null, null));
            }
            throw new NotSupportedException(method.Name);
        }
    }
}
