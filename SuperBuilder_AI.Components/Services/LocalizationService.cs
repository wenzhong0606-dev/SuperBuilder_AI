using System.Collections.Generic;

namespace SuperBuilder_AI.Components.Services;

/// <summary>运行时多语言骨架：默认 zh-CN 词典（P11.2 接入 api/localization/resolve 拉取远程词条）。</summary>
public sealed class LocalizationService
{
    private readonly Dictionary<string, string> _zh = new()
    {
        ["app.title"] = "SuperBuilder AI",
        ["nav.ask"] = "Ask BI 智能问数",
        ["page.login"] = "登录 / 租户选择",
        ["page.ask"] = "Ask BI 智能问数",
    };

    public string CurrentCulture { get; private set; } = "zh-CN";

    public string T(string key) => _zh.TryGetValue(key, out var v) ? v : key;
}
