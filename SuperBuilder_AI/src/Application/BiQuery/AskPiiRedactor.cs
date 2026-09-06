using System.Text.RegularExpressions;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// Ask 审计脱敏工具（M6-05）：在审计记录落库前，对敏感字段做最小化脱敏。
///
/// <para>复用 <see cref="BuiltInPiiCatalog"/> 的内置 PII 列名启发目录判定敏感列/关键词；
/// 不引入新配置时即按内置目录工作。可选传入 <paramref name="restrictedColumns"/>> 扩展受限列（来自列安全配置）。</para>
/// </summary>
public sealed class AskPiiRedactor
{
	private static readonly AskPiiRedactor DefaultInstance = new();

	/// <summary>默认实例（无额外受限列）。</summary>
	public static AskPiiRedactor Default => DefaultInstance;

	private static readonly Regex WordToken = new(@"\b[\p{L}\p{N}_]+\b", RegexOptions.Compiled);
	private static readonly Regex EmailPattern = new(@"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	private static readonly Regex PhonePattern = new(@"(?<!\d)(?:\+?\d[\d\s-]{6,}\d)(?!\d)", RegexOptions.Compiled);
	private const string Mask = "***";
	private const int SampleValueMax = 32;

	private readonly HashSet<string> _extra;

	/// <summary>创建脱敏工具；可选传入额外受限列名（大小写不敏感）。</summary>
	public AskPiiRedactor(IEnumerable<string>? restrictedColumns = null)
	{
		_extra = new HashSet<string>(
			(restrictedColumns ?? Enumerable.Empty<string>()).Select(x => x.Trim().ToLowerInvariant()),
			StringComparer.Ordinal);
	}

	private bool IsPii(string lowerToken) =>
		BuiltInPiiCatalog.Contains(lowerToken) || _extra.Contains(lowerToken);

	/// <summary>对自由文本脱敏：掩码 PII 关键词、邮箱与电话。</summary>
	public string RedactText(string? text)
	{
		if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;
		var s = EmailPattern.Replace(text, Mask);
		s = PhonePattern.Replace(s, Mask);
		return WordToken.Replace(s, m => IsPii(m.Value.ToLowerInvariant()) ? Mask : m.Value);
	}

	/// <summary>对 SQL 文本脱敏：掩码其中的 PII 列名（保留结构，不保留参数值）。</summary>
	public string RedactSql(string? sql)
	{
		if (string.IsNullOrWhiteSpace(sql)) return sql ?? string.Empty;
		return WordToken.Replace(sql, m => IsPii(m.Value.ToLowerInvariant()) ? Mask : m.Value);
	}

	/// <summary>对查询结果样本脱敏：仅取首行，PII 列值置 <c>***</c>，非 PII 值截断。</summary>
	public string RedactResultSample(QueryResult? data)
	{
		if (data?.Rows is null || data.Rows.Count == 0) return "rows=0";
		var first = data.Rows[0];
		var parts = new List<string>(first.Count);
		foreach (var kv in first)
		{
			var key = kv.Key ?? "?";
			var masked = IsPii(key.ToLowerInvariant()) ? Mask : MaskValue(kv.Value);
			parts.Add($"{key}={masked}");
		}
		return $"rows={data.Rows.Count}; sample={{{string.Join(", ", parts)}}}";
	}

	private static string MaskValue(object? value)
	{
		if (value is null) return "null";
		var s = value.ToString() ?? string.Empty;
		return s.Length <= SampleValueMax ? s : s.Substring(0, SampleValueMax) + "…";
	}
}
