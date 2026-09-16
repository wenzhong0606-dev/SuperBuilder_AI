using System.Text.RegularExpressions;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>一次解析出的显式纠正。</summary>
public sealed record CorrectionCapture(CorrectionKind Kind, CorrectionPayload Payload);

/// <summary>
/// 显式纠正指令解析器（纯函数，便于单测）。
///
/// <para>
/// 仅识别「用户明确表达」的纠正句式，绝不从普通问句推断，避免规则中毒：
/// <list type="bullet">
/// <item>列展示声明：「type 应该显示类型名称」「status 列展示状态名」</item>
/// <item>码值映射：「type：1=采购入库，2=调拨入库」</item>
/// </list>
/// 表级纠正（「改成 wms_storage_receipt 表」）由 <c>AskController.TryExtractTableCorrection</c> 负责，此处不重复。
/// </para>
/// </summary>
public static class CorrectionInstructionParser
{
	/// <summary>展示类动词。</summary>
	private static readonly string[] DisplayMarkers =
	{
		"显示", "展示", "输出为", "呈现",
	};

	/// <summary>匹配「<列名> 字段/列 应显示 ...」中的列名（英文物理列名）。</summary>
	private static readonly Regex ColumnToken = new(
		@"(?<col>[A-Za-z_][A-Za-z0-9_]{1,63})\s*(?:字段|列)?\s*(?:应该|应|要|需|得)?\s*(?:显示|展示|呈现|输出)",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

	/// <summary>英文标识符（用于定位码值映射句中的目标列）。</summary>
	private static readonly Regex Identifier = new(
		@"[A-Za-z_][A-Za-z0-9_]*",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>
	/// 匹配「X 应显示 Y …」中的 Y（用于识别字典分类提示），支持多分类列表：
	/// <c>type 应显示 warehousing_type,outbound_type 名称</c>。
	/// </summary>
	private static readonly Regex DisplayTargetList = new(
		@"[A-Za-z_][A-Za-z0-9_]{1,63}\s*(?:字段|列)?\s*(?:应该|应|要|需|得)?\s*(?:显示|展示|呈现|输出)\s*"
		+ @"(?<list>[A-Za-z_][A-Za-z0-9_]*(?:\s*[,;|，；]\s*[A-Za-z_][A-Za-z0-9_]*)*)",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

	/// <summary>分类列表分隔符。</summary>
	private static readonly char[] CategorySeparators = { ',', ';', '|', '，', '；' };

	/// <summary>安全标识符（字典分类名）。</summary>
	private static readonly Regex SafeName = new(
		@"^[A-Za-z_][A-Za-z0-9_]*$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>匹配 code=label 码值对（label 不得为纯数字，避免把条件当映射）。</summary>
	private static readonly Regex ValuePair = new(
		@"(?<code>\d+|[A-Za-z_][A-Za-z0-9_]{0,31})\s*=\s*(?<label>[^\s,，;；。、]+)",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>解析显式纠正指令；无命中返回空列表。</summary>
	public static IReadOnlyList<CorrectionCapture> Parse(string? instruction)
	{
		var captures = new List<CorrectionCapture>();
		if (string.IsNullOrWhiteSpace(instruction)) return captures;

		var text = instruction.Trim();
		var column = ExtractColumn(text);
		var valueMap = ExtractValueMap(text);

		if (valueMap.Count > 0)
		{
			captures.Add(new CorrectionCapture(
				CorrectionKind.ValueMap,
				new CorrectionPayload
				{
					ColumnName = column,
					ValueMap = valueMap,
				}));
		}
		else if (!string.IsNullOrWhiteSpace(column) && ContainsDisplayMarker(text))
		{
			// 声明「该列应展示文本」；若用户给出字典分类名（如 receipt_type），一并记录以便自动查字典译码。
			var hint = ExtractCategoryHint(text, column);
			captures.Add(new CorrectionCapture(
				CorrectionKind.ColumnDisplay,
				new CorrectionPayload { ColumnName = column, CategoryHint = hint }));
		}

		return captures;
	}

	/// <summary>
	/// 提取被声明的物理列名。
	/// 优先匹配「X 字段/列 应显示 …」句式；否则回退为「第一个 = 之前最近的英文标识符」（码值映射句）。
	/// </summary>
	public static string? ExtractColumn(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return null;

		// 1) 展示句式
		var matches = ColumnToken.Matches(text);
		for (var i = matches.Count - 1; i >= 0; i--)
		{
			var candidate = matches[i].Groups["col"].Value;
			if (IsReservedWord(candidate)) continue;
			return candidate;
		}

		// 2) 码值映射句式：「type 字段的取值应该是 1=采购入库, 2=调拨入库」
		var equalsIndex = text.IndexOf('=');
		if (equalsIndex <= 0) return null;

		var head = text.Substring(0, equalsIndex);
		var identifiers = Identifier.Matches(head);
		for (var i = identifiers.Count - 1; i >= 0; i--)
		{
			var candidate = identifiers[i].Value;
			if (IsReservedWord(candidate)) continue;
			return candidate;
		}

		return null;
	}

	/// <summary>
	/// 提取字典分类提示：「type 应显示 receipt_type 名称」→ <c>receipt_type</c>；
	/// 多分类：「type 应显示 warehousing_type,outbound_type」→ <c>warehousing_type,outbound_type</c>。
	///
	/// <para>
	/// 目标词必须是安全英文标识符（自然语言描述如「状态名称」被拒），且单个目标不得等于列名本身。
	/// 多分类是实测必需：WMS 单据表的 <c>type</c> 单列码值跨 warehousing_type / outbound_type /
	/// variation_type / Input_output_type / wms_check_type 五个分类。
	/// </para>
	/// </summary>
	public static string? ExtractCategoryHint(string text, string? column = null)
	{
		if (string.IsNullOrWhiteSpace(text)) return null;

		var match = DisplayTargetList.Match(text);
		if (!match.Success) return null;

		var tokens = match.Groups["list"].Value
			.Split(CategorySeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(token => SafeName.IsMatch(token))
			.ToList();

		if (tokens.Count == 0) return null;

		// 仅单个且与列名相同 → 无信息量（如「type 应显示 type」）。
		if (tokens.Count == 1
			&& !string.IsNullOrWhiteSpace(column)
			&& tokens[0].Equals(column, StringComparison.OrdinalIgnoreCase))
			return null;

		return string.Join(",", tokens);
	}

	/// <summary>提取 code=label 码值映射；label 为纯数字的项丢弃。</summary>
	public static Dictionary<string, string> ExtractValueMap(string text)
	{
		var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (Match match in ValuePair.Matches(text))
		{
			var code = match.Groups["code"].Value.Trim();
			var label = match.Groups["label"].Value.Trim();
			if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(label)) continue;
			if (label.All(ch => char.IsDigit(ch) || ch == '.')) continue;
			map[code] = label;
		}

		return map;
	}

	private static bool ContainsDisplayMarker(string text)
		=> DisplayMarkers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));

	private static bool IsReservedWord(string candidate)
		=> candidate.Equals("select", StringComparison.OrdinalIgnoreCase)
			|| candidate.Equals("from", StringComparison.OrdinalIgnoreCase)
			|| candidate.Equals("where", StringComparison.OrdinalIgnoreCase)
			|| candidate.Equals("and", StringComparison.OrdinalIgnoreCase)
			|| candidate.Equals("or", StringComparison.OrdinalIgnoreCase)
			|| candidate.Equals("by", StringComparison.OrdinalIgnoreCase)
			|| candidate.Equals("order", StringComparison.OrdinalIgnoreCase)
			|| candidate.Equals("group", StringComparison.OrdinalIgnoreCase);
}
