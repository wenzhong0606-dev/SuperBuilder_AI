using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;

namespace SuperBuilder_AI.Services;

/// <summary>
/// Metadata Vector Index 生命周期管理服务。
///
/// 负责:
///
/// 1. Clear
/// 2. Create
/// 3. Rebuild
/// 4. Index
///
/// 数据链路:
///
/// SQL Server Metadata
///        ↓
/// MetadataTable
///        ↓
/// MetadataColumn
///        ↓
/// MetadataSemantic
///        ↓
/// MetadataVectorService
///        ↓
/// QwenEmbeddingService
///        ↓
/// Qdrant
/// </summary>
public class MetadataVectorIndexService
	: IMetadataVectorIndexService
{
	private readonly SuperBIContext _context;
	private readonly IQdrantService _qdrant;
	private readonly IMetadataVectorService _vectorService;

	/// <summary>
	/// 创建 MetadataVectorIndexService。
	/// </summary>
	public MetadataVectorIndexService(
		SuperBIContext context,
		IQdrantService qdrant,
		IMetadataVectorService vectorService)
	{
		_context = context;
		_qdrant = qdrant;
		_vectorService = vectorService;
	}

	/// <summary>
	/// 删除当前 Qdrant Collection。
	///
	/// 如果不存在则不处理。
	/// </summary>
	public async Task ClearAsync()
	{
		if (!await _qdrant.ExistsAsync())
		{
			return;
		}

		/*
         * RecreateCollectionAsync 会:
         *
         * Delete
         *   ↓
         * Create
         *
         * ClearAsync 单独使用时，
         * 这里仍然采用重新创建的生命周期，
         * 保证下一次 Index 时 Collection 一定存在。
         */
		await _qdrant
			.RecreateCollectionAsync();
	}

	/// <summary>
	/// 确保 Qdrant Collection 存在。
	/// </summary>
	public async Task CreateAsync()
	{
		await _qdrant
			.CreateCollectionAsync();
	}

	/// <summary>
	/// 清空旧 Vector，
	/// 然后重新建立全部 Metadata Vector。
	/// </summary>
	public async Task<MetadataVectorRebuildResult>
		RebuildAsync()
	{
		var result =
			new MetadataVectorRebuildResult
			{
				Success = false
			};

		try
		{
			/*
             * 第一步:
             *
             * 删除旧 Collection
             * +
             * 创建新的 1024 维 Collection
             */
			await _qdrant
				.RecreateCollectionAsync();

			/*
             * 第二步:
             *
             * 一次性读取:
             *
             * MetadataTable
             *     ↓
             * MetadataColumn
             *     ↓
             * MetadataSemantic
             */
			var tables =
				await _context.MetadataTables
					.Include(x => x.Columns)
						.ThenInclude(x => x.Semantic)
					.AsNoTracking()
					.OrderBy(x => x.Id)
					.ToListAsync();

			result.TableCount =
				tables.Count;

			/*
             * 第三步:
             *
             * 一个 Table 一个 Table
             * 建立:
             *
             * Table Vector
             * Column Vector
             * Semantic Vector
             */
			foreach (var table in tables)
			{
				var indexResult =
					await _vectorService
						.IndexAsync(table);

				if (!string.IsNullOrWhiteSpace(
					indexResult.TableVectorId))
				{
					result.TableVectorCount++;
				}

				result.ColumnVectorCount +=
					indexResult.ColumnVectors.Count;

				result.SemanticVectorCount +=
					indexResult.SemanticVectors.Count;
			}

			result.Success = true;

			return result;
		}
		catch (Exception ex)
		{
			result.Success = false;
			result.ErrorMessage = ex.Message;

			return result;
		}
	}

	/// <summary>
	/// 为指定 MetadataTable 建立向量。
	///
	/// 注意:
	///
	/// 不会删除 Collection。
	///
	/// 适用于:
	///
	/// MetadataTable 更新
	///        ↓
	/// 单表重新 Embedding
	///        ↓
	/// Upsert
	/// </summary>
	public async Task<MetadataVectorIndexResult>
		IndexAsync(
			long metadataTableId)
	{
		var table =
			await _context.MetadataTables
				.Include(x => x.Columns)
					.ThenInclude(x => x.Semantic)
				.FirstOrDefaultAsync(
					x => x.Id == metadataTableId);

		if (table == null)
		{
			throw new InvalidOperationException(
				$"MetadataTable不存在。" +
				$"Id={metadataTableId}");
		}

		/*
         * 确保 Collection 存在。
         */
		await _qdrant
			.CreateCollectionAsync();

		/*
         * 建立:
         *
         * Table
         * Column
         * Semantic
         */
		return await _vectorService
			.IndexAsync(table);
	}
}