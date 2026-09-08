using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Components.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 平台管理域客户端实现（M9-01）：语言目录 CRUD/排序/启停、公开语言目录、演示数据预览与安装。
/// 继承 <see cref="ApiClientBase"/> 复用鉴权头/401 回收/错误解析与读/写原语。
/// </summary>
public sealed class AdminApiClient : ApiClientBase, IAdminApiClient
{
    public AdminApiClient(IHttpClientFactory factory, AppState appState) : base(factory, appState) { }

    public async Task<(IReadOnlyList<AdminLanguageView>? Result, string? Error)> GetAdminLanguagesAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await GetAsync<List<AdminLanguageView>>("api/localization/admin/languages", ct);
            if (list is null) return (null, null);
            return (list, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, "无法连接服务，请确认 API 已启动且地址配置正确。" +
                (string.IsNullOrWhiteSpace(ex.Message) ? "" : $"（{ex.Message}）"));
        }
    }

    public async Task<(bool Ok, string? Error)> CreateLanguageAsync(AdminLanguageCreate model, CancellationToken ct = default)
    {
        var (ok, status, error, _) = await PostAsync("api/localization/languages", model, ct);
        if (!ok && status == 401) OnUnauthorized();
        return (ok, error);
    }

    public async Task<(bool Ok, string? Error)> UpdateLanguageAsync(long id, AdminLanguageUpdate model, CancellationToken ct = default)
    {
        var (ok, status, error, _) = await PutAsync($"api/localization/languages/{id}", model, ct);
        if (!ok && status == 401) OnUnauthorized();
        return (ok, error);
    }

    public async Task<(bool Ok, string? Error)> SetLanguageEnabledAsync(long id, bool enabled, CancellationToken ct = default)
    {
        var (ok, status, error, _) = await PostAsync($"api/localization/languages/{id}/enabled", new { enabled }, ct);
        if (!ok && status == 401) OnUnauthorized();
        return (ok, error);
    }

    public async Task<(bool Ok, string? Error)> ReorderLanguagesAsync(IReadOnlyList<long> orderedIds, CancellationToken ct = default)
    {
        var (ok, status, error, _) = await PostAsync("api/localization/languages/reorder", new { orderedIds }, ct);
        if (!ok && status == 401) OnUnauthorized();
        return (ok, error);
    }

    public async Task<(IReadOnlyList<PublicLanguageView>? Result, string? Error)> GetPublicLanguagesAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await GetAsync<List<PublicLanguageView>>("api/localization/public/languages", ct);
            if (list is null) return (null, null);
            return (list, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, "无法连接服务，请确认 API 已启动且地址配置正确。" +
                (string.IsNullOrWhiteSpace(ex.Message) ? "" : $"（{ex.Message}）"));
        }
    }

    public async Task<(DemoInstallPlan? Result, string? Error)> GetDemoDataPlanAsync(CancellationToken ct = default)
    {
        var (data, _, err, _) = await GetJsonAsync("api/demo-data/preview", ct);
        if (data is not { ValueKind: JsonValueKind.Object })
            return (null, err ?? "无法读取演示数据计划。");
        var v = data.Value;
        var items = v.TryGetProperty("items", out var its) && its.ValueKind == JsonValueKind.Array
            ? its.EnumerateArray().Select(x => new DemoPlanItem(
                x.TryGetProperty("entityType", out var et) ? et.GetString() ?? "" : "",
                x.TryGetProperty("count", out var c) && c.TryGetInt32(out var n) ? n : 0,
                x.TryGetProperty("description", out var d) ? d.GetString() : null)).ToList()
            : new List<DemoPlanItem>();
        return (new DemoInstallPlan
        {
            AlreadyInstalled = v.TryGetProperty("alreadyInstalled", out var a) && a.GetBoolean(),
            DemoTenantCode = v.TryGetProperty("demoTenantCode", out var tc) ? tc.GetString() ?? "demo" : "demo",
            DemoTenantName = v.TryGetProperty("demoTenantName", out var tn) ? tn.GetString() ?? "" : "",
            AdminUsername = v.TryGetProperty("adminUsername", out var au) ? au.GetString() ?? "" : "",
            AdminEmail = v.TryGetProperty("adminEmail", out var ae) ? ae.GetString() ?? "" : "",
            Items = items,
        }, null);
    }

    public async Task<(DemoInstallResult? Result, string? Error)> InstallDemoDataAsync(CancellationToken ct = default)
    {
        var client = Factory.CreateClient("SuperBuilderApi");
        try
        {
            var resp = await client.PostAsJsonAsync("api/demo-data/install", new { }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var (_, msg, _) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
                if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
                return (null, msg ?? $"安装失败（{(int)resp.StatusCode}）。");
            }
            var r = await resp.Content.ReadFromJsonAsync<DemoInstallResult>(ct);
            return (r, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, "无法连接服务，请确认 API 已启动且地址配置正确。" +
                (string.IsNullOrWhiteSpace(ex.Message) ? "" : $"（{ex.Message}）"));
        }
    }
}
