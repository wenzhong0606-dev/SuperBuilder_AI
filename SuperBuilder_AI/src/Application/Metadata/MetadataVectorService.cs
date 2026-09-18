using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
			MetadataTable metadataTable,
			CancellationToken ct = default)
	{
		var result =
			new MetadataVectorIndexResult();

		var entries =
			new List<VectorEntry>();

		/*
         * 1. 收集待索引条目（table / column / semantic）
         *    先过滤空 SearchText，批量化 embedding + upsert。
         */
		if (!string.IsNullOrWhiteSpace(
			metadataTable.SearchText))
		{
			entries.Add(
				new VectorEntry(
					CreateStableVectorId(
						"table",
						metadataTable.Id),
					metadataTable.SearchText,
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
					},
					"table",
					metadataTable,
					null,
					null));
		}
		else
		{
			metadataTable.VectorStatus = "Pending";
		}

		foreach (var column in metadataTable.Columns)
		{
			if (string.IsNullOrWhiteSpace(
				column.SearchText))
			{
				column.VectorStatus = "Pending";
				continue;
			}

			entries.Add(
				new VectorEntry(
					CreateStableVectorId(
						"column",
						column.Id),
					column.SearchText,
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
					},
					"column",
					metadataTable,
					column,
					null));
		}

		foreach (var column in metadataTable.Columns)
		{
			var semantic = column.Semantic;
			if (semantic == null)
				continue;

			if (string.IsNullOrWhiteSpace(
				semantic.SearchText))
			{
				semantic.VectorStatus = "Pending";
				continue;
			}

			entries.Add(
				new VectorEntry(
					CreateStableVectorId(
						"semantic",
						semantic.Id),
					semantic.SearchText,
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
					},
					"semantic",
					metadataTable,
					column,
					semantic));
		}

		if (entries.Count == 0)
		{
			return result;
		}

		try
		{
			var texts =
				entries
					.Select(e => e.SearchText)
					.ToList();

			var vectors =
				(await _embedding.GenerateBatchAsync(
					texts,
					"document",
					ct))
				.ToList();

			var points =
				new List<(
					string Id,
					float[] VectorData,
					IReadOnlyDictionary<string, object> Payload)>(
					entries.Count);

			for (var i = 0; i < entries.Count; i++)
			{
				var entry = entries[i];
				var vector = vectors[i];

				entry.ApplyVectorId(entry.StableId);

				// §L.4 payload 隔离字段：tenant_id / data_source_id / metadata_version / metadata_type。
				// 三类 point 均经 entry.Table 归属（table/column/semantic 的父表），可直接取租户/源/版本。
				entry.Payload["tenant_id"] = entry.Table.TenantId;
				entry.Payload["data_source_id"] = entry.Table.DataSourceId;
				entry.Payload["metadata_version"] = entry.Table.MetadataVersion;
				entry.Payload["metadata_type"] = entry.Kind;

				switch (entry.Kind)
				{
					case "table":
						entry.Table.VectorDimension =
							vector.Length;
						entry.Table.VectorStatus = "Synced";
						entry.Table.VectorSyncTime =
							DateTime.UtcNow;
						entry.Table.VectorErrorCode = null;
						result.TableVectorId =
							entry.StableId;
						break;

					case "column":
						entry.Column!.VectorDimension =
							vector.Length;
						entry.Column.VectorStatus = "Synced";
						entry.Column.VectorSyncTime =
							DateTime.UtcNow;
						entry.Column.VectorErrorCode = null;
						result.ColumnVectors[entry.RefId] =
							entry.StableId;
						break;

					case "semantic":
						entry.Semantic!.VectorDimension =
							vector.Length;
						entry.Semantic.VectorStatus = "Synced";
						entry.Semantic.VectorSyncTime =
							DateTime.UtcNow;
						entry.Semantic.VectorErrorCode = null;
						result.SemanticVectors[entry.RefId] =
							entry.StableId;
						break;
				}

				points.Add(
					(entry.StableId, vector, entry.Payload));
			}

			await _qdrant.UpsertBatchAsync(points);
		}
		catch (Exception ex)
		{
			// 单表整体失败不影响其它表；记录短诊断码（含 EMB_ 前缀，
			// 见 QwenEmbeddingService），便于在 VectorErrorCode(nvarchar 64) 内定位。
			var errCode = ex.Message.Length > 60
				? ex.Message[..60]
				: ex.Message;

			metadataTable.VectorStatus = "Failed";
			metadataTable.VectorErrorCode = errCode;

			foreach (var column in metadataTable.Columns)
			{
				column.VectorStatus = "Failed";
				column.VectorErrorCode = errCode;

				if (column.Semantic != null)
				{
					column.Semantic.VectorStatus =
						"Failed";
					column.Semantic.VectorErrorCode = errCode;
				}
			}
		}

		return result;
	}

	/// <summary>
	/// 单表待索引向量条目。持有目标对象引用以便批量回填，
	/// 避免批量完成后再次按 Id 查找。
	/// </summary>
	private sealed record VectorEntry(
		string StableId,
		string SearchText,
		Dictionary<string, object> Payload,
		string Kind,
		MetadataTable Table,
		MetadataColumn? Column,
		MetadataSemantic? Semantic)
	{
		public long RefId =>
			Kind switch
			{
				"table" => Table.Id,
				"column" => Column!.Id,
				"semantic" => Semantic!.Id,
				_ => 0
			};

		public void ApplyVectorId(string id)
		{
			switch (Kind)
			{
				case "table":
					Table.VectorId = id;
					break;
				case "column":
					Column!.VectorId = id;
					break;
				case "semantic":
					Semantic!.VectorId = id;
					break;
			}
		}
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