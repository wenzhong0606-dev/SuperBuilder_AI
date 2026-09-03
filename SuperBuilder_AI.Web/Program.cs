using SuperBuilder_AI.Components.Services;
using SuperBuilder_AI.Web;

var builder = WebApplication.CreateBuilder(args);

// 经典 Blazor Server：_Host.cshtml 提供 HTML 壳，组件经 SignalR 在服务器端渲染
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// 共享服务（Scoped，每个 SignalR 电路一个实例）
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<AuthStore>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<LocalizationService>();
// 全局轻提示（由 MainLayout 中的 SbToastHost 统一渲染）
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<IApiClient, ApiClient>();
builder.Services.AddScoped<AskSessionStore>();
builder.Services.AddScoped<FileDownloadService>();

// 直接使用 API 的 HTTPS 端口，避免 HTTP -> HTTPS 自动重定向时 Authorization 头被移除。
// 可用 appsettings:ApiBaseUrl 覆盖（例如仅启用 HTTP 的本地环境）。
builder.Services.AddHttpClient("SuperBuilderApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7086");
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
