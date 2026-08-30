using System.Text.Json;
using System.Text.Json.Serialization;
using SuperBuilder_AI.Models.Theme;

namespace SuperBuilder_AI.Services.Theming;

/// <summary>
/// 主题 DSL 序列化 / 反序列化 / 校验助手（P7.4 Theme 管理端点）。
/// <para>
/// 与 <see cref="DashboardDslSerializer"/> 保持一致：camelCase、大小写不敏感、忽略 null 以缩小体积。
/// 主题 DSL 仅承载结构化设计令牌（色值/字号/间距），不承载任何 CSS 字符串或标记语言，
/// 故无需像 Dashboard DSL 那样做 HTML 红线拦截。
/// </para>
/// </summary>
public static class ThemeDslSerializer
{
	/// <summary>统一的序列化选项：camelCase、大小写不敏感、忽略 null 以缩小体积。</summary>
	private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	/// <summary>序列化 <see cref="ThemeDsl"/> 为 JSON 字符串。</summary>
	public static string Serialize(ThemeDsl dsl)
	{
		ArgumentNullException.ThrowIfNull(dsl);
		return JsonSerializer.Serialize(dsl, Options);
	}

	/// <summary>
	/// 反序列化并校验主题 DSL。
	/// </summary>
	/// <returns>成功且校验通过返回 <c>true</c>，<paramref name="dsl"/> 为解析结果。</returns>
	public static bool TryDeserialize(string? json, out ThemeDsl? dsl, out IReadOnlyList<string> errors)
	{
		dsl = null;
		errors = Array.Empty<string>();

		if (string.IsNullOrWhiteSpace(json))
		{
			errors = new[] { "主题 DSL JSON 不能为空。" };
			return false;
		}

		ThemeDsl? parsed;
		try
		{
			parsed = JsonSerializer.Deserialize<ThemeDsl>(json, Options);
		}
		catch (JsonException ex)
		{
			errors = new[] { $"主题 DSL JSON 解析失败：{ex.Message}" };
			return false;
		}

		if (parsed is null)
		{
			errors = new[] { "主题 DSL 反序列化结果为空。" };
			return false;
		}

		var validationErrors = Validate(parsed);
		if (validationErrors.Count > 0)
		{
			errors = validationErrors;
			return false;
		}

		dsl = parsed;
		errors = Array.Empty<string>();
		return true;
	}

	/// <summary>主题 DSL 业务校验（当前仅校验版本号白名单，令牌均有合理默认值）。</summary>
	public static IReadOnlyList<string> Validate(ThemeDsl dsl)
	{
		ArgumentNullException.ThrowIfNull(dsl);

		var errors = new List<string>();
		if (!ThemeDslVersions.Supported.Contains(dsl.Version))
			errors.Add($"不支持的主题 DSL 版本：{dsl.Version}（受支持：{string.Join(", ", ThemeDslVersions.Supported)}）。");
		return errors;
	}
}
