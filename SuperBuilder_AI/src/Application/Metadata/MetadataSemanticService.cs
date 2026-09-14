using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

	private readonly ILogger<MetadataSemanticService> _logger;



	/// <summary>
	/// 单次Qwen请求最大字段数量
	///
	/// 根据Prompt长度调整
	/// </summary>
	private const int QwenBatchSize = 25;
	private const int MaxConcurrency = 3;
	private const int MaxAttempts = 3;



	public MetadataSemanticService(
		SuperBIContext context,
		IQwenService qwenService,
		ILogger<MetadataSemanticService> logger)
	{

		_context = context;

		_qwenService = qwenService;

		_logger = logger;

	}






	/// <summary>
	/// 单字段生成语义
	/// </summary>
	public async Task<MetadataSemantic?>
		GenerateAsync(
			MetadataColumn column,
			CancellationToken ct = default)
	{

		var result =
			await GenerateBatchAsync(
				new List<MetadataColumn>
				{
					column
				},
				ct: ct);


		return result.FirstOrDefault();

	}






	/// <summary>
	/// 批量生成字段语义
	/// </summary>
	public async Task<List<MetadataSemantic>>
		GenerateBatchAsync(
			List<MetadataColumn> columns,
			Action<SemanticGenerationProgress>? progress = null,
			CancellationToken ct = default)
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




		var batches = columns
			.Chunk(QwenBatchSize)
			.Select((batch, index) => new SemanticBatch(index + 1, batch.ToList()))
			.ToList();

		using var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
		var pending = batches
			.Select(batch => ProcessBatchAsync(batch, batches.Count, semaphore, ct))
			.ToList();

		var batchesCompleted = 0;
		var fieldsCompleted = 0;
		var fieldsGenerated = 0;
		var fieldsFailed = 0;

		while (pending.Count > 0)
		{
			ct.ThrowIfCancellationRequested();
			var finishedTask = await Task.WhenAny(pending);
			pending.Remove(finishedTask);
			var batchResult = await finishedTask;

			batchesCompleted++;
			fieldsCompleted += batchResult.BatchSize;
			fieldsGenerated += batchResult.Items.Count;
			fieldsFailed += Math.Max(0, batchResult.BatchSize - batchResult.Items.Count);
			allItems.AddRange(batchResult.Items);

			progress?.Invoke(new SemanticGenerationProgress(
				batchesCompleted,
				batches.Count,
				fieldsCompleted,
				columns.Count,
				fieldsGenerated,
				fieldsFailed,
				batchResult.Message));
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
			.SaveChangesAsync(ct);




		return result;

	}



	private async Task<SemanticBatchResult> ProcessBatchAsync(
		SemanticBatch batch,
		int totalBatches,
		SemaphoreSlim semaphore,
		CancellationToken ct)
	{
		await semaphore.WaitAsync(ct);
		try
		{
			List<MetadataSemanticBatchItem> lastPartial = new();

			for (var attempt = 1; attempt <= MaxAttempts; attempt++)
			{
				ct.ThrowIfCancellationRequested();
				try
				{
					var response = await GenerateBatchPromptAsync(batch.Columns, ct);
					var expectedIds = batch.Columns.Select(x => x.Id).ToHashSet();
					var items = ParseJson(response)
						.Where(x => expectedIds.Contains(x.Id))
						.GroupBy(x => x.Id)
						.Select(x => x.First())
						.ToList();

					if (items.Count > lastPartial.Count)
						lastPartial = items;

					if (items.Count == batch.Columns.Count)
					{
						_logger.LogInformation("Metadata semantic batch {Batch}/{TotalBatches} completed on attempt {Attempt}: {Count} fields.", batch.Number, totalBatches, attempt, items.Count);
						return new SemanticBatchResult(items, batch.Columns.Count, "第 " + batch.Number + "/" + totalBatches + " 批完成：" + items.Count + "/" + batch.Columns.Count + " 个字段。");
					}

					throw new InvalidDataException("Qwen semantic response incomplete: " + items.Count + "/" + batch.Columns.Count + ".");
				}
				catch (OperationCanceledException) when (ct.IsCancellationRequested)
				{
					throw;
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Metadata semantic batch {Batch}/{TotalBatches} attempt {Attempt}/{MaxAttempts} failed.", batch.Number, totalBatches, attempt, MaxAttempts);
					if (attempt < MaxAttempts)
						await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
				}
			}

			_logger.LogError("Metadata semantic batch {Batch}/{TotalBatches} exhausted retries. Generated {Generated}/{Total} fields.", batch.Number, totalBatches, lastPartial.Count, batch.Columns.Count);
			return new SemanticBatchResult(lastPartial, batch.Columns.Count, "第 " + batch.Number + "/" + totalBatches + " 批重试结束：成功 " + lastPartial.Count + "/" + batch.Columns.Count + "。");
		}
		finally
		{
			semaphore.Release();
		}
	}

	private sealed record SemanticBatch(int Number, List<MetadataColumn> Columns);
	private sealed record SemanticBatchResult(List<MetadataSemanticBatchItem> Items, int BatchSize, string Message);







	/// <summary>
	/// 单批调用Qwen生成语义
	/// </summary>
	private async Task<string>
		GenerateBatchPromptAsync(
			List<MetadataColumn> columns,
			CancellationToken ct)
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
				prompt.ToString(),
				ct);

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