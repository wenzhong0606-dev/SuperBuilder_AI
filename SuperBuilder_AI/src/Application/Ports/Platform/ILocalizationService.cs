using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Interfaces.Platform;

/// <summary>
/// 本地化服务端口（P5 Multi-Language Runtime）。
///
/// 职责边界：
/// <list type="bullet">
/// <item>语言区域解析与归一化（<see cref="LocaleContext"/>）。</item>
/// <item>回退链构造：用于多语言标签/资源查找的优先级序列。</item>
/// <item>UI/消息文本的多语言资源取值（P5.3 接入资源文件；P5.1 返回键名本身作为占位）。</item>
/// </list>
///
/// 不负责：业务语义多语言标签的持久化读写（P5.2 <c>ISemanticLabelService</c>）、
/// AI 意图的语言无关化（P5.4）。
/// </summary>
public interface ILocalizationService
{
	/// <summary>
	/// 解析语言区域上下文。输入为 null/空白或无法识别时回退到 <see cref="LocaleContext.Default"/>。
	/// </summary>
	LocaleContext Resolve(string? culture);

	/// <summary>
	/// 构造标签查找回退链（按优先级从高到低）。
	/// 例：<c>zh-TW</c> → <c>["zh-TW", "zh", "zh-CN"]</c>；
	/// 默认语言 → <c>["zh-CN"]</c>（已是终点，无需回退）。
	/// <para>
	/// 设计要点：回退链终结点恒为 <see cref="LocaleContext.Default"/> 的文化名，
	/// 保证任何语言下都至少能取到平台默认标签，避免空展示。
	/// </para>
	/// </summary>
	IReadOnlyList<string> BuildFallbackChain(LocaleContext? locale);

	/// <summary>平台支持的语言区域列表（供语言切换器与端点暴露）。</summary>
	IReadOnlyList<LocaleContext> SupportedLocales { get; }

	/// <summary>
	/// 按语言区域取本地化文本。
	/// P5.1 阶段尚未接入资源文件，返回 <paramref name="key"/> 本身作为占位（保证调用方无需判空）；
	/// P5.3 接入资源后返回对应语言的文案，未命中时回退默认语言。
	/// </summary>
	string GetString(string key, LocaleContext? locale = null);
}
