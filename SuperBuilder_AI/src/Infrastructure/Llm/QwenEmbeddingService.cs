using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces;
using System.Collections.Generic;
using System.Threading;

namespace SuperBuilder_AI.Services;

/// <summary>
/// Qwen Embedding 服务。
///
/// 使用阿里云百炼 OpenAI Compatible Embedding API。
/// </summary>
public class QwenEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingOptions _options;

    public QwenEmbeddingService(
        HttpClient httpClient,
        IOptions<EmbeddingOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public int Dimension => _options.Dimensions;

    public string ModelName => _options.Model;

    public async Task<float[]> GenerateAsync(
        string text,
        string textType = "document",
        CancellationToken ct = default)
    {
        var vectors = await GenerateBatchAsync(
            new[] { text },
            textType,
            ct);
        return vectors[0];
    }

    public async Task<IReadOnlyList<float[]>> GenerateBatchAsync(
        IEnumerable<string> texts,
        string textType = "document",
        CancellationToken ct = default)
    {
        var list = texts?.ToList() ?? new List<string>();
        if (list.Count == 0)
            return Array.Empty<float[]>();

        foreach (var t in list)
        {
            if (string.IsNullOrWhiteSpace(t))
                throw new ArgumentException(
                    "Embedding文本不能为空。",
                    nameof(texts));
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("EMB_CFG: API Key 未配置。");
        if (string.IsNullOrWhiteSpace(_options.Endpoint))
            throw new InvalidOperationException("EMB_CFG: Endpoint 未配置。");
        if (string.IsNullOrWhiteSpace(_options.Model))
            throw new InvalidOperationException("EMB_CFG: Model 未配置。");

        // qwen3.7-text-embedding 的 OpenAI Compatible API 支持
        // model/input/dimensions/encoding_format，但 text_type 仅通过
        // DashScope 原生 API/SDK 提供。兼容端点不接收该非兼容字段。
        _ = textType;

        // 分片：单批过大可能触发 Qwen 单批上限或限流（429）。
        // 片内仍是批量请求，保留吞吐；片间避免单次过载。
        var batchSize = _options.BatchSize <= 0 ? 16 : _options.BatchSize;
        var results = new List<float[]>(list.Count);

        for (var start = 0; start < list.Count; start += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var count = Math.Min(batchSize, list.Count - start);
            var slice = list.GetRange(start, count);
            var sliceVectors = await GenerateSliceWithRetryAsync(slice, ct);
            results.AddRange(sliceVectors);
        }

        return results;
    }

    /// <summary>
    /// 对单一切片做带退避重试的 embedding 调用。
    /// 仅对可重试错误（限流 429 / 服务端 5xx / 单请求超时）重试，最多 3 次。
    /// 超时（HttpClient 超时或远端无响应）抛出的 OperationCanceledException
    /// 若非 job 取消则纳入重试，使「被吞的请求」能快速失败而非永久挂起。
    /// </summary>
    private async Task<IReadOnlyList<float[]>> GenerateSliceWithRetryAsync(
        List<string> slice,
        CancellationToken ct = default)
    {
        const int maxAttempts = 3;
        var delayMs = 300;
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await GenerateSliceAsync(slice, ct);
            }
            catch (InvalidOperationException ex)
                when (attempt < maxAttempts
                    && (ex.Message.StartsWith("EMB_HTTP_429")
                        || ex.Message.StartsWith("EMB_HTTP_5")))
            {
                lastError = ex;
                await Task.Delay(delayMs, ct);
                delayMs = Math.Min(delayMs * 2, 5000);
            }
            catch (OperationCanceledException ex)
                when (attempt < maxAttempts && !ct.IsCancellationRequested)
            {
                // 单请求超时（HttpClient 超时 / 远端无响应）但 job 未取消：可重试。
                lastError = ex;
                await Task.Delay(delayMs, ct);
                delayMs = Math.Min(delayMs * 2, 5000);
            }
        }

        var detail = (lastError?.Message?.Length ?? 0) > 50
            ? lastError!.Message[..50]
            : (lastError?.Message ?? "unknown");
        throw new InvalidOperationException($"EMB_RETRY_FAIL: {detail}");
    }

    /// <summary>
    /// 单次切片 embedding 调用。失败时抛出以 EMB_ 短码前缀的
    /// InvalidOperationException，便于上层在 VectorErrorCode 内诊断。
    /// 透传 CancellationToken，使 job 取消能中断挂起的请求。
    /// </summary>
    private async Task<IReadOnlyList<float[]>> GenerateSliceAsync(
        List<string> slice,
        CancellationToken ct = default)
    {
        var requestBody = new
        {
            model = _options.Model,
            input = slice,
            dimensions = _options.Dimensions,
            encoding_format = "float"
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            _options.Endpoint);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var code = (int)response.StatusCode;
            var tag = code == 429
                ? "EMB_HTTP_429"
                : (code >= 500 ? $"EMB_HTTP_{code}" : $"EMB_HTTP_{code}");
            var snippet = responseBody.Length > 48
                ? responseBody[..48]
                : responseBody;
            throw new InvalidOperationException($"{tag}: {snippet}");
        }

        var result = JsonSerializer.Deserialize<QwenEmbeddingResponse>(
            responseBody,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (result?.Data == null || result.Data.Count == 0)
            throw new InvalidOperationException("EMB_EMPTY_RESP");

        if (result.Data.Count != slice.Count)
            throw new InvalidOperationException(
                $"EMB_COUNT_MISMATCH(req={slice.Count},ret={result.Data.Count})");

        var vectors = new List<float[]>(slice.Count);
        for (var i = 0; i < result.Data.Count; i++)
        {
            var embedding = result.Data[i].Embedding;
            if (embedding == null || embedding.Count == 0)
                throw new InvalidOperationException("EMB_NULL_VECTOR");

            if (embedding.Count != _options.Dimensions)
                throw new InvalidOperationException(
                    $"EMB_DIM_MISMATCH(cfg={_options.Dimensions},act={embedding.Count})");

            vectors.Add(embedding.ToArray());
        }

        return vectors;
    }

    private sealed class QwenEmbeddingResponse
    {
        public List<QwenEmbeddingData> Data { get; set; } = new();
    }

    private sealed class QwenEmbeddingData
    {
        public List<float> Embedding { get; set; } = new();
    }
}
