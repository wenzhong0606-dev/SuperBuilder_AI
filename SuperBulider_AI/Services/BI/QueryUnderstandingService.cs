using System.Text.Json;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Services.BI;

/// <summary>
/// 查询理解服务。
///
/// Phase 1.5.1
///
/// 负责将用户自然语言查询转换为结构化 QueryIntent。
///
/// 流程:
///
/// 用户问题
///     ↓
/// MetadataContext
///     ↓
/// Qwen
///     ↓
/// QueryIntent
///
/// 注意:
///
/// QueryIntent 是后续 QueryPlanBuilder 的输入。
/// </summary>
public class QueryUnderstandingService
	: IQueryUnderstandingService
{
	private readonly IMetadataContextBuilder _contextBuilder;

	private readonly IQwenService _qwenService;

	/// <summary>
	/// 创建查询理解服务。
	/// </summary>
	public QueryUnderstandingService(
		IMetadataContextBuilder contextBuilder,
		IQwenService qwenService)
	{
		_contextBuilder = contextBuilder;

		_qwenService = qwenService;
	}

	/// <summary>
	/// 理解用户查询意图。
	/// </summary>
	public async Task<QueryIntent> UnderstandAsync(
		string question)
	{
		if (string.IsNullOrWhiteSpace(question))
		{
			throw new ArgumentException(
				"用户问题不能为空。",
				nameof(question));
		}

		/*
		 * ============================================================
		 * 1.
		 * 获取Metadata上下文
		 *
		 * MetadataContext的作用:
		 *
		 * 让AI知道当前系统真实存在的:
		 *
		 * Table
		 * Column
		 * Semantic
		 *
		 * 从而避免AI凭空创造数据库字段。
		 * ============================================================
		 */

		var metadataContext =
			await _contextBuilder
				.BuildAsync(question);

		/*
		 * ============================================================
		 * 2.
		 * 构造QueryIntent Prompt
		 *
		 * 这里必须明确告诉AI:
		 *
		 * Metrics不是string数组。
		 *
		 * 而是:
		 *
		 * QueryMetric[]
		 *
		 * 每一个Metric必须包含:
		 *
		 * Name
		 * Field
		 * Aggregation
		 *
		 * Filters同样必须是:
		 *
		 * QueryFilter[]
		 * ============================================================
		 */

		var prompt =
			$$"""
			你是企业级 BI 查询理解 AI。

			你的任务是：

			将用户的自然语言查询转换为严格的 QueryIntent JSON。

			你必须根据提供的 Metadata 知识理解用户使用的业务名称，
			并尽可能映射到真实的 Metadata 字段。

			============================================================
			Metadata知识
			============================================================

			{{metadataContext}}

			============================================================
			用户问题
			============================================================

			{{question}}

			============================================================
			输出要求
			============================================================

			1. 只能返回合法 JSON。

			2. 禁止返回 Markdown。

			3. 禁止返回 ```json。

			4. 禁止添加JSON之外的解释。

			5. Metrics 必须是对象数组。

			6. Filters 必须是对象数组。

			7. 不允许把 Metrics 写成字符串数组。

			8. 不允许把 Filters 写成字符串数组。

			9. 如果用户没有明确指标，Metrics返回空数组。

			10. 如果用户没有过滤条件，Filters返回空数组。

			11. 如果用户没有分组字段，Dimensions返回空数组。

			12. 如果用户没有排序，OrderBy返回null。

			13. 如果用户没有限制数量，Limit返回null。

			14. Aggregation只能使用：

			    NONE
			    SUM
			    COUNT
			    AVG
			    MAX
			    MIN

			15. Operator只能使用：

			    =
			    !=
			    >
			    <
			    >=
			    <=
			    LIKE

			============================================================
			QueryIntent JSON结构
			============================================================

			{
			  "OriginalQuestion": "",
			  "IntentType": "",
			  "Metrics": [
			    {
			      "Name": "",
			      "Field": "",
			      "Aggregation": "NONE"
			    }
			  ],
			  "Filters": [
			    {
			      "Field": "",
			      "Operator": "=",
			      "Value": ""
			    }
			  ],
			  "Dimensions": [],
			  "OrderBy": null,
			  "OrderDirection": null,
			  "Limit": null,
			  "Explanation": ""
			}

			============================================================
			重要规则
			============================================================

			Metrics中的：

			Name
			表示用户理解的业务指标名称。

			Field
			表示该指标对应的真实Metadata字段名称。

			Aggregation
			表示该指标需要使用的聚合方式。

			例如：

			用户：
			销售金额

			应该类似：

			{
			  "Name": "销售金额",
			  "Field": "Amount",
			  "Aggregation": "SUM"
			}

			用户：
			客户数量

			应该类似：

			{
			  "Name": "客户数量",
			  "Field": "CustomerId",
			  "Aggregation": "COUNT"
			}

			不要输出：

			"Metrics": [
			  "销售金额"
			]

			必须输出：

			"Metrics": [
			  {
			    "Name": "销售金额",
			    "Field": "Amount",
			    "Aggregation": "SUM"
			  }
			]

			============================================================
			最终只返回JSON
			============================================================
			""";

		/*
		 * ============================================================
		 * 3.
		 * 调用Qwen
		 *
		 * 当前项目IQwenService只有:
		 *
		 * GenerateSqlAsync
		 *
		 * 因此这里继续使用现有接口。
		 * 不增加ChatAsync。
		 * ============================================================
		 */

		var response =
			await _qwenService
				.GenerateSqlAsync(prompt);

		/*
		 * ============================================================
		 * 4.
		 * 清理可能存在的Markdown
		 *
		 * 虽然Prompt已经禁止Markdown，
		 * 但模型仍然可能返回:
		 *
		 * ```json
		 * {...}
		 * ```
		 *
		 * 因此这里做一次保护性清理。
		 * ============================================================
		 */

		response =
			CleanJson(response);

		/*
		 * ============================================================
		 * 5.
		 * JSON反序列化
		 * ============================================================
		 */

		var intent =
			JsonSerializer.Deserialize<QueryIntent>(
				response,
				new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

		if (intent == null)
		{
			throw new InvalidOperationException(
				"AI返回的QueryIntent无法解析。");
		}

		/*
		 * 原始问题永远以程序收到的question为准。
		 *
		 * 不使用AI返回的OriginalQuestion，
		 * 防止模型修改原始问题。
		 */

		intent.OriginalQuestion =
			question;

		return intent;
	}

	/// <summary>
	/// 清理AI返回的JSON。
	/// </summary>
	private static string CleanJson(
		string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new InvalidOperationException(
				"AI没有返回任何内容。");
		}

		text =
			text.Trim();

		/*
		 * 处理:
		 *
		 * ```json
		 * {...}
		 * ```
		 */

		if (text.StartsWith("```"))
		{
			var firstLineEnd =
				text.IndexOf('\n');

			if (firstLineEnd >= 0)
			{
				text =
					text.Substring(
						firstLineEnd + 1);
			}

			var last =
				text.LastIndexOf("```");

			if (last >= 0)
			{
				text =
					text.Substring(
						0,
						last);
			}
		}

		return text.Trim();
	}
}