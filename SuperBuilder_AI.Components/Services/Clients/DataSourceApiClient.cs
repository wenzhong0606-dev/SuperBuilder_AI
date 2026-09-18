using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 数据源/元数据域客户端实现（M9-01，增量内聚）：后台元数据扫描的触发与轮询。
/// 继承 <see cref="ApiClientBase"/> 复用鉴权头/401 回收/错误解析与读原语。
/// </summary>
public sealed class DataSourceApiClient : ApiClientBase, IDataSourceApiClient
{
    public DataSourceApiClient(IHttpClientFactory factory, AppState appState) : base(factory, appState) { }

    /// <summary>M4-05 触发后台扫描：POST 创建任务并入队，从 202 响应体解析 jobId。</summary>
    public async Task<(bool Ok, int Status, long? JobId, string? Error, string? Code)> StartScanAsync(long dataSourceId, CancellationToken ct = default)
    {
        var client = CreateClient();
        var sentWithToken = !string.IsNullOrEmpty(AppState.Token);
        try
        {
            var resp = await client.PostAsJsonAsync($"api/data-sources/{dataSourceId}/metadata/scan", new { }, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized(sentWithToken);
            if (!resp.IsSuccessStatusCode)
            {
                var (code, msg, _) = ParseApiError(body);
                return (false, (int)resp.StatusCode, null, msg ?? $"请求失败（{(int)resp.StatusCode}）。", code);
            }
            long? jobId = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("jobId", out var jp) && jp.ValueKind == JsonValueKind.Number)
                    jobId = jp.GetInt64();
            }
            return (true, (int)resp.StatusCode, jobId, null, null);
        }
        catch (Exception ex)
        {
            return (false, 0, null, "网络错误：" + ex.Message, null);
        }
    }

    /// <summary>M4-05 轮询扫描任务状态；成功解析为 <see cref="ScanJobView"/>。</summary>
    public async Task<(ScanJobView? Job, int Status, string? Error, string? Code)> GetScanJobAsync(long dataSourceId, long jobId, CancellationToken ct = default)
    {
        var (data, status, err, code) = await GetJsonAsync($"api/data-sources/{dataSourceId}/metadata/scan/{jobId}", ct);
        if (data is null) return (null, status, err, code);
        try
        {
            var job = JsonSerializer.Deserialize<ScanJobView>(data.Value.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return (job, status, null, null);
        }
        catch (Exception ex)
        {
            return (null, status, "解析扫描任务失败：" + ex.Message, code);
        }
    }

    /// <summary>C5 获取最新扫描任务（不限终态）。</summary>
    public async Task<(ScanJobView? Job, int Status, string? Error, string? Code)> GetLatestScanJobAsync(long dataSourceId, CancellationToken ct = default)
    {
        var (data, status, err, code) = await GetJsonAsync($"api/data-sources/{dataSourceId}/metadata/scan/latest", ct);
        if (data is null) return (null, status, err, code);
        try
        {
            var job = JsonSerializer.Deserialize<ScanJobView>(data.Value.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return (job, status, null, null);
        }
        catch (Exception ex)
        {
            return (null, status, "解析扫描任务失败：" + ex.Message, code);
        }
    }

    /// <summary>C3 软取消进行中的扫描任务（POST …/scan/{jobId}/cancel）。</summary>
    public async Task<(bool Ok, int Status, string? Error, string? Code)> CancelScanAsync(long dataSourceId, long jobId, CancellationToken ct = default)
    {
        var client = CreateClient();
        var sentWithToken = !string.IsNullOrEmpty(AppState.Token);
        try
        {
            var resp = await client.PostAsync($"api/data-sources/{dataSourceId}/metadata/scan/{jobId}/cancel", null, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized(sentWithToken);
            if (!resp.IsSuccessStatusCode)
            {
                var (code, msg, _) = ParseApiError(body);
                return (false, (int)resp.StatusCode, msg ?? $"请求失败（{(int)resp.StatusCode}）。", code);
            }
            return (true, (int)resp.StatusCode, null, null);
        }
        catch (Exception ex)
        {
            return (false, 0, "网络错误：" + ex.Message, null);
        }
    }

    /// <summary>C7 仅重扫失败项（POST …/scan/{jobId}/retry-failed）。</summary>
    public async Task<(bool Ok, int Status, long? JobId, string? Error, string? Code)> RetryFailedScanAsync(long dataSourceId, long jobId, CancellationToken ct = default)
    {
        var client = CreateClient();
        var sentWithToken = !string.IsNullOrEmpty(AppState.Token);
        try
        {
            var resp = await client.PostAsync($"api/data-sources/{dataSourceId}/metadata/scan/{jobId}/retry-failed", null, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized(sentWithToken);
            if (!resp.IsSuccessStatusCode)
            {
                var (code, msg, _) = ParseApiError(body);
                return (false, (int)resp.StatusCode, null, msg ?? $"请求失败（{(int)resp.StatusCode}）。", code);
            }
            long? newJobId = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("jobId", out var jp) && jp.ValueKind == JsonValueKind.Number)
                    newJobId = jp.GetInt64();
            }
            return (true, (int)resp.StatusCode, newJobId, null, null);
        }
        catch (Exception ex)
        {
            return (false, 0, null, "网络错误：" + ex.Message, null);
        }
    }

    /// <summary>测试数据源连通性（POST …/test-connection，使用已存储连接串）。</summary>
    public async Task<(bool Ok, int Status, string? Error, string? Code)> TestConnectionAsync(long dataSourceId, CancellationToken ct = default)
    {
        var client = CreateClient();
        var sentWithToken = !string.IsNullOrEmpty(AppState.Token);
        try
        {
            var resp = await client.PostAsync($"api/data-sources/{dataSourceId}/test-connection", null, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized(sentWithToken);
            if (!resp.IsSuccessStatusCode)
            {
                var (code, msg, _) = ParseApiError(body);
                return (false, (int)resp.StatusCode, msg ?? $"请求失败（{(int)resp.StatusCode}）。", code);
            }
            return (true, (int)resp.StatusCode, null, null);
        }
        catch (Exception ex)
        {
            return (false, 0, "网络错误：" + ex.Message, null);
        }
    }

    /// <summary>C8 启用/禁用数据源（PATCH …/enable 或 …/disable）。</summary>
    public async Task<(bool Ok, int Status, string? Error, string? Code)> SetDataSourceEnabledAsync(long dataSourceId, bool enabled, CancellationToken ct = default)
    {
        var client = CreateClient();
        var sentWithToken = !string.IsNullOrEmpty(AppState.Token);
        try
        {
            var resp = await client.PatchAsync($"api/data-sources/{dataSourceId}/{(enabled ? "enable" : "disable")}", null, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized(sentWithToken);
            if (!resp.IsSuccessStatusCode)
            {
                var (code, msg, _) = ParseApiError(body);
                return (false, (int)resp.StatusCode, msg ?? $"请求失败（{(int)resp.StatusCode}）。", code);
            }
            return (true, (int)resp.StatusCode, null, null);
        }
        catch (Exception ex)
        {
            return (false, 0, "网络错误：" + ex.Message, null);
        }
    }
}
