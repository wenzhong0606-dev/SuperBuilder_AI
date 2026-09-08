using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 业务术语的强类型封装，用于在查询计划链路中以编译期约束替代自由字符串。
///
/// 设计要点：
/// 1. 值类型（readonly struct），热路径（逐术语召回/评分）零分配；
/// 2. 构造即规范化：去首尾空白、折叠内部连续空白为单空格；
/// 3. 值相等以不区分大小写（OrdinalIgnoreCase）为准，与重构前
///    <c>Distinct(StringComparer.OrdinalIgnoreCase)</c> 的去重语义保持一致，
///    因此可作为 Dictionary 键与集合去重依据；
/// 4. 提供到 string 的隐式转换，下游（语义搜索、文本归一化、关联判断）无需改动即可消费；
/// 5. 配套 JSON 转换器，使 List&lt;BusinessTerm&gt; 在诊断端点序列化为纯 string[]，
///    保持与既有 List&lt;string&gt; 线格式兼容。
/// </summary>
[JsonConverter(typeof(BusinessTermJsonConverter))]
public readonly struct BusinessTerm : IEquatable<BusinessTerm>
{
	private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

	/// <summary>规范化后的术语文本（永不为 null）。</summary>
	public string Value { get; }

	public BusinessTerm(string? value)
	{
		Value = Normalize(value);
	}

	private static string Normalize(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;
		return Whitespace.Replace(value.Trim(), " ").Trim();
	}

	/// <summary>从原始文本创建业务术语（null/空白归一为空术语）。</summary>
	public static BusinessTerm Create(string? raw) => new BusinessTerm(raw);

	/// <summary>空术语（Value 为空字符串）。</summary>
	public static readonly BusinessTerm Empty = new BusinessTerm(string.Empty);

	/// <summary>是否为空术语（Value 为空）。</summary>
	public bool IsEmpty => string.IsNullOrEmpty(Value);

	public bool Equals(BusinessTerm other)
		=> string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

	public override bool Equals(object? obj)
		=> obj is BusinessTerm other && Equals(other);

	public override int GetHashCode()
		=> Value == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

	public override string ToString() => Value ?? string.Empty;

	/// <summary>隐式转换为 string，使下游 string 形参可无缝消费业务术语。</summary>
	public static implicit operator string(BusinessTerm term) => term.Value ?? string.Empty;
}

/// <summary>
/// 使 <see cref="BusinessTerm"/> 在 JSON 中序列化为纯字符串（而非 { "Value": "..." }），
/// 从而 List&lt;BusinessTerm&gt; 与既有 List&lt;string&gt; 诊断线格式保持兼容。
/// </summary>
public sealed class BusinessTermJsonConverter : JsonConverter<BusinessTerm>
{
	public override BusinessTerm Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options)
		=> new BusinessTerm(reader.GetString());

	public override void Write(
		Utf8JsonWriter writer,
		BusinessTerm value,
		JsonSerializerOptions options)
		=> writer.WriteStringValue(value.Value);
}
