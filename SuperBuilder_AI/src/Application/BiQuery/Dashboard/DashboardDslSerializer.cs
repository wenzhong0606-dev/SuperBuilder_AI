using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.Dashboard;

namespace SuperBuilder_AI.Services.BI.Dashboard;

/// <summary>
/// <see cref="IDashboardDslSerializer"/> 的默认实现（P6 Low-code BI Engine）。
///
/// 实现要点：
/// <list type="number">
/// <item>组件多态由 <see cref="WidgetDsl"/> 上的 <c>[JsonPolymorphic]</c> 声明驱动，
///       序列化器只负责提供一致的 <see cref="JsonSerializerOptions"/>。</item>
/// <item><strong>先校验后信任</strong>：<see cref="TryDeserialize"/> 反序列化成功后必须
///       再跑一遍 <see cref="Validate"/>，因为 JSON 能表达的类型合法组合未必是业务合法的
///       （例如 Chart 组件没有指标字段、页面 Id 重复）。</item>
/// <item><strong>红线硬校验</strong>：任何文本字段出现 HTML 标签或脚本片段一律拒绝，
///       从源头保证"底层不存裸 HTML"。</item>
/// </list>
/// </summary>
public sealed class DashboardDslSerializer : IDashboardDslSerializer
{
	/// <summary>统一的序列化选项：camelCase、大小写不敏感、忽略null 以缩小体积。</summary>
	private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	/// <summary>HTML 标签或脚本片段的检测模式（P6 红线：DSL 不得承载标记语言）。</summary>
	private static readonly Regex HtmlPattern = new(
		@"<\s*/?\s*(script|iframe|style|div|span|table|img|a|p|br|h[1-6])\b|<\s*!\s*doctype|javascript\s*:",
		RegexOptions.IgnoreCase | RegexOptions.Compiled,
		TimeSpan.FromMilliseconds(100));

	/// <inheritdoc />
	public string Serialize(DashboardDsl dsl)
	{
		ArgumentNullException.ThrowIfNull(dsl);
		return JsonSerializer.Serialize(dsl, Options);
	}

	/// <inheritdoc />
	public bool TryDeserialize(string? json, out DashboardDsl? dsl, out IReadOnlyList<string> errors)
	{
		dsl = null;

		if (string.IsNullOrWhiteSpace(json))
		{
			errors = new[] { "DSL JSON 不能为空。" };
			return false;
		}

		DashboardDsl? parsed;
		try
		{
			parsed = JsonSerializer.Deserialize<DashboardDsl>(json, Options);
		}
		catch (JsonException ex)
		{
			errors = new[] { $"DSL JSON 解析失败：{ex.Message}" };
			return false;
		}
		catch (NotSupportedException ex)
		{
			// 多态分型遇到未知 type 时会抛此异常。
			errors = new[] { $"DSL 含不支持的组件类型：{ex.Message}" };
			return false;
		}

		if (parsed is null)
		{
			errors = new[] { "DSL JSON 反序列化结果为空。" };
			return false;
		}

		// 版本兼容门禁（M9-09）：先拦截不受支持的版本，避免对其做迁移假设。
		if (!DslVersions.Supported.Contains(parsed.Version))
		{
			errors = new[] { $"不支持的 DSL 版本：{parsed.Version}（受支持：{string.Join(", ", DslVersions.Supported)}）。" };
			return false;
		}

		// 旧版本归一化为当前版本（M9-09：旧 DSL 可加载 / 升级）。
		Upgrade(parsed);

		// 升级后做完整业务校验（先校验后信任）。
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

	/// <summary>
	/// 将任意<strong>受支持</strong>版本的 DSL 归一化为 <see cref="DslVersions.Current"/>（M9-09）。
	/// 在 <see cref="TryDeserialize"/> 中于校验前调用，使历史仪表盘在加载时自动升级，
	/// 无需手工数据迁移；调用方须先通过 <see cref="DslVersions.Supported"/> 白名单。
	/// </summary>
	/// <remarks>
	/// 当前仅有 V1 且 <see cref="DslVersions.V1"/> == <see cref="DslVersions.Current"/>，故 V1 分支即现状同构；
	/// 但结构归一化（缺省布局物化）对<strong>所有受支持版本</strong>（含当前）都应执行，
	/// 以保证内存模型完整、渲染器无需判空——故此处<strong>不做</strong>「版本==Current 即跳过」的早返回。
	/// 未来新增 V2 时，在此 <c>switch</c> 注册 <c>UpgradeFromV1</c> 等转换
	/// （字段重命名 / 默认值补全 / 结构迁移），并同步把新版本加入 <see cref="DslVersions.Supported"/>。
	/// </remarks>
	private static void Upgrade(DashboardDsl dsl)
	{
		switch (dsl.Version)
		{
			case DslVersions.V1:
				NormalizeV1(dsl);
				break;

			default:
				// 防御性分支：白名单已拦截未知版本，理论上不可达。
				throw new InvalidOperationException($"未注册 DSL 升级路径：{dsl.Version}");
		}

		dsl.Version = DslVersions.Current;
	}

	/// <summary>V1 / 当前版本的结构归一化。</summary>
	/// <remarks>
	/// 当前 V1 与 Current 同构，此处做一项向前兼容的默认值物化：将缺失（<c>null</c>）
	/// 的页面布局补全为默认 <see cref="LayoutKinds.Grid"/>（12 栅格），使内存模型始终完整、
	/// 渲染器（<see cref="DashboardLowcodeRenderer"/>）无需再判空。未来 V2 演进时在此追加字段迁移。
	/// </remarks>
	private static void NormalizeV1(DashboardDsl dsl)
	{
		foreach (var page in dsl.Pages)
		{
			page.Layout ??= new LayoutDsl();
		}
	}

	/// <inheritdoc />
	public IReadOnlyList<string> Validate(DashboardDsl dsl)
	{
		ArgumentNullException.ThrowIfNull(dsl);

		var errors = new List<string>();

		// 1. 版本
		if (!DslVersions.Supported.Contains(dsl.Version))
			errors.Add($"不支持的 DSL 版本：{dsl.Version}（受支持：{string.Join(", ", DslVersions.Supported)}）。");

		// 2. 标题
		if (string.IsNullOrWhiteSpace(dsl.Title))
			errors.Add("仪表盘标题不能为空。");

		// 3. 页面
		if (dsl.Pages.Count == 0)
			errors.Add("仪表盘至少需要一个页面。");

		var pageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var page in dsl.Pages)
		{
			ValidatePage(page, pageIds, errors);
		}

		// 4. 全局筛选器
		foreach (var filter in dsl.GlobalFilters)
		{
			ValidateFilter("全局筛选器", filter, errors);
		}

		return errors;
	}

	private static void ValidatePage(PageDsl page, HashSet<string> pageIds, List<string> errors)
	{
		if (string.IsNullOrWhiteSpace(page.Id))
		{
			errors.Add("页面 Id 不能为空。");
		}
		else if (!pageIds.Add(page.Id))
		{
			errors.Add($"页面 Id 重复：{page.Id}。");
		}

		// 布局
		if (page.Layout is not null)
		{
			if (!string.Equals(page.Layout.Kind, LayoutKinds.Grid, StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(page.Layout.Kind, LayoutKinds.Flow, StringComparison.OrdinalIgnoreCase))
			{
				errors.Add($"页面 {page.Id} 的布局方式不受支持：{page.Layout.Kind}。");
			}
			if (page.Layout.Columns <= 0)
				errors.Add($"页面 {page.Id} 的栅格列数必须大于 0。");
		}

		var widgetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var widget in page.Widgets)
		{
			ValidateWidget(page.Id, widget, widgetIds, errors);
		}
	}

	private static void ValidateWidget(
		string pageId,
		WidgetDsl widget,
		HashSet<string> widgetIds,
		List<string> errors)
	{
		var where = $"页面 {pageId} 的组件";

		if (string.IsNullOrWhiteSpace(widget.Id))
			errors.Add($"{where} Id 不能为空。");
		else if (!widgetIds.Add(widget.Id))
			errors.Add($"{where} Id 重复：{widget.Id}。");

		if (!WidgetTypes.Supported.Contains(widget.Type))
			errors.Add($"{where} {widget.Id} 类型不受支持：{widget.Type}。");

		// 类型专属校验
		switch (widget)
		{
			case ChartWidgetDsl chart:
				if (!ChartTypes.Supported.Contains(chart.ChartType))
					errors.Add($"{where} {widget.Id} 图表类型不受支持：{chart.ChartType}。");
				break;

			case TextWidgetDsl text:
				// P6 红线：文本组件不得承载裸 HTML / 脚本。
				if (ContainsHtml(text.Content))
					errors.Add($"{where} {widget.Id} 文本内容含 HTML 标记或脚本，DSL 只允许结构化文本。");
				break;

			case FilterWidgetDsl filter:
				if (string.IsNullOrWhiteSpace(filter.Field))
					errors.Add($"{where} {widget.Id} 筛选器未绑定字段。");
				break;
		}

		// 取数校验
		if (widget.Query is not null)
		{
			ValidateQuery($"{where} {widget.Id}", widget.Query, errors);
		}
		else if (widget is not TextWidgetDsl and not FilterWidgetDsl)
		{
			// 除文本与筛选器外，其余组件必须定义取数，否则渲染时无从取数。
			errors.Add($"{where} {widget.Id} 未定义取数（Query）。");
		}
	}

	private static void ValidateQuery(string where, WidgetQueryDsl query, List<string> errors)
	{
		foreach (var metric in query.Metrics)
		{
			if (string.IsNullOrWhiteSpace(metric.Field))
				errors.Add($"{where} 的指标未指定字段。");
			if (!AggregateTypes.Supported.Contains(metric.Aggregation))
				errors.Add($"{where} 的聚合方式不受支持：{metric.Aggregation}。");
		}

		foreach (var filter in query.Filters)
		{
			ValidateFilter(where, filter, errors);
		}

		foreach (var sort in query.Sorts)
		{
			if (string.IsNullOrWhiteSpace(sort.Field))
				errors.Add($"{where} 的排序未指定字段。");
			if (!string.Equals(sort.Direction, "ASC", StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(sort.Direction, "DESC", StringComparison.OrdinalIgnoreCase))
			{
				errors.Add($"{where} 的排序方向不受支持：{sort.Direction}。");
			}
		}

		if (query.Limit is <= 0)
		{
			// Limit 为正整数或 null；此处仅拦截显式传入的非正值。
			if (query.Limit.HasValue)
				errors.Add($"{where} 的 Limit 必须大于 0。");
		}
	}

	private static void ValidateFilter(string where, FilterDsl filter, List<string> errors)
	{
		if (string.IsNullOrWhiteSpace(filter.Field))
			errors.Add($"{where} 的筛选条件未指定字段。");
		if (!FilterOperators.Supported.Contains(filter.Operator))
			errors.Add($"{where} 的筛选操作符不受支持：{filter.Operator}。");
	}

	/// <summary>检测文本中是否混入 HTML 标签或脚本片段（P6 红线）。</summary>
	private static bool ContainsHtml(string? text)
		=> !string.IsNullOrWhiteSpace(text) && HtmlPattern.IsMatch(text);
}
