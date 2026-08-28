using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// Flexible JSON converter for List&lt;string&gt; that handles both
/// string arrays and object arrays returned by the LLM.
///
/// When the LLM returns Dimensions as objects (e.g., [{"semanticText":"物料"}])
/// instead of strings (e.g., ["物料"]), this converter extracts the
/// semanticText / name / semantic_text field and normalizes to a string.
///
/// This prevents JSON deserialization failures in QueryUnderstandingService.
/// </summary>
public sealed class FlexibleStringListConverter
    : JsonConverter<List<string>>
{
    private static readonly string[] PropertyNames =
        { "semanticText", "name", "semantic_text", "value", "label", "text" };

    public override List<string>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return new List<string>();

        if (reader.TokenType != JsonTokenType.StartArray)
            return new List<string>();

        var result = new List<string>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    result.Add(value);
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                // Object element — extract a known property
                string? extracted = null;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;

                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var propName = reader.GetString();

                        if (propName is not null &&
                            PropertyNames.Contains(
                                propName,
                                StringComparer.OrdinalIgnoreCase))
                        {
                            reader.Read();
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                extracted ??= reader.GetString();
                            }
                            else
                            {
                                // Skip the value
                                reader.Skip();
                            }
                        }
                        else
                        {
                            // Skip the property value
                            reader.Read();
                            reader.Skip();
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(extracted))
                    result.Add(extracted);
            }
            else
            {
                // Skip unexpected tokens
                reader.Skip();
            }
        }

        return result;
    }

    public override void Write(
        Utf8JsonWriter writer,
        List<string> value,
        JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
