using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using SuperBuilder_AI.Components.Services;

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
        builder.Services.AddScoped<IApiClient, ApiClient>();
        builder.Services.AddScoped<AskSessionStore>();
        builder.Services.AddScoped<FileDownloadService>();
        builder.Services.AddHttpClient("SuperBuilderApi", client =>
        {
            // Windows / iOS  simulator 直连本机回环；Android 模拟器回环为 10.0.2.2（S5-4 双端回归）。
#if ANDROID
            client.BaseAddress = new Uri("http://10.0.2.2:5032");
#else
            client.BaseAddress = new Uri("https://localhost:5032");
#endif
        });

        return builder.Build();
    }
}
