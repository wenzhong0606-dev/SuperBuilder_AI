using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.Metadata;


namespace SuperBuilder_AI.Services;

/// <summary>
/// Metadata字段语义生成服务。
///
/// 功能:
///
/// MetadataColumn
///        ↓
/// Batch Prompt
///        ↓
/// Qwen
///        ↓
/// MetadataSemantic
///
/// Phase 1.4.5:
///
/// 1. Batch调用Qwen
/// 2. MetadataColumn.Id唯一关联
/// 3. 支持多数据库同名字段
/// 4. 批量保存
/// 5. 自动拆分超长Prompt
///
/// </summary>
public class MetadataSemanticService
	: IMetadataSemanticService
{

	private readonly SuperBIContext _context;


	private readonly IQwenService _qwenService;



	/// <summary>
	/// 单次Qwen请求最大字段数量
	///
	/// 根据Prompt长度调整
	/// </summary>
	private const int QwenBatchSize = 50;



	public MetadataSemanticService(
		SuperBIContext context,
		IQwenService qwenService)
	{

		_context = context;

		_qwenService = qwenService;

	}






	/// <summary>
	/// 单字段生成语义
	/// </summary>
	public async Task<MetadataSemantic?>
		GenerateAsync(
			MetadataColumn column)
	{

		var result =
			await GenerateBatchAsync(
				new List<MetadataColumn>
				{
					column
				});


		return result.FirstOrDefault();

	}






	/// <summary>
	/// 批量生成字段语义
	/// </summary>
	public async Task<List<MetadataSemantic>>
		GenerateBatchAsync(
			List<MetadataColumn> columns)
	{


		if (columns == null ||
			columns.Count == 0)
		{
			return new List<MetadataSemantic>();
		}






		/*
		 * 只处理没有Semantic的数据
		 */

		columns =
			columns
			.Where(x =>
				x.Semantic == null)
			.ToList();



		if (columns.Count == 0)
		{
			return new List<MetadataSemantic>();
		}







		/*
		 * =============================
		 *
		 * 分批调用Qwen
		 *
		 * =============================
		 */


		var allItems =
			new List<MetadataSemanticBatchItem>();




		foreach (var batch in columns.Chunk(QwenBatchSize))
		{

			try
			{

				var response =
					await GenerateBatchPromptAsync(
						batch.ToList());



				var items =
					ParseJson(response);



				allItems.AddRange(items);

			}
			catch (Exception)
			{

				/*
				 * 单批失败不影响其它批次
				 *
				 * 后续可以增加日志
				 */

				continue;

			}

		}








		if (allItems.Count == 0)
		{
			return new List<MetadataSemantic>();
		}








		/*
		 * =============================
		 *
		 * 保存Semantic
		 *
		 * =============================
		 */


		var result =
			new List<MetadataSemantic>();






		foreach (var item in allItems)
		{


			var column =
				columns
				.FirstOrDefault(x =>
					x.Id == item.Id);



			if (column == null)
			{
				continue;
			}






			var semantic =
				column.Semantic;



			if (semantic == null)
			{

				semantic =
					new MetadataSemantic
					{

						MetadataColumnId =
							column.Id,


					Source =
						SemanticSource.AI

					};



				_context.MetadataSemantics
					.Add(semantic);

			}







			semantic.BusinessMeaning =
				item.BusinessMeaning;


			semantic.Keywords =
				MetadataSemantic.FormatList(
					MetadataSemantic.ParseList(item.Keywords));


			semantic.Synonyms =
				MetadataSemantic.FormatList(
					MetadataSemantic.ParseList(item.Synonyms));


			semantic.ExampleQuestions =
				MetadataSemantic.FormatList(
					MetadataSemantic.ParseList(item.ExampleQuestions));


			semantic.BusinessDomain =
				item.BusinessDomain;


			// 置信度收敛到 [0,1]，超出范围截断而非报错，避免破坏既有数据。
			semantic.Confidence =
				item.Confidence is { } c
					? Math.Clamp(c, 0m, 1m)
					: semantic.Confidence;






			semantic.SearchText =
				BuildSearchText(
					column,
					semantic);




			result.Add(
				semantic);


		}






		await _context
			.SaveChangesAsync();




		return result;

	}









	/// <summary>
	/// 单批调用Qwen生成语义
	/// </summary>
	private async Task<string>
		GenerateBatchPromptAsync(
			List<MetadataColumn> columns)
	{


		var prompt =
			new StringBuilder();




		prompt.AppendLine(
			"""
			你是一名企业BI系统数据语义专家。

			请根据数据库字段信息生成业务语义。

			要求：

			1. 返回JSON数组。
			2. 必须保留输入Id。
			3. Id必须为数字。
			4. 每个输入字段必须返回一条记录。
			5. 不允许输出Markdown。
			6. 不允许输出解释文字。


			返回格式：

			[
			 {
			   "Id":1001,
			   "BusinessMeaning":"",
			   "Keywords":"",
			   "Synonyms":"",
			   "ExampleQuestions":"",
			   "BusinessDomain":"",
			   "Confidence":0.95
			 }
			]


			字段信息：

			""");







		foreach (var column in columns)
		{


			var input =
				new
				{

					Id =
						column.Id,


					Table =
						column.MetadataTable?.TableName,


					Column =
						column.ColumnName,


					Comment =
						column.ColumnComment,


					DataType =
						column.DataType

				};




			prompt.AppendLine(
				JsonSerializer.Serialize(
					input));

		}





		return await _qwenService
			.GenerateSqlAsync(
				prompt.ToString());

	}









	/// <summary>
	/// 解析Qwen JSON
	/// </summary>
	private List<MetadataSemanticBatchItem>
		ParseJson(
			string text)
	{


		try
		{

			text =
				CleanJson(text);



			return JsonSerializer
				.Deserialize<List<MetadataSemanticBatchItem>>
				(
					text,
					new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					})
				??
				new List<MetadataSemanticBatchItem>();

		}
		catch
		{

			return new List<MetadataSemanticBatchItem>();

		}

	}









	/// <summary>
	/// 清理AI返回内容
	/// </summary>
	private string CleanJson(
		string text)
	{


		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}




		text =
			text.Trim();




		if (text.StartsWith("```"))
		{

			var start =
				text.IndexOf('\n');


			if (start > 0)
			{
				text =
					text.Substring(
						start + 1);
			}



			var end =
				text.LastIndexOf("```");



			if (end > 0)
			{
				text =
					text.Substring(
						0,
						end);
			}

		}






		var arrayStart =
			text.IndexOf('[');



		var arrayEnd =
			text.LastIndexOf(']');



		if (arrayStart >= 0 &&
		   arrayEnd > arrayStart)
		{

			text =
				text.Substring(
					arrayStart,
					arrayEnd - arrayStart + 1);

		}



		return text.Trim();

	}









	/// <summary>
	/// 构造Embedding搜索文本
	/// </summary>
	private string BuildSearchText(
		MetadataColumn column,
		MetadataSemantic semantic)
	{

		return
			$"""
			表:
			{column.MetadataTable?.TableName}


			字段:
			{column.ColumnName}


			字段说明:
			{column.ColumnComment}


			业务含义:
			{semantic.BusinessMeaning}


			关键词:
			{semantic.Keywords}


			同义词:
			{semantic.Synonyms}


			示例问题:
			{semantic.ExampleQuestions}


			业务领域:
			{semantic.BusinessDomain}

			""";

	}

}