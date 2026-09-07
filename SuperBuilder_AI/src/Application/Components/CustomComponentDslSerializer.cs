using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Models.AppBuilder;
using SuperBuilder_AI.Models.Components;

namespace SuperBuilder_AI.Services.Components;

/// <summary>
/// 自定义组件 DSL 安全边界：限制大小、版本、Key、组件类型，并复用 App DSL 的
/// HTML/脚本、数据绑定、聚合与筛选白名单校验。数据库永不保存未经校验的 DSL。
/// </summary>
public sealed class CustomComponentDslSerializer
{
    public const int MaxDslBytes = 64 * 1024;
    private static readonly Regex KeyPattern = new("^[a-z][a-z0-9-]{1,63}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IAppDslSerializer _appSerializer;

    public CustomComponentDslSerializer(IAppDslSerializer appSerializer) => _appSerializer = appSerializer;

    public string Serialize(CustomComponentDsl dsl) => JsonSerializer.Serialize(dsl, Options);

    public bool TryDeserialize(string? json, out CustomComponentDsl? dsl, out IReadOnlyList<string> errors)
    {
        dsl = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            errors = new[] { "组件 DSL JSON 不能为空。" };
            return false;
        }
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxDslBytes)
        {
            errors = new[] { $"组件 DSL 超过 {MaxDslBytes / 1024}KB 上限。" };
            return false;
        }

        try
        {
            dsl = JsonSerializer.Deserialize<CustomComponentDsl>(json, Options);
        }
        catch (JsonException ex)
        {
            errors = new[] { $"组件 DSL JSON 解析失败：{ex.Message}" };
            return false;
        }

        if (dsl is null)
        {
            errors = new[] { "组件 DSL 反序列化结果为空。" };
            return false;
        }

        var list = new List<string>();
        if (!CustomComponentDslVersions.Supported.Contains(dsl.Version))
            list.Add($"不支持的组件 DSL 版本：{dsl.Version}。");
        if (!KeyPattern.IsMatch(dsl.Key ?? string.Empty))
            list.Add("组件 Key 必须以小写字母开头，且只能包含小写字母、数字和连字符（2-64 位）。");
        if (string.IsNullOrWhiteSpace(dsl.Name))
            list.Add("组件名称不能为空。");
        if (dsl.Root is null)
            list.Add("组件 Root 不能为空。");
        else
        {
            var wrapper = new AppDsl
            {
                Version = AppDslVersions.Current,
                Name = dsl.Name,
                Description = dsl.Description,
                Pages = new List<PagePlan>
                {
                    new()
                    {
                        Id = "component-preview",
                        Name = dsl.Name,
                        Components = new List<ComponentPlan> { dsl.Root },
                    },
                },
            };
            list.AddRange(_appSerializer.Validate(wrapper));
        }

        errors = list;
        if (list.Count == 0) return true;
        dsl = null;
        return false;
    }

    public string Blueprint(string type = AppComponentTypes.Chart)
    {
        if (!AppComponentTypes.Supported.Contains(type)) type = AppComponentTypes.Chart;
        return Serialize(new CustomComponentDsl
        {
            Key = "my-component",
            Name = "My component",
            Root = new ComponentPlan
            {
                Id = "root",
                Type = type,
                Title = "My component",
                Order = 1,
                Properties = new Dictionary<string, string>(),
                Style = new AppComponentStyle { Palette = "primary", ShowBorder = true, Padding = "normal" },
            },
        });
    }
}
