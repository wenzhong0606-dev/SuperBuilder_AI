using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using SuperBuilder_AI.Models.DTO;

namespace SuperBuilder_AI.Services;

/// <summary>
/// 元数据发现启发式：字典表识别、角色映射（code/name/type）、外键展示列优选。
/// 纯函数、无 IO —— 便于单元测试，并保证对任意租户/数据源的异构 schema 只依赖「角色」而非字面列名。
/// </summary>
public static class MetadataDiscoveryHeuristics
{
	/// <summary>表名疑似字典表的提示词（不区分大小写，子串匹配）。</summary>
	private static readonly string[] DictionaryNameHints =
	{
		"dict", "enum", "lookup", "codedict", "code_table", "base_code", "sys_code",
		"dictionary", "字典", "码表"
	};

	/// <summary>码值（code）角色列提示词，按优先级排列。</summary>
	private static readonly string[] StrongCodeHints = { "code", "编码", "代码" };
	private static readonly string[] WeakCodeHints = { "key", "value", "val", "id" };

	/// <summary>名称（name）角色列提示词，按优先级排列。</summary>
	private static readonly string[] StrongNameHints = { "name", "label", "title", "caption", "名称" };
	private static readonly string[] WeakNameHints = { "text", "desc", "remark", "val", "value", "名" };

	/// <summary>分类（type）角色列提示词。</summary>
	private static readonly string[] TypeHints =
	{
		"type", "category", "group", "class", "kind", "分类", "类型", "分组"
	};

	/// <summary>
	/// 审计 / 扩展 / 系统噪声列提示词：不计入字典表「有效列宽」。
	///
	/// <para>
	/// 真实字典表普遍带大量此类列。实测 PMIS（JeeSite/JNPF 系）的 <c>js_sys_dict_data</c>
	/// 共 44 列，其中 <c>extend_s1..s8</c> / <c>extend_i1..i4</c> / <c>extend_f1..f4</c> /
	/// <c>extend_d1..d4</c> / <c>extend_json</c> / <c>create_*</c> / <c>update_*</c> /
	/// <c>tree_*</c> / <c>parent_*</c> / <c>corp_*</c> / <c>css_*</c> 等占绝大多数，
	/// 真正的语义列只有 <c>dict_code</c> / <c>dict_label</c> / <c>dict_value</c> /
	/// <c>dict_type</c> / <c>dict_icon</c> 5 列。
	/// 若按原始列数设上限，这类字典表会被永久判为「业务表」而无法自动发现。
	/// </para>
	/// </summary>
	private static readonly string[] NoiseColumnHints =
	{
		"extend", "create", "created", "update", "updated", "remark", "description",
		"is_sys", "is_deleted", "del_flag", "deleted", "corp", "tenant", "css_",
		"tree_", "parent_", "sort", "version", "revision", "level", "leaf",
	};

	/// <summary>
	/// 判断表名是否疑似字典/码表候选。
	/// 仅按表名特征做粗筛，最终是否建配置由 <see cref="TryResolveDictionaryRoles"/> 决定。
	/// </summary>
	public static bool IsDictionaryCandidate(string? tableName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
			return false;

		return DictionaryNameHints.Any(hint =>
			tableName.Contains(hint, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// 剔除审计/扩展/系统噪声列后的「语义列」。
	///
	/// <para>
	/// 角色解析与宽度闸门都必须跑在语义列上：JeeSite 系字典表的
	/// <c>parent_codes</c> 会命中 code 角色、<c>tree_names</c> 会命中 name 角色，
	/// 若在原始列集上解析就会选中这两列，得到完全错误的角色映射。
	/// </para>
	/// </summary>
	public static IReadOnlyList<ColumnMetadataDto> SemanticColumns(
		IReadOnlyList<ColumnMetadataDto>? columns)
	{
		if (columns is null || columns.Count == 0)
			return Array.Empty<ColumnMetadataDto>();

		return columns
			.Where(c => !string.IsNullOrWhiteSpace(c.ColumnName)
				&& !MatchesAny(c.ColumnName, NoiseColumnHints))
			.ToList();
	}

	/// <summary>
	/// 字典表的「有效列宽」：<see cref="SemanticColumns"/> 的列数。
	/// 用于宽度闸门 —— 直接按原始列数设限会把带 <c>extend_*</c> 扩展列的
	/// 真实字典表（如 PMIS <c>js_sys_dict_data</c>，44 列）误判为业务表。
	/// </summary>
	public static int EffectiveDictionaryWidth(IReadOnlyList<ColumnMetadataDto>? columns)
		=> SemanticColumns(columns).Count;

	/// <summary>
	/// 从候选表的列中解析字典「角色映射」：哪列承载 code、哪列承载 name、哪列（可选）承载分类。
	/// 找不到 code 或 name 角色时返回 false（宁可不建配置，也不误配）。
	/// </summary>
	public static bool TryResolveDictionaryRoles(
		IReadOnlyList<ColumnMetadataDto>? columns,
		out string codeColumn,
		out string nameColumn,
		out string? typeColumn)
	{
		codeColumn = string.Empty;
		nameColumn = string.Empty;
		typeColumn = null;

		if (columns is null || columns.Count < 2)
			return false;

		var usable = columns
			.Where(c => !string.IsNullOrWhiteSpace(c.ColumnName))
			.ToList();
		if (usable.Count < 2)
			return false;

		// 1) name 角色：先强提示（name/label/…），再弱提示（text/desc/val/…）。
		var name = FirstByHints(usable, StrongNameHints)
			?? FirstByHints(usable, WeakNameHints);

		// 2) code 角色：排除已选 name 列后，先强提示（code/编码），再弱提示（key/value/id）。
		var codePool = usable
			.Where(c => !SameColumn(c, name))
			.ToList();
		var code = FirstByHints(codePool, StrongCodeHints)
			?? FirstByHints(codePool, WeakCodeHints);

		if (name is null || code is null)
			return false;

		// 3) type 角色（可选）：在剩余列中找分类提示词；排除 code/name。
		var type = usable
			.FirstOrDefault(c => !SameColumn(c, code) && !SameColumn(c, name)
				&& MatchesAny(c.ColumnName, TypeHints));

		codeColumn = code.ColumnName!;
		nameColumn = name.ColumnName!;
		typeColumn = type?.ColumnName;
		return true;
	}

	/// <summary>
	/// 为外键的「被引用表」优选展示列（用于结果译码写 <c>{col}_name</c>）。
	/// 优先名称型列（非主键优先），其次被引用主键列，最后回退到 FK 声明的被引用列。
	/// </summary>
	public static string? PickDisplayColumn(
		IReadOnlyDictionary<string, List<ColumnMetadataDto>>? columnsByTable,
		string? referencedTable,
		string? referencedColumn)
	{
		if (columnsByTable is not null
			&& !string.IsNullOrWhiteSpace(referencedTable)
			&& columnsByTable.TryGetValue(referencedTable, out var cols)
			&& cols.Count > 0)
		{
			var named = cols
				.Where(c => !string.IsNullOrWhiteSpace(c.ColumnName) && MatchesAny(c.ColumnName, StrongNameHints))
				// 非主键优先（主键多为 id，名称型权重更高）
				.OrderByDescending(c => c.IsPrimaryKey ? 0 : 1)
				.ToList();
			if (named.Count > 0)
				return named[0].ColumnName;

			var pk = cols.FirstOrDefault(c => c.IsPrimaryKey && !string.IsNullOrWhiteSpace(c.ColumnName));
			if (pk is not null)
				return pk.ColumnName;
		}

		return referencedColumn;
	}

	/// <summary>
	/// 注释图例中的「码值」token：独立数字。
	/// 前后紧邻数字者不匹配，规避年份（<c>2024</c>）与长数值。
	/// </summary>
	private static readonly Regex LegendCodeRegex =
		new(@"(?<!\d)(?<code>\d{1,3})(?!\d)", RegexOptions.Compiled);

	/// <summary>
	/// 图例 JSON 序列化选项：不转义非 ASCII。
	/// 默认编码器会把中文写成 <c>\u5DF2\u521B\u5EFA</c>，
	/// 令 <c>MetadataColumn.ValueMapJson</c> 在 SQL 里不可读、难以人工复核。
	/// </summary>
	private static readonly JsonSerializerOptions LegendJsonOptions = new()
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	/// <summary>图例标签的修剪字符：分隔符 / 括号 / 空白。</summary>
	private const string LegendTrimChars = " \t\r\n-－:：=＝()（）,，、;；。";

	/// <summary>超长注释视为说明文本而非图例，不解析。</summary>
	private const int LegendCommentMaxLength = 400;

	/// <summary>图例条目数下限（单条不足以构成映射）。</summary>
	private const int LegendMinPairs = 2;

	/// <summary>图例条目数上限（防备散文被误切）。</summary>
	private const int LegendMaxPairs = 40;

	/// <summary>首个码值必须出现在注释前部，避免句子中段的数字被误判为图例。</summary>
	private const int LegendPrefixMaxLength = 20;

	/// <summary>单个图例标签的最大长度（超长说明是散文续写，不是标签）。</summary>
	private const int LegendLabelMaxLength = 12;

	/// <summary>
	/// 从列注释中的「码值图例」解析枚举映射，产出可直接写入
	/// <c>MetadataColumn.ValueMapJson</c> 的扁平 JSON（<c>{"0":"否","1":"是"}</c>）。
	///
	/// <para>
	/// 实测 WMS 注释广泛自带图例（<c>是否删除标识：0否 1是</c>、
	/// <c>状态 0-已创建 1-执行中 2-已完成…</c>、
	/// <c>单据来源类型（0：生产 1：采购地磅 2采购入库）</c>），
	/// 是零成本、天然随租户 schema 变化的译码来源；此前无任何写入方，该层长期静默。
	/// </para>
	///
	/// <para>
	/// 接受条件（宁缺勿滥）：≥2 个码值；首个码值位于注释前 20 字符内；
	/// 码值自 0 或 1 起严格递增且步长 ≤3（容忍图例缺号，实测存在 <c>0,1,3</c>）；
	/// 标签非空、≤12 字符、不含数字；注释 ≤400 字符。
	/// 逐条数字散文（如 <c>类型 0：当前所有数据…145仓库…445 仓库数据</c>）会被拒绝。
	/// </para>
	///
	/// <para>
	/// 调用方必须仅在 <c>ValueMapJson</c> 为空时写入 —— 管理侧声明与学习规则优先级更高。
	/// </para>
	/// </summary>
	public static bool TryParseValueMapFromComment(string? comment, out string json)
	{
		json = string.Empty;

		if (string.IsNullOrWhiteSpace(comment) || comment.Length > LegendCommentMaxLength)
			return false;

		var text = NormalizeLegendText(comment);
		var matches = LegendCodeRegex.Matches(text);
		if (matches.Count < LegendMinPairs || matches.Count > LegendMaxPairs)
			return false;
		if (matches[0].Index > LegendPrefixMaxLength)
			return false;

		var pairs = new List<(int Code, string Label)>(matches.Count);
		for (var i = 0; i < matches.Count; i++)
		{
			var start = matches[i].Index + matches[i].Length;
			var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
			var label = text[start..end].Trim(LegendTrimChars.ToCharArray());
			if (!IsLegendLabel(label))
				return false;

			if (!int.TryParse(matches[i].Groups["code"].Value, out var code))
				return false;
			if (pairs.Any(p => p.Code == code))
				return false;

			pairs.Add((code, label));
		}

		// 码值必须自成序列：起始 0/1，严格递增且步长有界。
		if (pairs[0].Code is not (0 or 1))
			return false;
		for (var i = 1; i < pairs.Count; i++)
		{
			var step = pairs[i].Code - pairs[i - 1].Code;
			if (step is < 1 or > 3)
				return false;
		}

		var map = pairs
			.OrderBy(p => p.Code)
			.ToDictionary(p => p.Code.ToString(), p => p.Label);
		json = JsonSerializer.Serialize(map, LegendJsonOptions);
		return true;
	}

	/// <summary>
	/// 归一化图例文本：全角数字转半角、全角冒号/等号转半角，
	/// 使 <c>0：生产</c> 与 <c>0:生产</c> 走同一套解析。
	/// </summary>
	private static string NormalizeLegendText(string comment)
	{
		if (!comment.Any(ch => ch is >= '\uFF10' and <= '\uFF19' or '\uFF1A' or '\uFF1D'))
			return comment;

		var buffer = new char[comment.Length];
		for (var i = 0; i < comment.Length; i++)
		{
			buffer[i] = comment[i] switch
			{
				>= '\uFF10' and <= '\uFF19' => (char)(comment[i] - 0xFEE0),
				'\uFF1A' => ':',
				'\uFF1D' => '=',
				var ch => ch
			};
		}

		return new string(buffer);
	}

	/// <summary>
	/// 图例标签有效性：非空、长度受限、不含数字。
	/// 含数字说明是散文续写（如 <c>145仓库当前数据</c>），不是枚举标签。
	/// </summary>
	private static bool IsLegendLabel(string label)
		=> !string.IsNullOrWhiteSpace(label)
			&& label.Length <= LegendLabelMaxLength
			&& !label.Any(char.IsDigit);

	private static ColumnMetadataDto? FirstByHints(
		IEnumerable<ColumnMetadataDto> columns,
		IReadOnlyList<string> hints)
		=> columns.FirstOrDefault(c => MatchesAny(c.ColumnName, hints));

	private static bool MatchesAny(string? value, IReadOnlyList<string> hints)
	{
		if (string.IsNullOrWhiteSpace(value))
			return false;

		foreach (var hint in hints)
		{
			if (value.Contains(hint, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	private static bool SameColumn(ColumnMetadataDto? a, ColumnMetadataDto? b)
		=> a is not null && b is not null
			&& string.Equals(a.ColumnName, b.ColumnName, StringComparison.OrdinalIgnoreCase);
}
