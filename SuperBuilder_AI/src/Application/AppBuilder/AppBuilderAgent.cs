using System.Globalization;
using System.Text.Json;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Models.AppBuilder;

namespace SuperBuilder_AI.Services.AppBuilder;

/// <summary>
/// <see cref="IAppBuilderAgent"/> 的默认实现（P8.2 AI App Builder）。
///
/// <para>编排两条路径：</para>
/// <list type="bullet">
/// <item><strong>默认路径</strong> <see cref="BuildFromDslAsync"/>：结构化 DSL → <see cref="AppPlan"/>，
///       纯确定性、不调用 LLM。这是 P8.3 控制器保存应用的主路径，行为与历史版本逐字节一致。</item>
/// <item><strong>非默认路径</strong> <see cref="GenerateFromDescriptionAsync"/>：自然语言描述 → 调 Qwen
///       生成 DSL JSON → 校验 → <see cref="AppPlan"/>。仅当调用方显式传入描述时才启用 LLM。</item>
/// </list>
///
/// <para>两条路径都复用 P8.1 的 <see cref="IAppDslSerializer"/> 做"先校验后信任"，并保证 DSL 红线
/// （绝不承载裸 HTML）。本服务不触碰任何 Golden 依赖文件，零回归风险。</para>
/// </summary>
public sealed class AppBuilderAgent : IAppBuilderAgent
{
	private readonly IAppDslSerializer _dslSerializer;
	private readonly IQwenService _qwenService;

	/// <summary>创建应用编排服务。</summary>
	public AppBuilderAgent(IAppDslSerializer dslSerializer, IQwenService qwenService)
	{
		_dslSerializer = dslSerializer;
		_qwenService = qwenService;
	}

	/// <inheritdoc />
	public Task<AppBuildResult> BuildFromDslAsync(long tenantId, AppDsl dsl, string? code = null)
	{
		ArgumentNullException.ThrowIfNull(dsl);

		// 默认路径：仅做结构化校验，不调用 LLM。
		var errors = _dslSerializer.Validate(dsl);
		if (errors.Count > 0)
			return Task.FromResult(AppBuildResult.Fail(errors));

		var dslJson = _dslSerializer.Serialize(dsl);
		var plan = ToPlan(tenantId, dsl, dslJson, code);
		return Task.FromResult(AppBuildResult.Ok(plan, dslJson));
	}

	/// <inheritdoc />
	public async Task<AppBuildResult> GenerateFromDescriptionAsync(long tenantId, string description, string? code = null)
	{
		if (string.IsNullOrWhiteSpace(description))
			return AppBuildResult.Fail(new[] { "应用描述不能为空。" });

		// 非默认路径：调用 LLM 生成 DSL JSON。
		var prompt = BuildPrompt(description);
		var response = await _qwenService.GenerateSqlAsync(prompt);
		var json = CleanJson(response);

		// 先校验后信任：LLM 可能产出非法 DSL（HTML、未知枚举、缺页面等），统一在此拦截。
		if (!_dslSerializer.TryDeserialize(json, out var dsl, out var errors))
			return AppBuildResult.Fail(errors, usedAi: true);

		var dslJson = _dslSerializer.Serialize(dsl!);
		var plan = ToPlan(tenantId, dsl!, dslJson, code);
		return AppBuildResult.Ok(plan, dslJson, usedAi: true);
	}

	/// <summary>把校验通过的 DSL 与租户信息组装为可持久化的 <see cref="AppPlan"/>。</summary>
	private static AppPlan ToPlan(long tenantId, AppDsl dsl, string dslJson, string? code)
		=> new()
		{
			TenantId = tenantId,
			Code = !string.IsNullOrWhiteSpace(code)
				? code
				: (!string.IsNullOrWhiteSpace(dsl.Code) ? dsl.Code : Slugify(dsl.Name)),
			Name = dsl.Name,
			Description = dsl.Description,
			Status = AppStatuses.Draft,
			DslVersion = dsl.Version,
			DslJson = dslJson,
			ThemeKey = dsl.ThemeKey,
		};

	/// <summary>把应用名称转换为可用作业务编码的 slug（小写、连字符分隔、去噪）。</summary>
	private static string Slugify(string name)
	{
		var src = string.IsNullOrWhiteSpace(name) ? "app" : name;
		var sb = new System.Text.StringBuilder(src.Length);
		foreach (var ch in src.ToLowerInvariant())
		{
			if (char.IsLetterOrDigit(ch))
				sb.Append(ch);
			else if (char.IsWhiteSpace(ch) || ch is '-' or '_')
			{
				if (sb.Length > 0 && sb[^1] != '-')
					sb.Append('-');
			}
		}

		var slug = sb.ToString().Trim('-');
		return string.IsNullOrEmpty(slug) ? "app" : slug;
	}

	/// <summary>清理 LLM 可能包裹的 Markdown 代码块标记。</summary>
	private static string CleanJson(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			throw new InvalidOperationException("AI 没有返回任何内容。");

		text = text.Trim();
		if (text.StartsWith("```"))
		{
			var firstLineEnd = text.IndexOf('\n');
			if (firstLineEnd >= 0)
				text = text.Substring(firstLineEnd + 1);

			var last = text.LastIndexOf("```");
			if (last >= 0)
				text = text.Substring(0, last);
		}

		return text.Trim();
	}

	/// <summary>构造 AppDsl 生成提示词（描述 AppDsl 的 JSON 结构约束，要求只返回合法 JSON）。</summary>
	private static string BuildPrompt(string description)
	{
		var componentTypes = string.Join(" / ", AppComponentTypes.Supported);
		var aggregations = string.Join(" / ", AppAggregateTypes.Supported);
		var operators = string.Join(" / ", AppFilterOperators.Supported);
		var layoutKinds = string.Join(" / ", AppLayoutKinds.Supported);

		return $$"""
            你是企业级低代码 BI 应用的架构 AI。

            你的任务是：将用户的自然语言应用描述，转换为一份严格的 AppDsl JSON。

            必须遵守的约束：

            1. 只能返回合法 JSON，禁止返回 Markdown、禁止 ```json、禁止 JSON 之外的解释。
            2. 应用至少需要一个页面（pages 数组非空），页面 Id 在同一应用内唯一。
            3. 组件类型（type）只能是：{{componentTypes}}。
            4. 取数聚合方式（aggregation）只能是：{{aggregations}}。
            5. 筛选操作符（operator）只能是：{{operators}}。
            6. 页面布局方式（layout.kind）只能是：{{layoutKinds}}；grid 布局默认 12 栅格。
            7. 文本组件（type=text）可不绑定取数（binding 为 null）。
            8. 组件标题（title）等文本字段不得包含 HTML 标签或脚本片段。
            9. 字段一律使用业务语义名（如 sales_order、amount、region），不要臆造不存在的字段。
            10. 若用户未要求主题，themeKey 留空（null）。

            AppDsl JSON 结构示例：

            {
              "version": "1.0",
              "code": "sales-board",
              "name": "销售看板",
              "description": "销售概览应用",
              "themeKey": null,
              "pages": [
                {
                  "id": "home",
                  "name": "首页",
                  "order": 1,
                  "layout": { "kind": "grid", "columns": 12, "rowHeight": 64, "gap": 12 },
                  "components": [
                    {
                      "type": "kpi",
                      "id": "kpi1",
                      "title": "总销售额",
                      "order": 1,
                      "binding": {
                        "entity": "sales_order",
                        "metrics": [ { "field": "amount", "aggregation": "sum" } ],
                        "dimensions": [ "region" ],
                        "filters": [],
                        "limit": null
                      },
                      "properties": { "format": "N2" },
                      "style": { "palette": "primary" }
                    },
                    {
                      "type": "chart",
                      "id": "chart1",
                      "title": "地区趋势",
                      "order": 2,
                      "binding": {
                        "entity": "sales_order",
                        "metrics": [ { "field": "amount", "aggregation": "sum" } ],
                        "dimensions": [ "region" ],
                        "filters": [],
                        "limit": null
                      },
                      "properties": { "chartType": "bar", "categoryField": "region" }
                    }
                  ]
                }
              ]
            }

            ============================================================
            用户描述
            ============================================================

            {{description}}

            ============================================================
            最终只返回 JSON
            ============================================================
            """;
	}
}
