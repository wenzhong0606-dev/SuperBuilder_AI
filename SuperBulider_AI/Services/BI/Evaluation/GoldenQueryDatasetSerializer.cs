using System.Text.Json;
using System.Text.Json.Serialization;
using SuperBulider_AI.Models.BI.Evaluation;

namespace SuperBulider_AI.Services.BI.Evaluation;

/// <summary>
/// Golden Dataset JSON 序列化与反序列化边界。
/// 仅负责 JSON 与 GoldenQueryDataset 之间的转换，不执行 QueryPlan Evaluation。
/// </summary>
public sealed class GoldenQueryDatasetSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public string Serialize(GoldenQueryDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        return JsonSerializer.Serialize(dataset, Options);
    }

    public GoldenQueryDataset Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var dataset = JsonSerializer.Deserialize<GoldenQueryDataset>(json, Options);
        return dataset ?? throw new JsonException("Golden Dataset JSON deserialized to null.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
    }
}
