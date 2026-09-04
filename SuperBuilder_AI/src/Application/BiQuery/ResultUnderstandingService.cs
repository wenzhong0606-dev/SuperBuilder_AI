using System.Text.Json;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;


namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 查询结果AI分析服务。
///
/// 数据:
///
/// QueryResult
///
/// 转换:
///
/// 用户可读回答
///
/// </summary>
public class ResultUnderstandingService
	:
	IResultUnderstandingService
{


	private readonly IQwenService _qwenService;



	public ResultUnderstandingService(
		IQwenService qwenService)
	{

		_qwenService =
			qwenService;

	}




	/// <summary>
	/// 分析查询结果。
	/// </summary>
	public async Task<QueryAnswer>
		AnalyzeAsync(
			string question,
			QueryResult result)
		=> await AnalyzeCoreAsync(question, result, null);

	public async Task<QueryAnswer> AnalyzeAsync(
		string question,
		QueryResult result,
		QueryPlan plan)
		=> await AnalyzeCoreAsync(question, result, plan);

	private async Task<QueryAnswer> AnalyzeCoreAsync(
		string question,
		QueryResult result,
		QueryPlan? plan)
	{


		if (!result.Success)
		{
			return new QueryAnswer
			{

				Success = false,

				Question = question,

				ErrorMessage =
					result.ErrorMessage

			};
		}

		// 明细/TopN 列表是“返回记录”，不是“统计分析”。
		// 直接给出确定性说明并以表格为唯一默认视图，避免 LLM 把 LIMIT 行数
		// 误称为总数，或把状态码、日期列随意加工成 KPI/趋势图。
		if (IsDetailList(plan))
		{
			var order = plan!.Orders.FirstOrDefault();
			var orderText = order is null || string.IsNullOrWhiteSpace(order.Field)
				? string.Empty
				: $"，按 {order.Field} {NormalizeDirection(order.Direction)} 排列";

			return new QueryAnswer
			{
				Success = true,
				Question = question,
				Answer = $"已返回 {result.Count} 条记录{orderText}。",
				Summary = new Dictionary<string, object?>(),
				Visualizations = new List<VisualizationSuggestion>
				{
					new()
					{
						Type = "table",
						Title = "查询明细",
						Reason = "明细列表优先展示业务字段，不自动生成统计图表。"
					}
				}
			};
		}




		var dataJson =
			JsonSerializer.Serialize(
				result.Rows,
				new JsonSerializerOptions
				{
					WriteIndented = true
				});





		var prompt =
			"""
			你是一个专业BI数据分析助手。

			用户问题:

			{{QUESTION}}


			数据库查询结果:

			{{DATA}}


			请分析数据，并严格返回JSON：

			{
			  "answer":"",
			  "summary":{},
			  "visualizations":[
				{
				  "type":"",
				  "title":"",
				  "xAxis":"",
				  "yAxis":[],
				  "reason":""
				}
			  ]
			}

			要求:

			1. answer 使用自然语言解释数据。
			2. summary 提取关键指标。
			3. visualizations 根据数据推荐:
			   table
			   bar
			   line
			   pie

			不要输出Markdown。
			不要输出代码块。
			""";


		prompt =
			prompt
			.Replace(
				"{{QUESTION}}",
				question)
			.Replace(
				"{{DATA}}",
				dataJson);






		var response =
			await _qwenService
			.GenerateSqlAsync(
				prompt);






		try
		{

			var answer =
				JsonSerializer.Deserialize<QueryAnswer>(
					response,
					new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});



			if (answer == null)
			{
				throw new Exception(
					"AI返回结果为空");
			}



			answer.Success =
				true;



			answer.Question =
				question;



			SanitizeVisualizations(answer, result);
			return answer;

		}
		catch (Exception ex)
		{

			return new QueryAnswer
			{

				Success = false,

				Question = question,

				ErrorMessage =
					ex.Message,

				Answer =
					response

			};

		}

	}

	private static bool IsDetailList(QueryPlan? plan)
	{
		if (plan is null || plan.IsAggregate) return false;
		if (plan.Metrics.Any(x => !string.IsNullOrWhiteSpace(x.Aggregation)
			&& !string.Equals(x.Aggregation, "NONE", StringComparison.OrdinalIgnoreCase))) return false;
		return plan.IsDetailRanking
			|| plan.Intent?.IsDetail == true
			|| plan.Limit.HasValue
			|| plan.Orders.Count > 0;
	}

	private static string NormalizeDirection(string? direction)
		=> string.Equals(direction, "DESC", StringComparison.OrdinalIgnoreCase) ? "倒序" : "正序";

	private static void SanitizeVisualizations(QueryAnswer answer, QueryResult result)
	{
		if (answer.Visualizations.Count == 0 || result.Rows.Count == 0) return;
		var columns = result.Rows.SelectMany(x => x.Keys).ToHashSet(StringComparer.OrdinalIgnoreCase);
		answer.Visualizations = answer.Visualizations.Where(v =>
		{
			var type = (v.Type ?? string.Empty).Trim().ToLowerInvariant();
			if (type == "table") return true;
			if (type is not ("bar" or "line" or "pie")) return false;
			if (string.IsNullOrWhiteSpace(v.XAxis) || !columns.Contains(v.XAxis)) return false;
			if (v.YAxis.Count == 0 || v.YAxis.Any(y => !columns.Contains(y))) return false;
			return v.YAxis.All(y => result.Rows.All(r => IsNumeric(r.GetValueOrDefault(y))));
		}).ToList();
	}

	private static bool IsNumeric(object? value)
	{
		if (value is null) return false;
		return value is byte or sbyte or short or ushort or int or uint or long or ulong
			or float or double or decimal;
	}

}
