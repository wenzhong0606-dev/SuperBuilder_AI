using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SuperBuilder_AI.Application.Common.Configuration;

/// <summary>
/// 统一 Dev/Test/Production 启动配置校验。
/// <list type="bullet">
///   <item>非开发环境（Production / Staging / 自定义托管）缺失关键配置 → 产出 <see cref="ConfigSeverity.Error"/>，由调用方 fail-fast 拒绝启动。</item>
///   <item>开发 / 测试环境 → 同类问题降级为 <see cref="ConfigSeverity.Warning"/>，不阻断启动。</item>
///   <item>校验信息仅携带配置键路径与原因，<b>绝不输出配置值（密钥）</b>，避免日志/异常泄漏敏感信息。</item>
/// </list>
/// 既有值返回型或已内联 fail-fast 的检查（Auth:SigningKey 强度、Cors 白名单、RateLimit:Redis、SecretStore:MasterKey）
/// 维持原样；本校验器集中覆盖此前<b>无启动校验</b>的关键配置：主库连接串、LLM/Embedding 密钥、向量库端点。
/// </summary>
public sealed class StartupConfigurationValidator
{
	// base appsettings.json 中用于标记「未替换」的占位符哨兵。仅匹配这些固定串，避免误伤合法值。
	private static readonly string[] PlaceholderTokens =
	{
		"__REQUIRED__",
		"__SET_VIA_ENV",
		"_OR_appsettings.Local.json__"
	};

	/// <summary>
	/// 执行校验。
	/// </summary>
	/// <param name="config">应用配置（已合并环境变量与 Local 覆盖后的最终视图）。</param>
	/// <param name="environment">宿主环境，用于判定是否严格模式。</param>
	public ConfigValidationReport Validate(IConfiguration config, IHostEnvironment environment)
	{
		var issues = new List<ConfigIssue>();
		// 严格模式：除 Development 与 Test 外的所有环境（Production / Staging / 自定义）均要求关键配置齐全。
		bool strict = !environment.IsDevelopment() && !IsTestEnvironment(environment);

		// 平台主库（必需）
		CheckConnectionString(issues, config, "ConnectionStrings:DefaultConnection", strict);
		CheckConnectionString(issues, config, "ConnectionStrings:SuperBI", strict);

		// LLM / Embedding 密钥（必需）
		CheckSecret(issues, config, "Qwen:ApiKey", strict);
		CheckSecret(issues, config, "Embedding:ApiKey", strict);

		// 向量库端点（必需）
		CheckRequired(issues, config, "Qdrant:Host", strict);

		// AllowedHosts 通配在生产视为不安全（仅告警，不阻断）
		var allowedHosts = config["AllowedHosts"];
		if (strict && string.Equals(allowedHosts?.Trim(), "*", System.StringComparison.Ordinal))
			issues.Add(new ConfigIssue(
				"AllowedHosts",
				"生产环境 AllowedHosts 为 '*'（接受任意主机头），建议限定为受信任域名。",
				ConfigSeverity.Warning));

		return new ConfigValidationReport(issues);
	}

	private static void CheckConnectionString(List<ConfigIssue> issues, IConfiguration config, string key, bool strict)
	{
		if (IsMissingOrPlaceholder(config[key]))
			AddMissing(issues, key, "数据库连接字符串缺失或为未替换的占位符", strict);
	}

	private static void CheckSecret(List<ConfigIssue> issues, IConfiguration config, string key, bool strict)
	{
		if (IsMissingOrPlaceholder(config[key]))
			AddMissing(issues, key, "API 密钥缺失或为未替换的占位符", strict);
	}

	private static void CheckRequired(List<ConfigIssue> issues, IConfiguration config, string key, bool strict)
	{
		if (string.IsNullOrWhiteSpace(config[key]))
			AddMissing(issues, key, "端点配置缺失", strict);
	}

	private static void AddMissing(List<ConfigIssue> issues, string key, string reason, bool strict)
	{
		var suffix = strict ? "（生产/预发环境禁止启动）" : "（开发/测试环境仅告警）";
		issues.Add(new ConfigIssue(key, reason + suffix, strict ? ConfigSeverity.Error : ConfigSeverity.Warning));
	}

	private static bool IsMissingOrPlaceholder(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return true;
		return PlaceholderTokens.Any(t => value.Contains(t, System.StringComparison.Ordinal));
	}

	private static bool IsTestEnvironment(IHostEnvironment environment)
		=> string.Equals(environment.EnvironmentName, "Test", System.StringComparison.OrdinalIgnoreCase);
}
