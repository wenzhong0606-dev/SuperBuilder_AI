using System.Text.RegularExpressions;

namespace SuperBuilder_AI.Services.Localization;

/// <summary>
/// 本地化译文占位符校验（M3-04「保存译文时校验 {0} 等占位符一致性」）。
/// <para>
/// 译文中的 <c>{0}</c>/<c>{1}</c> 等格式占位符须与平台基线保持一致，否则运行时 <c>string.Format</c>
/// 会因参数数不匹配抛 <see cref="System.FormatException"/>。此处仅比较占位符索引集合（顺序无关），
/// 允许译文调整参数次序（如 <c>{1}{0}</c> 仍为合法等价），但禁止增删占位符。
/// </para>
/// </summary>
public static class LocalizationPlaceholderValidator
{
    private static readonly Regex Placeholder = new(@"\{([0-9]+)\}", RegexOptions.Compiled);

    /// <summary>提取文本中出现的所有占位符索引（去重、升序）。</summary>
    public static IReadOnlyList<int> ExtractPlaceholders(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<int>();
        return Placeholder.Matches(text)
            .Select(m => int.Parse(m.Groups[1].Value))
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    /// <summary>判断基线与译文占位符集合是否一致（顺序无关）。</summary>
    public static bool AreConsistent(string? baseline, string? translation)
        => ExtractPlaceholders(baseline).SequenceEqual(ExtractPlaceholders(translation));
}
