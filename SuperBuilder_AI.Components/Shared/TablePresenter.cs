using System.Collections.Generic;
using System.Text.Json;

namespace SuperBuilder_AI.Components.Shared;

/// <summary>
/// 将后端任意 JSON 数组/对象安全渲染为友好表格的静态辅助。
/// 不依赖具体 DTO 字段名，列名取首行属性键并映射为中文友好名；
/// 状态类字段自动套用徽标样式。
/// </summary>
public static class TablePresenter
{
    private static readonly Dictionary<string, string> FriendlyNames = new()
    {
        ["id"] = "ID", ["code"] = "编码", ["name"] = "名称", ["title"] = "标题",
        ["displayName"] = "显示名", ["displayname"] = "显示名", ["description"] = "描述",
        ["status"] = "状态", ["type"] = "类型", ["kind"] = "类型", ["category"] = "分类",
        ["entity"] = "实体", ["domain"] = "域", ["tenantId"] = "租户", ["tenantid"] = "租户",
        ["userId"] = "用户", ["username"] = "用户名", ["email"] = "邮箱", ["role"] = "角色",
        ["roles"] = "角色", ["permissions"] = "权限", ["locale"] = "区域", ["culture"] = "区域",
        ["metric"] = "指标", ["resourceType"] = "资源", ["used"] = "已用", ["limit"] = "限额",
        ["enabled"] = "启用", ["isEnabled"] = "启用", ["createdAt"] = "创建时间",
        ["createdat"] = "创建时间", ["updatedAt"] = "更新时间", ["updatedat"] = "更新时间",
        ["synonyms"] = "同义词", ["recall"] = "召回", ["dslJson"] = "DSL", ["source"] = "来源",
    };

    /// <summary>从 JSON 根推断表格列。支持数组，或对象内常见集合键（data/items/results）。</summary>
    public static List<string> InferColumns(JsonElement? root)
    {
        if (root is null) return new();
        var arr = AsArray(root.Value);
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            return new();
        var first = arr[0];
        if (first.ValueKind != JsonValueKind.Object) return new();
        var cols = new List<string>();
        foreach (var p in first.EnumerateObject())
            if (p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number
                or JsonValueKind.True or JsonValueKind.False)
                cols.Add(p.Name);
        // 状态/名称类字段优先排前
        cols.Sort((a, b) =>
        {
            var pa = Priority(a); var pb = Priority(b);
            if (pa != pb) return pa - pb;
            return string.CompareOrdinal(a, b);
        });
        return cols;
    }

    /// <summary>取某行某列的值（字符串化）。</summary>
    public static string Cell(JsonElement row, string key)
    {
        if (row.ValueKind != JsonValueKind.Object) return "";
        if (!row.TryGetProperty(key, out var v)) return "";
        return Stringify(v);
    }

    public static string Friendly(string key)
        => FriendlyNames.TryGetValue(key, out var n) ? n : Humanize(key);

    /// <summary>状态类徽标变体。未知状态回退 muted。</summary>
    public static string StatusBadge(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "badge-muted";
        var s = value.ToLowerInvariant();
        if (s is "active" or "enabled" or "published" or "ready" or "ok" or "success" or "online")
            return "badge-success";
        if (s is "draft" or "pending" or "review" or "warning" or "paused")
            return "badge-warning";
        if (s is "disabled" or "archived" or "error" or "failed" or "offline" or "deleted")
            return "badge-danger";
        return "badge-info";
    }

    public static bool IsStatusColumn(string key)
        => key.Equals("status", System.StringComparison.OrdinalIgnoreCase)
        || key.Equals("state", System.StringComparison.OrdinalIgnoreCase);

    // —— 内部 ——

    private static JsonElement AsArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "data", "items", "results", "list", "value" })
                if (root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array)
                    return v;
        }
        return root;
    }

    private static int Priority(string key)
    {
        var k = key.ToLowerInvariant();
        if (k is "code" or "name" or "title" or "displayname") return 0;
        if (k is "status" or "state" or "type" or "kind") return 1;
        if (k is "id") return 2;
        if (k is "description" or "dsljson") return 9;
        return 5;
    }

    private static string Stringify(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.Number => v.GetRawText(),
        JsonValueKind.True => "是",
        JsonValueKind.False => "否",
        JsonValueKind.Null => "",
        _ => "[…]",
    };

    /// <summary>公开包装：供页面直接字符串化任意 JSON 值。</summary>
    public static string StringifySafe(JsonElement v) => Stringify(v);

    private static string Humanize(string key)
    {
        var s = key;
        var sb = new System.Text.StringBuilder();
        bool upper = true;
        foreach (var c in s)
        {
            if (c is '-' or '_' or ' ') { upper = true; continue; }
            sb.Append(upper ? char.ToUpperInvariant(c) : c);
            upper = false;
        }
        return sb.ToString();
    }
}
