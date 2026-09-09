using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SuperBuilder_AI.Components.Components.Pages.Analysis;
using SuperBuilder_AI.Components.Models;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class AskPublishRegressionTests : BunitContext
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Publish_uses_snapshot_and_only_publishes_a_successful_draft(bool createOk)
    {
        var api = DispatchProxy.Create<IApiClient, ApiStub>();
        var stub = (ApiStub)(object)api;
        stub.CreateOk = createOk;
        var apps = DispatchProxy.Create<IAppApiClient, AppStub>();
        var appStub = (AppStub)(object)apps;
        Services.AddSingleton(new AppState { Token = "test", TenantId = 1, UserId = 1 });
        Services.AddSingleton(new LocalizationService(null!, null!));
        Services.AddSingleton(api);
        Services.AddSingleton(apps);
        Services.AddSingleton(new AskSessionStore(JSInterop.JSRuntime));
        Services.AddSingleton(new FileDownloadService(JSInterop.JSRuntime));
        Services.AddSingleton(new ToastService());
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<Ask>();
        var turn = new AskTurn { Question = "最近十个入库单", Response = new BIResponse { Success = true, TurnId = "snapshot-1" } };
        var method = typeof(Ask).GetMethod("Publish", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await cut.InvokeAsync(() => (Task)method.Invoke(cut.Instance, new object[] { turn })!);
        Assert.Equal("api/apps/from-ask", stub.Endpoint);
        Assert.Equal("snapshot-1", stub.Body.GetProperty("turnId").GetString());
        Assert.Equal(createOk ? 1 : 0, appStub.PublishCalls);
        Assert.Equal(createOk ? PublishStage.Published : PublishStage.Failed, turn.PublishStage);
    }

    public class ApiStub : DispatchProxy
    {
        public bool CreateOk;
        public string? Endpoint;
        public JsonElement Body;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "PostAsync")
            {
                Endpoint = (string)args![0]!;
                Body = JsonSerializer.SerializeToElement(args[1]);
                return Task.FromResult<(bool, int, string?, string?)>((CreateOk, CreateOk ? 201 : 422, CreateOk ? null : "unsupported", null));
            }
            throw new InvalidOperationException("No other API expected");
        }
    }

    public class AppStub : DispatchProxy
    {
        public int PublishCalls;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != "PublishExistingAsync") throw new InvalidOperationException(method.Name);
            PublishCalls++;
            return Task.FromResult<(bool, int?, string?, int)>((true, 1, null, 200));
        }
    }
}
