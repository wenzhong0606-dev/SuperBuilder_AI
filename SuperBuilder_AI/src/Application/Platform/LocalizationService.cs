using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Services.Platform;

/// <summary>
/// <see cref="ILocalizationService"/> 的默认实现（P5 Multi-Language Runtime）。
///
/// 纯确定性实现：不访问数据库、不调用 LLM，所有结果仅依赖输入与
/// <see cref="LocaleContext.Supported"/>，保证可在单元测试中离线断言。
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
	/// <inheritdoc />
	public LocaleContext Resolve(string? culture) => LocaleContext.FromCulture(culture);

	/// <inheritdoc />
	public IReadOnlyList<string> BuildFallbackChain(LocaleContext? locale)
	{
		var context = locale ?? LocaleContext.Default;

		// 默认语言已是回退终点，直接返回单元素链，避免无意义的二次查找。
		if (context.IsDefault) return new[] { context.Culture };

		var chain = new List<string>(3);

		// 1) 精确文化：zh-TW / en-US
		if (!string.IsNullOrEmpty(context.Culture)) chain.Add(context.Culture);

		// 2) 纯语言段：zh / en —— 覆盖"同语言不同地区"的通用标签。
		if (!string.IsNullOrEmpty(context.Language)
			&& !string.Equals(context.Language, context.Culture, StringComparison.OrdinalIgnoreCase))
		{
			chain.Add(context.Language);
		}

		// 3) 平台默认语言：保证任何语言下都至少有兜底文案。
		var defaultCulture = LocaleContext.Default.Culture;
		if (!chain.Contains(defaultCulture, StringComparer.OrdinalIgnoreCase)) chain.Add(defaultCulture);

		return chain.Count > 0 ? chain : new[] { defaultCulture };
	}

	/// <inheritdoc />
	public IReadOnlyList<LocaleContext> SupportedLocales { get; } =
		LocaleContext.Supported.Values.ToList();

	/// <inheritdoc />
	public string GetString(string key, LocaleContext? locale = null)
	{
		if (string.IsNullOrWhiteSpace(key)) return string.Empty;

		// P5.1：资源文件尚未接入，返回键名本身，使调用方在 P5.3 之前即可安全使用。
		// 该行为对现有链路零影响（当前无任何调用方依赖本地化文案）。
		return key;
	}
}
