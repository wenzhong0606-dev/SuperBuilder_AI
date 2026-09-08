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
/// BI 问数域客户端实现（M9-01）：自然语言问数、原始响应与多轮语义细化。
/// 继承 <see cref="ApiClientBase"/> 复用鉴权头/401 回收/错误解析。
/// </summary>
public sealed class BiApiClient : ApiClientBase, IBiApiClient
{
    public BiApiClient(IHttpClientFactory factory, AppState appState) : base(factory, appState) { }

    public async Task<string?> AskRawAsync(string question, long? dataSourceId, CancellationToken ct = default)
    {
        var client = CreateClient();
        var payload = new { question, dataSourceId = dataSourceId ?? 0L };
        var resp = await client.PostAsJsonAsync("api/ask", payload, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
            return "ERROR " + (int)resp.StatusCode + ": " + err;
        }
        return await resp.Content.ReadAsStringAsync(ct);
    }

    /// <summary>类型化问数：反序列化为 <see cref="BIResponse"/>，并对非成功状态解析统一错误码。</summary>
    public async Task<AskOutcome> AskAsync(string question, long? dataSourceId, string? conversationId = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        var resp = await client.PostAsJsonAsync("api/ask", new { question, dataSourceId = dataSourceId ?? 0L, conversationId }, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var (code, msg, trace) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
            return new AskOutcome
            {
                Code = code,
                TraceId = trace,
                Error = msg ?? $"问数失败（{(int)resp.StatusCode}）。"
            };
        }

        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrEmpty(raw))
            return new AskOutcome { Error = "请求失败：空响应。" };

        try
        {
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var response = JsonSerializer.Deserialize<BIResponse>(raw, opt);
            if (response is null)
                return new AskOutcome { Error = "响应解析失败。" };
            return new AskOutcome { Response = response };
        }
        catch (JsonException ex)
        {
            return new AskOutcome { Error = "响应解析失败：" + ex.Message };
        }
    }

    /// <summary>
    /// 多轮语义调整：在已有问题（+ 历史上下文）上追加细化指令，重新走完整 BI 链路。
    /// 仅在用户显式发起「细化」时调用（对应 <c>POST api/ask/refine</c>）；默认问数路径 <c>api/ask</c> 不变。
    /// </summary>
    public async Task<AskOutcome> RefineAsync(
        string? question,
        string instruction,
        IEnumerable<RefineTurn>? history,
        long? dataSourceId,
        CancellationToken ct = default)
    {
        var client = CreateClient();
        var payload = new
        {
            question,
            instruction,
            history = history?.Select(t => new { role = t.Role, content = t.Content }).ToList(),
            dataSourceId = dataSourceId ?? 0L
        };

        var resp = await client.PostAsJsonAsync("api/ask/refine", payload, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var (code, msg, trace) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
            return new AskOutcome
            {
                Code = code,
                TraceId = trace,
                Error = msg ?? $"语义调整失败（{(int)resp.StatusCode}）。"
            };
        }

        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrEmpty(raw))
            return new AskOutcome { Error = "请求失败：空响应。" };

        try
        {
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var response = JsonSerializer.Deserialize<BIResponse>(raw, opt);
            if (response is null)
                return new AskOutcome { Error = "响应解析失败。" };
            return new AskOutcome { Response = response };
        }
        catch (JsonException ex)
        {
            return new AskOutcome { Error = "响应解析失败：" + ex.Message };
        }
    }
}
