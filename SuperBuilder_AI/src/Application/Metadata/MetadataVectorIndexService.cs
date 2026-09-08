using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.Metadata;

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
/// 5. DetectOrphans（孤儿检测）
/// 6. ValidateVectors（模型/维度校验）
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
	private readonly QdrantOptions _qdrantOptions;

	/// <summary>
	/// 创建 MetadataVectorIndexService。
	/// </summary>
	public MetadataVectorIndexService(
		SuperBIContext context,
		IQdrantService qdrant,
		IMetadataVectorService vectorService,
		IOptions<QdrantOptions> qdrantOptions)
	{
		_context = context;
		_qdrant = qdrant;
		_vectorService = vectorService;
		_qdrantOptions = qdrantOptions.Value;
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

			// 持久化向量同步状态（Synced / Failed / Pending）。
			await _context.SaveChangesAsync();

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
        var indexResult =
            await _vectorService
                .IndexAsync(table);

        // 持久化向量同步状态。
        await _context.SaveChangesAsync();

        return indexResult;
    }

    /// <summary>
    /// 孤儿检测：找出 Qdrant 中存在、但数据库中已无对应 Metadata 记录的 Vector Point。
    /// </summary>
    public async Task<MetadataVectorOrphanResult>
        DetectOrphansAsync(
            CancellationToken cancellationToken = default)
    {
        var dbIds = new HashSet<string>(
            await _context.MetadataTables
                .Where(x => x.VectorId != null)
                .Select(x => x.VectorId!)
                .ToListAsync(cancellationToken));

        dbIds.UnionWith(
            await _context.MetadataColumns
                .Where(x => x.VectorId != null)
                .Select(x => x.VectorId!)
                .ToListAsync(cancellationToken));

        dbIds.UnionWith(
            await _context.MetadataSemantics
                .Where(x => x.VectorId != null)
                .Select(x => x.VectorId!)
                .ToListAsync(cancellationToken));

        var qdrantIds =
            await _qdrant.ListPointIdsAsync(cancellationToken);

        var orphans =
            qdrantIds
                .Where(id => !dbIds.Contains(id))
                .ToList();

        return new MetadataVectorOrphanResult
        {
            QdrantPointCount = qdrantIds.Count,
            DatabaseVectorCount = dbIds.Count,
            OrphanCount = orphans.Count,
            OrphanIds = orphans
        };
    }

    /// <summary>
    /// 校验向量一致性：将存储的向量维度与当前 Qdrant 配置维度不一致的
    /// Metadata 记录标记为 <c>Stale</c>，返回受影响数量。
    /// </summary>
    public async Task<MetadataVectorValidationResult>
        ValidateVectorsAsync(
            CancellationToken cancellationToken = default)
    {
        var expected = (int)_qdrantOptions.VectorSize;
        var mismatched = new List<string>();

        var tables =
            await _context.MetadataTables
                .Where(x => x.VectorId != null)
                .ToListAsync(cancellationToken);

        foreach (var t in tables)
        {
            if (t.VectorDimension is { } dim && dim != expected)
            {
                t.VectorStatus = "Stale";
                mismatched.Add($"table:{t.Id}");
            }
        }

        var columns =
            await _context.MetadataColumns
                .Where(x => x.VectorId != null)
                .ToListAsync(cancellationToken);

        foreach (var c in columns)
        {
            if (c.VectorDimension is { } dim && dim != expected)
            {
                c.VectorStatus = "Stale";
                mismatched.Add($"column:{c.Id}");
            }
        }

        var semantics =
            await _context.MetadataSemantics
                .Where(x => x.VectorId != null)
                .ToListAsync(cancellationToken);

        foreach (var s in semantics)
        {
            if (s.VectorDimension is { } dim && dim != expected)
            {
                s.VectorStatus = "Stale";
                mismatched.Add($"semantic:{s.Id}");
            }
        }

        if (mismatched.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new MetadataVectorValidationResult
        {
            ExpectedDimension = expected,
            MismatchedCount = mismatched.Count,
            MismatchedIds = mismatched
        };
    }
}