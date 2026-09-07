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

// M8-05：内容安全策略（CSP）所需配置。
// ApiBaseUrl 必须进入 connect-src，否则同源策略下跨源 API 调用会被 CSP 整体拦截（页面将完全不可用）。
// Security:EnableCsp 为熔断开关（默认开启），若某环境出现意外拦截可临时关闭以便排查。
var apiBaseUrl = (builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7086").TrimEnd('/');
var enableCsp = builder.Configuration.GetValue("Security:EnableCsp", true);

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

// M8-05：内容安全策略（CSP）——仅作用于 Blazor Server Web 宿主（MAUI Hybrid 经文件系统 WebView 加载，不经此管线）。
// 基线策略：默认仅信任同源；脚本/样式允许内联（_Host 主题脚本与组件内联 style 需要），
// 连接目标限定同源 + 配置的 API 源；禁止外部框架嵌入（点击劫持）与 <object>/<embed>（历史插件风险）。
// 收紧（移除 unsafe-inline/unsafe-eval、改用 nonce）需配套改造 _Host 与 Blazor 运行时，待浏览器冒烟验证后实施。
if (enableCsp)
{
    app.Use(async (ctx, next) =>
    {
        var csp = "default-src 'self'; " +
                  "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
                  "style-src 'self' 'unsafe-inline'; " +
                  "img-src 'self' data:; " +
                  "font-src 'self' data:; " +
                  "connect-src 'self' " + apiBaseUrl + "; " +
                  "frame-ancestors 'self'; " +
                  "object-src 'none'; " +
                  "base-uri 'self'";
        ctx.Response.Headers["Content-Security-Policy"] = csp;
        await next();
    });
}

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
