using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Services.BI.Planning;

/// <summary>
/// QueryIntent 提示词的语言指令（P5 Multi-Language Runtime）。
///
/// <para>
/// <strong>语言无关化的实现原理</strong>：意图理解的输出不是"翻译"，而是
/// <em>归一化到平台默认语言（zh-CN）的语义空间</em>。无论用户用中文、英文、日文还是
/// 韩文提问，要求模型一律以中文输出 Metric / Dimension / Filter 的语义名称，
/// 使其与本平台 Metadata 语义空间对齐 —— 下游
/// <c>Intent → Semantic Concept → QueryPlan</c> 的解析因此与提问语言无关。
/// </para>
///
/// <para>
/// <strong>零回归门控</strong>：仅在传入<strong>非默认</strong>语言区域时追加指令块；
/// 默认语言（zh-CN）返回空字符串，提示词与 P5 之前逐字节一致 —— 这是 Golden
/// 与既有中文链路不受影响的关键。
/// </para>
/// </summary>
public static class QueryIntentLocaleDirective
{
	/// <summary>
	/// 生成语言指令块；默认语言/Invariant/null 一律返回空字符串（门控）。
	/// </summary>
	public static string Build(LocaleContext? locale)
	{
		// 门控：默认语言无需任何额外指令——模型本就应以中文语义空间作答。
		// 追加指令反而会改变提示词，破坏 Golden 的提示词稳定性。
		if (locale is null
			|| locale.IsDefault
			|| string.IsNullOrEmpty(locale.Culture))
		{
			return string.Empty;
		}

		var languageName = DescribeLanguage(locale.Language);

		return $$"""

                        ============================================================
                        语言处理要求
                        ============================================================

                        1. 用户使用【{{languageName}}（{{locale.Culture}}）】提问。

                        2. 你必须完整理解该语言的业务含义，不得因语言差异而拒绝或简化。

                        3. 【关键】输出的所有业务语义名称（Metrics / Dimensions / Filters 的
                           字段语义名、表名、业务术语）一律使用【简体中文】，
                           与平台 Metadata 语义空间保持一致。

                        4. 禁止在输出中保留{{languageName}}原始词汇作为字段语义名——
                           否则下游无法匹配到真实 Metadata 字段。

                        5. 若用户问题中的某个概念无法对应到 Metadata 知识中的字段，
                           按常规规则留空，不要臆造。

                        """;
	}

	/// <summary>把语言代码转为人可读名称，用于提示词表述；未登记语言回退显示代码本身。</summary>
	private static string DescribeLanguage(string language)
		=> language switch
		{
			"zh" => "中文",
			"en" => "英文",
			"ja" => "日文",
			"ko" => "韩文",
			_ => string.IsNullOrWhiteSpace(language) ? "该语言" : language,
		};
}
