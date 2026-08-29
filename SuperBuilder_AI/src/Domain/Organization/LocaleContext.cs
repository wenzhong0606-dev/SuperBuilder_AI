namespace SuperBuilder_AI.Models.Organization;

/// <summary>
/// 语言区域运行时上下文（P5 Multi-Language Runtime）。
///
/// 把散落的字符串 <c>culture</c>（如 <c>"zh-CN"</c>）收敛为带明确语义的运行时上下文对象，
/// 作为平台级多语言能力的统一载体：本地化资源解析、业务语义多语言标签、AI 意图语言无关化。
///
/// <para>
/// 设计要点：
/// <list type="number">
/// <item>文化标识按 IETF BCP 47 归一化：语言部分小写、地区部分大写（<c>zh_CN</c> → <c>zh-CN</c>）。</item>
/// <item>默认语言为 <c>zh-CN</c>（与平台既有中文业务语义、Golden 回归基线一致），
///       未指定语言的路径（含 Golden 运行时）恒得到 <see cref="Default"/> → 行为与 P5 之前完全一致（零回归）。</item>
/// <item>本类型仅承载上下文数据；回退链、资源解析等策略逻辑位于应用层
///       <c>ILocalizationService</c>，避免领域对象膨胀。</item>
/// </list>
/// </para>
/// </summary>
public sealed record LocaleContext
{
	/// <summary>归一化的 IETF 语言标签，如 <c>zh-CN</c> / <c>en-US</c> / <c>ja-JP</c>。永不为 null；未指定时为 <see cref="Default"/> 的文化名。</summary>
	public string Culture { get; init; }

	/// <summary>语言子标签（小写），如 <c>zh</c> / <c>en</c>。用于多语言标签的粗粒度回退。</summary>
	public string Language { get; init; }

	/// <summary>地区子标签（大写），如 <c>CN</c> / <c>US</c>；纯语言文化（如 <c>zh</c>）时为 null。</summary>
	public string? Region { get; init; }

	/// <summary>该语言区域的自有名称（Endonym），如 <c>简体中文</c> / <c>English</c>。用于语言切换器展示。</summary>
	public string DisplayName { get; init; }

	/// <summary>建议时区（IANA / Windows 标识），供时间维度展示与日期格式化使用；未配置时为 null。</summary>
	public string? TimeZoneId { get; init; }

	/// <summary>文本方向：<c>ltr</c>（默认）或 <c>rtl</c>（阿拉伯语、希伯来语等）。</summary>
	public string TextDirection { get; init; }

	/// <summary>
	/// 是否为平台默认语言（<c>zh-CN</c>）。
	/// 为 <c>true</c> 时，本地化资源与语义标签解析可跳过回退链直接取默认，减少一次查询。
	/// </summary>
	public bool IsDefault { get; init; }

	private LocaleContext(
		string culture,
		string language,
		string? region,
		string displayName,
		string? timeZoneId,
		string textDirection,
		bool isDefault)
	{
		Culture = culture;
		Language = language;
		Region = region;
		DisplayName = displayName;
		TimeZoneId = timeZoneId;
		TextDirection = textDirection;
		IsDefault = isDefault;
	}

	/// <summary>
	/// 由任意文化字符串构造语言区域上下文；无法识别或为 null/空白时回退到 <see cref="Default"/>。
	/// 归一化规则：<c>_</c> 统一为 <c>-</c>，语言段小写、地区段大写。
	/// </summary>
	public static LocaleContext FromCulture(string? culture)
	{
		var normalized = Normalize(culture);
		if (normalized is null) return Default;

		// 命中内置支持的语言区域：携带完整的展示名/时区/方向元数据。
		if (Supported.TryGetValue(normalized, out var builtIn)) return builtIn;

		// 未内置但格式合法（如 de-DE）：保持可扩展，按规则推导方向与语言段。
		var separator = normalized.IndexOf('-');
		var language = separator > 0 ? normalized[..separator] : normalized;
		var region = separator > 0 ? normalized[(separator + 1)..] : null;
		return new LocaleContext(
			normalized,
			language,
			region,
			normalized,
			null,
			IsRightToLeft(language) ? "rtl" : "ltr",
			false);
	}

	/// <summary>平台默认语言区域（<c>zh-CN</c>）。所有未显式指定语言的路径都落到此实例。</summary>
	public static LocaleContext Default { get; } = new(
		"zh-CN", "zh", "CN", "简体中文", "Asia/Shanghai", "ltr", true);

	/// <summary>
	/// 语言无关上下文（<c>Invariant</c>）。
	/// 用于语义层内部运作：不参与展示，仅表示"不按任何具体语言处理"。
	/// 与 <see cref="Default"/> 的区别在于它明确表示"无语言偏好"而非"默认中文"。
	/// </summary>
	public static LocaleContext Invariant { get; } = new(
		string.Empty, string.Empty, null, "Invariant", null, "ltr", false);

	/// <summary>
	/// 平台内置支持的语言区域（P5 首批）。键为归一化文化名。
	/// 扩展新语言只需在此登记，无需改动解析逻辑。
	/// </summary>
	public static IReadOnlyDictionary<string, LocaleContext> Supported { get; } =
		new Dictionary<string, LocaleContext>(StringComparer.OrdinalIgnoreCase)
		{
			["zh-CN"] = Default,
			["zh-TW"] = new("zh-TW", "zh", "TW", "繁體中文", "Asia/Taipei", "ltr", false),
			["en-US"] = new("en-US", "en", "US", "English", "America/New_York", "ltr", false),
			["ja-JP"] = new("ja-JP", "ja", "JP", "日本語", "Asia/Tokyo", "ltr", false),
			["ko-KR"] = new("ko-KR", "ko", "KR", "한국어", "Asia/Seoul", "ltr", false),
		};

	/// <summary>归一化文化字符串；输入无效时返回 null（交由调用方回退 <see cref="Default"/>）。</summary>
	private static string? Normalize(string? culture)
	{
		if (string.IsNullOrWhiteSpace(culture)) return null;

		var value = culture.Trim().Replace('_', '-');
		var separator = value.IndexOf('-');
		if (separator < 0)
		{
			// 纯语言段，如 "zh" / "en"。
			return value.Length is >= 2 and <= 3 && value.All(char.IsAsciiLetter)
				? value.ToLowerInvariant()
				: null;
		}

		var language = value[..separator];
		var region = value[(separator + 1)..];
		if (language.Length is < 2 or > 3 || !language.All(char.IsAsciiLetter)) return null;
		if (region.Length != 2 || !region.All(char.IsAsciiLetter)) return null;

		return $"{language.ToLowerInvariant()}-{region.ToUpperInvariant()}";
	}

	/// <summary>是否为从右向左书写的语言。</summary>
	private static bool IsRightToLeft(string language)
		=> language is "ar" or "he" or "fa" or "ur" or "ps" or "sd" or "yi";
}
