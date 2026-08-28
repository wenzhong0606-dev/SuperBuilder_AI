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

}