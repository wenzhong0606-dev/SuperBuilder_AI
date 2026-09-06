using System.Collections.Generic;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 内置 PII 列名启发目录（M5-08 真实治理基线）。
/// 用于在没有显式敏感度标注时，按物理列名（来自 M5-01/02 规范化语义模型）识别常见敏感字段。
/// 全部以小写存储，匹配时统一小写化后再比对。
/// </summary>
public static class BuiltInPiiCatalog
{
	private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
	{
		// 身份标识
		"id_card", "idcard", "id_no", "idno", "id_number", "idnumber", "id_num",
		"national_id", "ssn", "social_security", "passport", "passport_no", "passportno",
		// 凭证 / 密钥
		"password", "passwd", "pwd", "token", "secret", "apikey", "api_key",
		"oauth", "session", "salt", "hash", "certificate",
		// 联系方式
		"phone", "mobile", "mobilephone", "tel", "telephone", "email", "mail",
		// 金融
		"bank_account", "bankaccount", "account_no", "bank_no", "card_no", "cardno",
		"credit_card", "creditcard", "cvv", "salary", "wage", "income",
		// 个人属性
		"dob", "birthdate", "birthday", "address", "home_address", "ip_address", "ipaddr",
		"gender", "marital_status", "ethnicity", "nationality"
	};

	/// <summary>判断给定的小写列名是否属于内置 PII 集合。</summary>
	public static bool Contains(string? lowerColumnName) =>
		!string.IsNullOrWhiteSpace(lowerColumnName) && Names.Contains(lowerColumnName!);
}
