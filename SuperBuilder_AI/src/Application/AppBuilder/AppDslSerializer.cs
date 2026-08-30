using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Models.AppBuilder;

namespace SuperBuilder_AI.Services.AppBuilder;

/// <summary>
/// <see cref="IAppDslSerializer"/> 的默认实现（P8 AI App Builder）。
///
/// 实现要点（与 P6 <c>DashboardDslSerializer</c> 同源）：
/// <list type="number">
/// <item>提供一致的 <see cref="JsonSerializerOptions"/>（camelCase、大小写不敏感、忽略 null 以缩小体积）。</item>
/// <item><strong>先校验后信任</strong>：<see cref="TryDeserialize"/> 反序列化成功后必须再跑一遍
///       <see cref="Validate"/>，因为 JSON 能表达的类型合法组合未必是业务合法的（例如页面 Id 重复）。</item>
/// <item><strong>红线硬校验</strong>：任何文本字段出现 HTML 标签或脚本片段一律拒绝，从源头保证"底层不存裸 HTML"。</item>
/// </list>
/// </summary>
public sealed class AppDslSerializer : IAppDslSerializer
{
	/// <summary>统一的序列化选项：camelCase、大小写不敏感、忽略 null 以缩小体积。</summary>
	private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	/// <summary>HTML 标签或脚本片段的检测模式（P8 红线：DSL 不得承载标记语言）。</summary>
	private static readonly Regex HtmlPattern = new(
		@"<\s*/?\s*(script|iframe|style|div|span|table|img|a|p|br|h[1-6])\b|<\s*!\s*doctype|javascript\s*:",
		RegexOptions.IgnoreCase | RegexOptions.Compiled,
		TimeSpan.FromMilliseconds(100));

	/// <inheritdoc />
	public string Serialize(AppDsl dsl)
	{
		ArgumentNullException.ThrowIfNull(dsl);
		return JsonSerializer.Serialize(dsl, Options);
	}

	/// <inheritdoc />
	public bool TryDeserialize(string? json, out AppDsl? dsl, out IReadOnlyList<string> errors)
	{
		dsl = null;

		if (string.IsNullOrWhiteSpace(json))
		{
			errors = new[] { "应用 DSL JSON 不能为空。" };
			return false;
		}

		AppDsl? parsed;
		try
		{
			parsed = JsonSerializer.Deserialize<AppDsl>(json, Options);
		}
		catch (JsonException ex)
		{
			errors = new[] { $"应用 DSL JSON 解析失败：{ex.Message}" };
			return false;
		}

		if (parsed is null)
		{
			errors = new[] { "应用 DSL JSON 反序列化结果为空。" };
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

	/// <inheritdoc />
	public IReadOnlyList<string> Validate(AppDsl dsl)
	{
		ArgumentNullException.ThrowIfNull(dsl);

		var errors = new List<string>();

		// 1. 版本
		if (!AppDslVersions.Supported.Contains(dsl.Version))
			errors.Add($"不支持的应用 DSL 版本：{dsl.Version}（受支持：{string.Join(", ", AppDslVersions.Supported)}）。");

		// 2. 名称 + 红线
		if (string.IsNullOrWhiteSpace(dsl.Name))
			errors.Add("应用名称不能为空。");
		else if (ContainsHtml(dsl.Name))
			errors.Add("应用名称含 HTML 标记或脚本，DSL 只允许结构化文本。");

		if (ContainsHtml(dsl.Description))
			errors.Add("应用描述含 HTML 标记或脚本，DSL 只允许结构化文本。");

		// 3. 页面
		if (dsl.Pages.Count == 0)
			errors.Add("应用至少需要一个页面。");

		var pageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var page in dsl.Pages)
		{
			ValidatePage(page, pageIds, errors);
		}

		return errors;
	}

	private static void ValidatePage(PagePlan page, HashSet<string> pageIds, List<string> errors)
	{
		if (string.IsNullOrWhiteSpace(page.Id))
		{
			errors.Add("页面 Id 不能为空。");
		}
		else if (!pageIds.Add(page.Id))
		{
			errors.Add($"页面 Id 重复：{page.Id}。");
		}

		if (string.IsNullOrWhiteSpace(page.Name))
			errors.Add($"页面 {page.Id} 名称不能为空。");
		else if (ContainsHtml(page.Name))
			errors.Add($"页面 {page.Id} 名称含 HTML 标记或脚本。");

		// 布局
		if (page.Layout is not null)
		{
			if (!AppLayoutKinds.Supported.Contains(page.Layout.Kind))
				errors.Add($"页面 {page.Id} 的布局方式不受支持：{page.Layout.Kind}。");
			if (page.Layout.Columns <= 0)
				errors.Add($"页面 {page.Id} 的栅格列数必须大于 0。");
		}

		var componentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var component in page.Components)
		{
			ValidateComponent(page.Id, component, componentIds, errors);
		}
	}

	private static void ValidateComponent(
		string pageId,
		ComponentPlan component,
		HashSet<string> componentIds,
		List<string> errors)
	{
		var where = $"页面 {pageId} 的组件";

		if (string.IsNullOrWhiteSpace(component.Id))
			errors.Add($"{where} Id 不能为空。");
		else if (!componentIds.Add(component.Id))
			errors.Add($"{where} Id 重复：{component.Id}。");

		if (!AppComponentTypes.Supported.Contains(component.Type))
			errors.Add($"{where} {component.Id} 类型不受支持：{component.Type}。");

		// 取数校验：声明了 Binding 才校验内部结构（文本组件可不绑定）
		if (component.Binding is not null)
			ValidateBinding($"{where} {component.Id}", component.Binding, errors);

		// 红线：标题与属性值不得含 HTML
		if (ContainsHtml(component.Title))
			errors.Add($"{where} {component.Id} 标题含 HTML 标记或脚本。");
		foreach (var kv in component.Properties)
			if (ContainsHtml(kv.Value))
				errors.Add($"{where} {component.Id} 属性 {kv.Key} 含 HTML 标记或脚本。");
	}

	private static void ValidateBinding(string where, AppDataSourceBinding binding, List<string> errors)
	{
		foreach (var metric in binding.Metrics)
		{
			if (string.IsNullOrWhiteSpace(metric.Field))
				errors.Add($"{where} 的指标未指定字段。");
			if (!AppAggregateTypes.Supported.Contains(metric.Aggregation))
				errors.Add($"{where} 的聚合方式不受支持：{metric.Aggregation}。");
		}

		foreach (var filter in binding.Filters)
		{
			if (string.IsNullOrWhiteSpace(filter.Field))
				errors.Add($"{where} 的筛选条件未指定字段。");
			if (!AppFilterOperators.Supported.Contains(filter.Operator))
				errors.Add($"{where} 的筛选操作符不受支持：{filter.Operator}。");
		}

		if (binding.Limit is <= 0)
		{
			// Limit 为正整数或 null；此处仅拦截显式传入的非正值。
			if (binding.Limit.HasValue)
				errors.Add($"{where} 的 Limit 必须大于 0。");
		}
	}

	/// <summary>检测文本中是否混入 HTML 标签或脚本片段（P8 红线）。</summary>
	private static bool ContainsHtml(string? text)
		=> !string.IsNullOrWhiteSpace(text) && HtmlPattern.IsMatch(text);
}
