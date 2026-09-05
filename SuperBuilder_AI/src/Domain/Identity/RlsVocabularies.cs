namespace SuperBuilder_AI.Models.Identity;

/// <summary>
/// RLS 受控运算符词表（M1-06）。
///
/// 仅允许已知集合，杜绝 Operator 注入；下游 <see cref="RlsVocabularyValidator.NormalizeToSymbol"/>
/// 将其规范化为 SQL AST 运算符，配合参数化 Value 拼接，避免拼接式 SQL 注入。
///
/// 必须与 <c>RowLevelSecurityController.Save</c> 的校验、以及 <c>SuperBIContext</c> 中
/// <c>CK_RlsPolicies_Operator</c> 的 CHECK 保持一致。
/// </summary>
public static class RlsVocabularyValidator
{
	/// <summary>允许的运算符令牌（符号与枚举名均可，大小写不敏感）。</summary>
	public static IReadOnlySet<string> AllowedOperators { get; } =
		new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"=", "==", "!=", "<>", ">", ">=", "<", "<=",
			"LIKE", "IN", "IS NULL", "IS NOT NULL",
			"EQUALS", "NOTEQUALS", "GREATERTHAN", "GREATEROREQUAL",
			"LESSTHAN", "LESSOREQUAL", "CONTAINS"
		};

	/// <summary>判断给定运算符是否在受控词表内（大小写不敏感）。</summary>
	public static bool IsValidOperator(string? raw) =>
		!string.IsNullOrWhiteSpace(raw) && AllowedOperators.Contains(raw.Trim());

	/// <summary>
	/// 将受控运算符规范化为 SQL AST 符号（用于下游参数化拼接）。
	/// 不在词表内时抛出异常，调用方应先行 <see cref="IsValidOperator"/> 校验。
	/// </summary>
	public static string NormalizeToSymbol(string raw) => raw.Trim().ToUpperInvariant() switch
	{
		"=" or "==" or "EQUALS" => "=",
		"!=" or "<>" or "NOTEQUALS" => "!=",
		">" or "GREATERTHAN" => ">",
		">=" or "GREATEROREQUAL" => ">=",
		"<" or "LESSTHAN" => "<",
		"<=" or "LESSOREQUAL" => "<=",
		"LIKE" => "LIKE",
		"IN" => "IN",
		"IS NULL" => "IS NULL",
		"IS NOT NULL" => "IS NOT NULL",
		"CONTAINS" => "LIKE",
		_ => throw new ArgumentOutOfRangeException(nameof(raw), raw, "不受支持的 RLS 运算符。")
	};
}
