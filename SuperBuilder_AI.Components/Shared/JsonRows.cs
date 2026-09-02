using System.Collections.Generic;
using System.Text.Json;

namespace SuperBuilder_AI.Components.Shared;

/// <summary>
/// 松类型 JSON → 表格行的抽取工具。后端端点形状不完全一致
/// （有的直接返回数组，有的包在 <c>data.rows</c> / <c>rows</c> / <c>items</c> 中），
/// 由此处统一归一，页面只消费 <c>字段名 → 值</c> 的字典列表。
/// </summary>
public static class JsonRows
{
    /// <summary>从任意响应元素中提取行集合（失败返回空集合）。</summary>
    public static List<Dictionary<string, object?>> Extract(JsonElement? root)
    {
        if (root is null) return new List<Dictionary<string, object?>>();
        var el = root.Value;

        if (el.ValueKind == JsonValueKind.Array) return ToRows(el);
        if (el.ValueKind != JsonValueKind.Object) return new List<Dictionary<string, object?>>();

        foreach (var key in new[] { "rows", "data", "items", "result" })
        {
            if (!el.TryGetProperty(key, out var node)) continue;
            if (node.ValueKind == JsonValueKind.Array) return ToRows(node);
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("rows", out var inner) && inner.ValueKind == JsonValueKind.Array)
                    return ToRows(inner);
            }
        }
        return new List<Dictionary<string, object?>>();
    }

    /// <summary>把 JSON 数组转为行字典（每个属性为一个单元格）。</summary>
    public static List<Dictionary<string, object?>> ToRows(JsonElement array)
    {
        var list = new List<Dictionary<string, object?>>();
        if (array.ValueKind != JsonValueKind.Array) return list;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var row = new Dictionary<string, object?>();
            foreach (var p in item.EnumerateObject()) row[p.Name] = p.Value;
            list.Add(row);
        }
        return list;
    }

    /// <summary>取对象元素中的标量属性（字符串/数字/布尔），用于详情页元信息展示。</summary>
    public static string Scalar(JsonElement? root, params string[] candidateKeys)
    {
        if (root is not { ValueKind: JsonValueKind.Object }) return "";
        var el = root.Value;
        foreach (var key in candidateKeys)
        {
            if (!el.TryGetProperty(key, out var v)) continue;
            if (v.ValueKind is JsonValueKind.String) return v.GetString() ?? "";
            if (v.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) return v.ToString();
        }
        return "";
    }

    /// <summary>以 <c>键 → 值</c> 形式输出对象的全部标量属性（跳过对象/数组），用于 SbKeyValue。</summary>
    public static List<KeyValuePair<string, string>> Scalars(JsonElement? root)
    {
        var list = new List<KeyValuePair<string, string>>();
        if (root is not { ValueKind: JsonValueKind.Object }) return list;
        foreach (var p in root.Value.EnumerateObject())
        {
            if (p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) continue;
            var text = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.ToString();
            list.Add(new KeyValuePair<string, string>(p.Name, text));
        }
        return list;
    }
}
