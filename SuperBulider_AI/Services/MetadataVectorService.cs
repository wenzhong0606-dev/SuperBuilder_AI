using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Models.AI;
using SuperBulider_AI.Models.Metadata;


namespace SuperBulider_AI.Services;

/// <summary>
/// Metadata向量服务
///
/// 负责创建:
///
/// MetadataTable
///        ↓
/// Table Vector
///
/// MetadataColumn
///        ↓
/// Column Vector
///
/// MetadataSemantic
///        ↓
/// Semantic Vector
///
///        ↓
/// Qdrant
///
/// </summary>
public class MetadataVectorService
	: IMetadataVectorService
{


	private readonly IEmbeddingService _embedding;


	private readonly IQdrantService _qdrant;



	public MetadataVectorService(
		IEmbeddingService embedding,
		IQdrantService qdrant)
	{

		_embedding = embedding;

		_qdrant = qdrant;

	}





	/// <summary>
	/// 创建Metadata全部向量
	///
	/// 包含:
	///
	/// 1. 表向量
	/// 2. 字段向量
	/// 3. 字段语义向量
	///
	/// </summary>
	public async Task<MetadataVectorIndexResult>
		IndexAsync(
			MetadataTable metadataTable)
	{


		var result =
			new MetadataVectorIndexResult();




		/*
		 * ============================
		 *
		 * 1.
		 * Table Vector
		 *
		 * ============================
		 */


		if (!string.IsNullOrWhiteSpace(
			metadataTable.SearchText))
		{


			var tableVector =
				await _embedding
				.GenerateAsync(
					metadataTable.SearchText);



			var tableVectorId =
				Guid.NewGuid()
				.ToString();



			await _qdrant
				.UpsertAsync(

					tableVectorId,

					tableVector,


					new Dictionary<string, object>
					{

						["type"]
						=
						"table",


						["metadataType"]
						=
						"table",


						["metadataId"]
						=
						metadataTable.Id,


						["tableId"]
						=
						metadataTable.Id,


						["table"]
						=
						metadataTable.TableName
						??
						"",


						["description"]
						=
						metadataTable.TableComment
						??
						""

					});



			result.TableVectorId =
				tableVectorId;

		}







		/*
		 * ============================
		 *
		 * 2.
		 * Column Vector
		 *
		 * ============================
		 */


		foreach (var column in metadataTable.Columns)
		{


			if (string.IsNullOrWhiteSpace(
				column.SearchText))
			{
				continue;
			}




			var columnVector =
				await _embedding
				.GenerateAsync(
					column.SearchText);




			var columnVectorId =
				Guid.NewGuid()
				.ToString();




			await _qdrant
				.UpsertAsync(

					columnVectorId,

					columnVector,


					new Dictionary<string, object>
					{

						["type"]
						=
						"column",


						["metadataType"]
						=
						"column",


						["metadataId"]
						=
						column.Id,


						["columnId"]
						=
						column.Id,


						["tableId"]
						=
						metadataTable.Id,


						["table"]
						=
						metadataTable.TableName
						??
						"",


						["column"]
						=
						column.ColumnName
						??
						"",


						["description"]
						=
						column.ColumnComment
						??
						""

					});




			result.ColumnVectors
				.Add(
					column.Id,
					columnVectorId);

		}







		/*
		 * ============================
		 *
		 * 3.
		 * Semantic Vector
		 *
		 * ============================
		 */


		foreach (var column in metadataTable.Columns)
		{


			var semantic =
				column.Semantic;



			if (semantic == null)
			{
				continue;
			}




			if (string.IsNullOrWhiteSpace(
				semantic.SearchText))
			{
				continue;
			}




			var semanticVector =
				await _embedding
				.GenerateAsync(
					semantic.SearchText);




			var semanticVectorId =
				Guid.NewGuid()
				.ToString();





			await _qdrant
				.UpsertAsync(

					semanticVectorId,

					semanticVector,


					new Dictionary<string, object>
					{

						["type"]
						=
						"semantic",


						["metadataType"]
						=
						"semantic",


						["semanticId"]
						=
						semantic.Id,


						["columnId"]
						=
						column.Id,


						["tableId"]
						=
						metadataTable.Id,


						["table"]
						=
						metadataTable.TableName
						??
						"",


						["column"]
						=
						column.ColumnName
						??
						"",


						["businessMeaning"]
						=
						semantic.BusinessMeaning
						??
						"",


						["keywords"]
						=
						semantic.Keywords
						??
						""

					});




			result.SemanticVectors
				.Add(
					semantic.Id,
					semanticVectorId);


		}




		return result;

	}

}