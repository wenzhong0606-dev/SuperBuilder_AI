using System;
using System.Security.Cryptography;
using System.Text;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services;

/// <summary>
/// Metadata 向量服务。
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
/// </summary>
public class MetadataVectorService
	: IMetadataVectorService
{
	private readonly IEmbeddingService _embedding;
	private readonly IQdrantService _qdrant;

	/// <summary>
	/// 创建 MetadataVectorService。
	/// </summary>
	public MetadataVectorService(
		IEmbeddingService embedding,
		IQdrantService qdrant)
	{
		_embedding = embedding;
		_qdrant = qdrant;
	}

	/// <summary>
	/// 创建 Metadata 全部向量。
	///
	/// 包含:
	///
	/// 1. Table Vector
	/// 2. Column Vector
	/// 3. Semantic Vector
	/// </summary>
	public async Task<MetadataVectorIndexResult>
		IndexAsync(
			MetadataTable metadataTable)
	{
		var result =
			new MetadataVectorIndexResult();

		/*
         * ============================
         * 1.
         * Table Vector
         * ============================
         */

		if (!string.IsNullOrWhiteSpace(
			metadataTable.SearchText))
		{
			try
			{
				var tableVector =
					await _embedding.GenerateAsync(
						metadataTable.SearchText,
						"document");

				var tableVectorId =
					CreateStableVectorId(
						"table",
						metadataTable.Id);

				await _qdrant.UpsertAsync(
					tableVectorId,
					tableVector,
					new Dictionary<string, object>
					{
						["type"] =
							"table",

						["metadataType"] =
							"table",

						["metadataId"] =
							metadataTable.Id,

						["tableId"] =
							metadataTable.Id,

						["table"] =
							metadataTable.TableName
							?? string.Empty,

						["description"] =
							metadataTable.TableComment
							?? string.Empty
					});

				metadataTable.VectorId =
					tableVectorId;
				metadataTable.VectorDimension =
					tableVector.Length;
				metadataTable.VectorStatus =
					"Synced";
				metadataTable.VectorSyncTime =
					DateTime.UtcNow;
				metadataTable.VectorErrorCode =
					null;

				result.TableVectorId =
					tableVectorId;
			}
			catch (Exception ex)
			{
				// 单表向量失败不影响其它表；记录脱敏后的异常类型名。
				metadataTable.VectorStatus = "Failed";
				metadataTable.VectorErrorCode =
					ex.GetType().Name;
			}
		}
		else
		{
			metadataTable.VectorStatus = "Pending";
		}

		/*
         * ============================
         * 2.
         * Column Vector
         * ============================
         */

		foreach (var column
				 in metadataTable.Columns)
		{
			if (string.IsNullOrWhiteSpace(
				column.SearchText))
			{
				column.VectorStatus = "Pending";
				continue;
			}

			try
			{
				var columnVector =
					await _embedding.GenerateAsync(
						column.SearchText,
						"document");

				var columnVectorId =
					CreateStableVectorId(
						"column",
						column.Id);

				await _qdrant.UpsertAsync(
					columnVectorId,
					columnVector,
					new Dictionary<string, object>
					{
						["type"] =
							"column",

						["metadataType"] =
							"column",

						["metadataId"] =
							column.Id,

						["columnId"] =
							column.Id,

						["tableId"] =
							metadataTable.Id,

						["table"] =
							metadataTable.TableName
							?? string.Empty,

						["column"] =
							column.ColumnName
							?? string.Empty,

						["description"] =
							column.ColumnComment
							?? string.Empty
					});

				column.VectorId = columnVectorId;
				column.VectorDimension = columnVector.Length;
				column.VectorStatus = "Synced";
				column.VectorSyncTime = DateTime.UtcNow;
				column.VectorErrorCode = null;

				result.ColumnVectors.Add(
					column.Id,
					columnVectorId);
			}
			catch (Exception ex)
			{
				column.VectorStatus = "Failed";
				column.VectorErrorCode = ex.GetType().Name;
			}
		}

		/*
         * ============================
         * 3.
         * Semantic Vector
         * ============================
         */

		foreach (var column
				 in metadataTable.Columns)
		{
			var semantic =
				column.Semantic;

			if (semantic == null)
			{
				column.VectorStatus = "Pending";
				continue;
			}

			if (string.IsNullOrWhiteSpace(
				semantic.SearchText))
			{
				semantic.VectorStatus = "Pending";
				continue;
			}

			try
			{
				var semanticVector =
					await _embedding.GenerateAsync(
						semantic.SearchText,
						"document");

				var semanticVectorId =
					CreateStableVectorId(
						"semantic",
						semantic.Id);

				await _qdrant.UpsertAsync(
					semanticVectorId,
					semanticVector,
					new Dictionary<string, object>
					{
						["type"] =
							"semantic",

						["metadataType"] =
							"semantic",

						["metadataId"] =
							semantic.Id,

						["semanticId"] =
							semantic.Id,

						["columnId"] =
							column.Id,

						["tableId"] =
							metadataTable.Id,

						["table"] =
							metadataTable.TableName
							?? string.Empty,

						["column"] =
							column.ColumnName
							?? string.Empty,

						["businessMeaning"] =
							semantic.BusinessMeaning
							?? string.Empty,

						["keywords"] =
							semantic.Keywords
							?? string.Empty
					});

				semantic.VectorId = semanticVectorId;
				semantic.VectorDimension = semanticVector.Length;
				semantic.VectorStatus = "Synced";
				semantic.VectorSyncTime = DateTime.UtcNow;
				semantic.VectorErrorCode = null;

				result.SemanticVectors.Add(
					semantic.Id,
					semanticVectorId);
			}
			catch (Exception ex)
			{
				semantic.VectorStatus = "Failed";
				semantic.VectorErrorCode = ex.GetType().Name;
			}
		}

		return result;
	}

	/// <summary>
	/// 创建稳定的 Qdrant Vector ID。
	///
	/// 不能使用 Guid.NewGuid()，
	/// 因为重新建立索引时会产生新的 Vector。
	///
	/// 当前规则:
	///
	/// table:123
	/// column:456
	/// semantic:789
	///
	/// ↓
	///
	/// SHA256
	/// ↓
	///
	/// 固定 GUID
	///
	/// 因此同一个 Metadata 永远对应同一个
	/// Qdrant Point ID。
	/// </summary>
	private static string CreateStableVectorId(
		string type,
		long metadataId)
	{
		var key =
			$"{type}:{metadataId}";

		var hash =
			SHA256.HashData(
				Encoding.UTF8.GetBytes(key));

		var guidBytes =
			hash[..16];

		return new Guid(
			guidBytes)
			.ToString();
	}
}