using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Services;
using Xunit;

namespace SuperBuilder_AI.LiveAI.Tests;

public sealed class LiveAiSmokeTests
{
    [Fact]
    public async Task Qwen_And_Embedding_Live_Contracts_Are_Healthy()
    {
        Assert.True(
            string.Equals(
                Environment.GetEnvironmentVariable("SB_LIVE_AI"),
                "true",
                StringComparison.OrdinalIgnoreCase),
            "Live AI 测试只能由专用 workflow 显式启用。");

        var qwenKey = Environment.GetEnvironmentVariable("QWEN_API_KEY");
        var embeddingKey = Environment.GetEnvironmentVariable("EMBEDDING_API_KEY");

        Assert.False(string.IsNullOrWhiteSpace(qwenKey), "QWEN_API_KEY 未配置。");
        Assert.False(string.IsNullOrWhiteSpace(embeddingKey), "EMBEDDING_API_KEY 未配置。");

        var qwenConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Qwen:ApiKey"] = qwenKey,
                ["Qwen:Endpoint"] = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions",
                ["Qwen:Model"] = "qwen-plus",
                ["Qwen:TimeoutSeconds"] = "90"
            })
            .Build();

        using var qwenHttp = new HttpClient();
        var qwen = new QwenService(qwenHttp, qwenConfig);

        var qwenResult = await qwen.GenerateSqlAsync(
            "Return only this exact marker with no explanation: SUPERBUILDER_LIVE_AI_OK");

        Assert.Contains("SUPERBUILDER_LIVE_AI_OK", qwenResult, StringComparison.OrdinalIgnoreCase);

        using var embeddingHttp = new HttpClient();
        var embedding = new QwenEmbeddingService(
            embeddingHttp,
            Options.Create(new EmbeddingOptions
            {
                ApiKey = embeddingKey!,
                Endpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1/embeddings",
                Model = "qwen3.7-text-embedding",
                Dimensions = 1024,
                TimeoutSeconds = 90,
                BatchSize = 4
            }));

        var vector = await embedding.GenerateAsync("SuperBuilder AI live regression");

        Assert.Equal(1024, vector.Length);
        Assert.All(vector, value => Assert.False(float.IsNaN(value) || float.IsInfinity(value)));
    }
}
