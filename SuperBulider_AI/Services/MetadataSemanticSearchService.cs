using Microsoft.EntityFrameworkCore;
using SuperBulider_AI.Data;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Models.AI;


namespace SuperBulider_AI.Services;

/// <summary>
/// Metadata语义检索服务
///
/// 功能:
///
/// 用户问题
///     ↓
/// Embedding
///     ↓
/// Qdrant Query
///     ↓
/// 根据VectorType解析:
///
/// table
/// column
/// semantic
///
///     ↓
///
/// MetadataTable
/// MetadataColumn
/// MetadataSemantic
///
///     ↓
///
/// MetadataSemanticSearchResult
///
/// </summary>
public class MetadataSemanticSearchService
	: IMetadataSemanticSearchService
{


	private readonly IEmbeddingService _embedding;


	private readonly IQdrantService _qdrant;


	private readonly SuperBIContext _context;



	public MetadataSemanticSearchService(
		IEmbeddingService embedding,
		IQdrantService qdrant,
		SuperBIContext context)
	{

		_embedding = embedding;

		_qdrant = qdrant;

		_context = context;

	}





	/// <summary>
	/// Metadata语义搜索
	/// </summary>
	public async Task<List<MetadataSemanticSearchResult>>
		SearchAsync(
			string question,
			int topK = 10)
	{


		/*
		 * 1.
		 * 用户问题Embedding
		 */

		var vector =
			await _embedding
			.GenerateAsync(question);





		/*
		 * 2.
		 * Qdrant查询
		 */

		var points =
			await _qdrant
			.QueryAsync(
				vector,
				topK);





		var results =
			new List<MetadataSemanticSearchResult>();





		/*
		 * 3.
		 * 解析Vector
		 */

		foreach (var point in points)
		{


			if (!point.Payload
				.TryGetValue(
					"type",
					out var typeValue))
			{
				continue;
			}



			var vectorType =
				typeValue
				.ToString()
				?.ToLower();




			if (string.IsNullOrWhiteSpace(vectorType))
			{
				continue;
			}




			switch (vectorType)
			{

				/*
				 * ============================
				 * Table Vector
				 * ============================
				 */

				case "table":

					await LoadTableVectorAsync(
						point,
						results);

					break;



				/*
				 * ============================
				 * Column Vector
				 * ============================
				 */

				case "column":

					await LoadColumnVectorAsync(
						point,
						results);

					break;




				/*
				 * ============================
				 * Semantic Vector
				 * ============================
				 */

				case "semantic":

					await LoadSemanticVectorAsync(
						point,
						results);

					break;


			}

		}




		return results
			.OrderByDescending(x =>
				x.Score)
			.ToList();

	}






	/// <summary>
	/// 加载表向量
	/// </summary>
	private async Task LoadTableVectorAsync(
		VectorSearchResult point,
		List<MetadataSemanticSearchResult> results)
	{


		if (!TryGetLong(
			point.Payload,
			"metadataId",
			out var tableId))
		{
			return;
		}





		var table =
			await _context.MetadataTables

			.Include(x =>
				x.Columns)

			.FirstOrDefaultAsync(x =>
				x.Id == tableId);





		if (table == null)
		{
			return;
		}





		results.Add(
			new MetadataSemanticSearchResult
			{

				VectorType =
					"table",


				VectorId =
					point.Id,


				Table =
					table,


				Score =
					point.Score

			});

	}






	/// <summary>
	/// 加载字段向量
	/// </summary>
	private async Task LoadColumnVectorAsync(
		VectorSearchResult point,
		List<MetadataSemanticSearchResult> results)
	{


		if (!TryGetLong(
			point.Payload,
			"columnId",
			out var columnId))
		{
			return;
		}





		var column =
			await _context.MetadataColumns

			.Include(x =>
				x.MetadataTable)

			.Include(x =>
				x.Semantic)

			.FirstOrDefaultAsync(x =>
				x.Id == columnId);





		if (column == null)
		{
			return;
		}





		results.Add(
			new MetadataSemanticSearchResult
			{

				VectorType =
					"column",


				VectorId =
					point.Id,


				Table =
					column.MetadataTable,


				Column =
					column,


				Semantic =
					column.Semantic,


				Score =
					point.Score

			});

	}






	/// <summary>
	/// 加载语义向量
	/// </summary>
	private async Task LoadSemanticVectorAsync(
		VectorSearchResult point,
		List<MetadataSemanticSearchResult> results)
	{


		if (!TryGetLong(
			point.Payload,
			"semanticId",
			out var semanticId))
		{
			return;
		}





		var semantic =
			await _context.MetadataSemantics

			.Include(x =>
				x.MetadataColumn)

			.ThenInclude(x =>
				x!.MetadataTable)

			.FirstOrDefaultAsync(x =>
				x.Id == semanticId);





		if (semantic == null
			||
			semantic.MetadataColumn == null)
		{
			return;
		}





		results.Add(
			new MetadataSemanticSearchResult
			{

				VectorType =
					"semantic",


				VectorId =
					point.Id,


				Table =
					semantic.MetadataColumn
					.MetadataTable,


				Column =
					semantic.MetadataColumn,


				Semantic =
					semantic,


				Score =
					point.Score

			});

	}






	/// <summary>
	/// 获取Qdrant Payload Long值
	/// </summary>
	private bool TryGetLong(
		Dictionary<string, object> payload,
		string key,
		out long value)
	{

		value = 0;



		if (!payload.TryGetValue(
			key,
			out var obj))
		{
			return false;
		}




		try
		{

			value =
				Convert.ToInt64(obj);

			return true;

		}
		catch
		{

			return false;

		}

	}


}