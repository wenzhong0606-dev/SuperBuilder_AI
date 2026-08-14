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
         * QueryIntent负责表达:
         *
         * 用户业务语义。
         *
         * QueryPlanBuilder负责:
         *
         * 用户业务语义
         *      ↓
         * Metadata真实字段
         *
         * 因此这里必须尽可能正确识别:
         *
         * Metric
         * Filter
         * Dimension
         * OrderBy
         * OrderDirection
         * Limit
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

            16. IntentType只能使用：

                Detail
                Aggregate
                Ranking
                Comparison
                Trend

            ============================================================
            排序与排名语义规则
            ============================================================

            一、数量/金额/销量等指标的“最多”

            以下表达表示按照对应业务指标从大到小排序：

            最多
            最大
            最大的
            最高
            最高的
            数量最多
            金额最多
            销量最多
            销售额最高
            库存最多
            库存数量最多

            必须：

            OrderDirection = "DESC"


            二、数量/金额/销量等指标的“最少”

            以下表达表示按照对应业务指标从小到大排序：

            最少
            最小
            最小的
            最低
            最低的
            数量最少
            金额最少
            销量最低
            库存最少
            库存数量最少

            必须：

            OrderDirection = "ASC"


            三、Top-N / 前N

            以下表达表示限制返回数量：

            Top 10
            top10
            前10条
            前十条
            前10名
            前十名
            10条
            十条

            例如：

            “数量最多的十条库存”

            必须理解为：

            OrderBy = “库存数量”
            OrderDirection = "DESC"
            Limit = 10

            而不是：

            OrderBy = “创建时间”


            四、Limit绝对不等于时间排序

            非常重要：

            Limit只表示返回记录数量。

            例如：

            用户：

            数量最多的十条库存

            正确：

            OrderBy = “库存数量”
            OrderDirection = "DESC"
            Limit = 10

            错误：

            OrderBy = “创建时间”
            OrderDirection = "DESC"
            Limit = 10


            五、只有用户明确要求时间顺序时才使用时间字段

            以下表达才允许使用时间字段：

            最近
            最新
            最新的
            最近创建
            最新创建
            最近新增
            最新新增

            例如：

            “最近十条库存”

            可以：

            OrderBy = “创建时间”
            OrderDirection = "DESC"
            Limit = 10


            六、最早时间

            以下表达表示时间从早到晚：

            最早
            最早创建
            最早新增
            最早的十条

            应：

            OrderDirection = "ASC"


            七、Ranking

            当用户要求：

            最多
            最少
            最大
            最小
            最高
            最低
            Top N
            前 N 名
            排名前 N

            如果存在明确的业务指标排序要求：

            IntentType = "Ranking"


            八、排序字段必须是用户要求排序的业务指标

            例如：

            用户：

            数量最多的十条库存

            正确：

            {
              "IntentType": "Ranking",
              "OrderBy": "库存数量",
              "OrderDirection": "DESC",
              "Limit": 10
            }


            用户：

            数量最少的十条库存

            正确：

            {
              "IntentType": "Ranking",
              "OrderBy": "库存数量",
              "OrderDirection": "ASC",
              "Limit": 10
            }


            用户：

            销售金额最高的十个客户

            正确：

            {
              "IntentType": "Ranking",
              "OrderBy": "销售金额",
              "OrderDirection": "DESC",
              "Limit": 10
            }


            九、不要因为存在Limit而自动产生OrderBy

            如果用户只是：

            “查询十条库存”

            那么：

            Limit = 10

            OrderBy = null

            OrderDirection = null

            除非用户明确要求：

            最近
            最新
            最多
            最少
            最大
            最小
            最高
            最低
            Top N
            排名前 N


            ============================================================
            Metric规则
            ============================================================

            Metrics中的：

            Name
            表示用户理解的业务指标名称。

            Field
            表示该指标对应的真实Metadata字段名称或业务字段名称。

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


            用户：

            库存数量最多的十条库存

            应该类似：

            {
              "Name": "库存数量",
              "Field": "库存数量",
              "Aggregation": "NONE"
            }

            同时：

            "OrderBy": "库存数量",
            "OrderDirection": "DESC",
            "Limit": 10


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
            Filter规则
            ============================================================

            Filter必须使用：

            Field
            Operator
            Value

            例如：

            {
              "Field": "年份",
              "Operator": "=",
              "Value": "2025"
            }


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
         * ============================================================
         */

		var response =
			await _qwenService
				.GenerateSqlAsync(prompt);

		/*
         * ============================================================
         * 4.
         * 清理可能存在的Markdown
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