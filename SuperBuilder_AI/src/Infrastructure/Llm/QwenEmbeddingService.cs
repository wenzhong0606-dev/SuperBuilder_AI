using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces;
using System.Collections.Generic;

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
        var vectors = await GenerateBatchAsync(
            new[] { text },
            textType);
        return vectors[0];
    }

    public async Task<IReadOnlyList<float[]>> GenerateBatchAsync(
        IEnumerable<string> texts,
        string textType = "document")
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
        // DashScope 原生 API/SDK 提供。兼容端点不接收该非兼容字段。
        _ = textType;

        var requestBody = new
        {
            model = _options.Model,
            input = list,
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

        if (result.Data.Count != list.Count)
        {
            throw new InvalidOperationException(
                $"Embedding 数量不匹配。" +
                $"请求={list.Count}。" +
                $"返回={result.Data.Count}。");
        }

        var vectors = new List<float[]>(list.Count);
        for (var i = 0; i < result.Data.Count; i++)
        {
            var embedding = result.Data[i].Embedding;
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
