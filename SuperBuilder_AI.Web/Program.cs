using SuperBuilder_AI.Components.Services;
using SuperBuilder_AI.Web;

var builder = WebApplication.CreateBuilder(args);

// 经典 Blazor Server：_Host.cshtml 提供 HTML 壳，组件经 SignalR 在服务器端渲染
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// 共享服务（Scoped，每个 SignalR 电路一个实例）
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<LocalizationService>();
builder.Services.AddScoped<IApiClient, ApiClient>();

// API 基地址：默认本机 5032（P11.0 起的 API），可用 appsettings:ApiBaseUrl 覆盖
builder.Services.AddHttpClient("SuperBuilderApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:5032");
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
