using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces;

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
        string textType = "document")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "Embedding文本不能为空。",
                nameof(text));
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Embedding API Key 未配置。");
        }

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw new InvalidOperationException("Embedding Endpoint 未配置。");
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new InvalidOperationException("Embedding Model 未配置。");
        }

        // qwen3.7-text-embedding 的 OpenAI Compatible API 支持
        // model/input/dimensions/encoding_format，但 text_type 仅通过
        // DashScope 原生 API/SDK 提供。此前将 text_type 直接发送到
        // /compatible-mode/v1/embeddings 会导致 Runtime 请求契约失败。
        // 当前保持 IEmbeddingService 的 textType 参数兼容性，但不向
        // OpenAI Compatible Endpoint 发送该非兼容字段。
        _ = textType;

        var requestBody = new
        {
            model = _options.Model,
            input = text,
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

        using var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Qwen Embedding API 调用失败。" +
                $" HTTP={(int)response.StatusCode} " +
                $" Response={responseBody}");
        }

        var result = JsonSerializer.Deserialize<QwenEmbeddingResponse>(
            responseBody,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (result?.Data == null || result.Data.Count == 0)
        {
            throw new InvalidOperationException(
                "Qwen Embedding API 返回结果为空。");
        }

        var embedding = result.Data[0].Embedding;

        if (embedding == null || embedding.Count == 0)
        {
            throw new InvalidOperationException(
                "Qwen Embedding API 未返回有效向量。");
        }

        if (embedding.Count != _options.Dimensions)
        {
            throw new InvalidOperationException(
                $"Embedding维度不匹配。" +
                $"配置={_options.Dimensions}。" +
                $"实际={embedding.Count}。");
        }

        return embedding.ToArray();
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
