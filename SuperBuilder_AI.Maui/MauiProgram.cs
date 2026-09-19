using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using SuperBuilder_AI.Components.Services;
using SuperBuilder_AI.Maui.Services;
using System.IO;
using System.Text.Json;

namespace SuperBuilder_AI.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        // 共享服务（Scoped，MAUI WebView 本地电路）
        builder.Services.AddScoped<AppState>();
        builder.Services.AddScoped<AuthStore>();
        builder.Services.AddScoped<ThemeService>();
        builder.Services.AddScoped<LocalizationService>();
        builder.Services.AddScoped<ToastService>();
        // M9-01：拆分 God ApiClient 后的 DI 注册——7 个聚焦域客户端（可独立测试/替换）+ 向后兼容门面。
        builder.Services.AddScoped<IIdentityApiClient, IdentityApiClient>();
        builder.Services.AddScoped<IAdminApiClient, AdminApiClient>();
        builder.Services.AddScoped<IBiApiClient, BiApiClient>();
        builder.Services.AddScoped<IAppApiClient, AppApiClient>();
        builder.Services.AddScoped<IDashboardApiClient, DashboardApiClient>();
        builder.Services.AddScoped<IAgentApiClient, AgentApiClient>();
        builder.Services.AddScoped<IDataSourceApiClient, DataSourceApiClient>();
        builder.Services.AddScoped<IApiClient, ApiClient>();
        builder.Services.AddScoped<AskSessionStore>();
        builder.Services.AddScoped<FileDownloadService>();
        // Phase 1（M8-05）：MAUI 无 httpOnly cookie 概念，沿用本地存储方案（计划明确豁免）。
        builder.Services.AddScoped<CircuitSessionContext>();
        builder.Services.AddScoped<IAuthPersistence, MauiAuthPersistence>();
        builder.Services.AddScoped<ILoginCompletion, MauiLoginCompletion>();
        builder.Services.AddHttpClient("SuperBuilderApi", client =>
        {
            // 基地址按平台取默认回环；物理设备（独立机器）通过 Resources/Raw/appsettings.json 的
            // 对应 ApiBaseUrl* 键覆盖为宿主/开发机地址——绝不可使用设备自身 localhost（M8-07）。
            string platformKey;
            if (DeviceInfo.Platform == DevicePlatform.Android)
                platformKey = "ApiBaseUrlAndroid";
            else if (DeviceInfo.Platform == DevicePlatform.iOS)
                platformKey = "ApiBaseUrlIos";
            else if (DeviceInfo.Platform == DevicePlatform.MacCatalyst)
                platformKey = "ApiBaseUrlMacCatalyst";
            else
                platformKey = "ApiBaseUrlWindows";
            var overrideUrl = ReadAppSettingsValue(platformKey);
            var baseUrl = overrideUrl ?? (
#if ANDROID
                "http://10.0.2.2:5032"
#else
                // 直接访问 HTTPS 端口，避免重定向过程中 Authorization 头被移除。
                "https://localhost:7086"
#endif
            );
            client.BaseAddress = new Uri(baseUrl);
        });

        return builder.Build();
    }

    /// <summary>
    /// 从应用包内嵌的 appsettings.json 读取配置值（物理设备联调时覆盖 API 基地址）。
    /// 文件缺失或键不存在时返回 null，由调用方回退到平台默认回环。
    /// </summary>
    private static string? ReadAppSettingsValue(string key)
    {
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult();
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }
        catch
        {
            // 配置缺失/不可读：忽略，回退默认。
        }
        return null;
    }
}
