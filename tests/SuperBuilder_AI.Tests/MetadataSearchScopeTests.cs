using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services;
using SuperBuilder_AI.Services.BI;
using SuperBuilder_AI.Services.BI.Planning;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P0：理解阶段 Metadata 上下文的数据源作用域收敛。
///
/// 背景：<see cref="MetadataSemanticSearchService"/> 是<strong>全局 top-K</strong> 召回
/// （Qdrant payload 未写入 dataSourceId，无服务端过滤条件）。若不限定作用域，
/// 理解阶段的提示词会把<strong>其他数据源</strong>的同名列一起喂给 LLM，
/// 实测后果：问「按类型统计入库单据数量」时，提示词里出现 MES 的
/// <c>mes_eqp_spare_warehouse_enter.type</c>，维度被解析到别的数据源的表上，
/// 置信度被压低而错走澄清（0.742 / Medium）。
///
/// 本文件锁定三件事：
///   1. 检索层按 <c>MetadataTable.DataSourceId</c> 过滤（以元库为准，不依赖索引新旧）；
///   2. 过滤发生在全量召回之后，因此必须过采样，否则作用域内候选不足；
///   3. 作用域沿 MetadataContextBuilder / QueryUnderstandingService / BIConversationService
///      逐层透传，且 null 作用域（Golden / 内部兼容路径）行为不变。
/// </summary>
public class MetadataSearchScopeTests
{
	/// <summary>数据源 1 —— WMS（源库）。</summary>
	private const long WmsSourceId = 1;

	/// <summary>数据源 2 —— PMIS（源库）。</summary>
	private const long PmisSourceId = 2;

	private const long TenantId = 4;


	// ------------------------------------------------------------------
	// 检索层：作用域过滤
	// ------------------------------------------------------------------

	[Fact]
	public async Task SearchAsync_限定数据源_丢弃其他数据源候选()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		// 混合召回：分数最高的两条属于 PMIS，WMS 的排在后面。
		// 这模拟真实情形 —— 跨源同名列（都叫 type）语义相似度更高，抢占前排。
		var qdrant = new FakeQdrant(
			TablePoint(201, "mes_eqp_spare_warehouse_enter", 0.95),
			TablePoint(202, "mes_eqp_spare_receipt_info", 0.90),
			TablePoint(101, "wms_storage_receipt", 0.80),
			TablePoint(102, "wms_inventory", 0.70));

		var service = new MetadataSemanticSearchService(new FakeEmbedding(), qdrant, ctx);

		var results = await service.SearchAsync("按类型统计入库单据数量", 2, null, new[] { WmsSourceId });

		Assert.Equal(new[] { 101L, 102L }, results.Select(r => r.Table!.Id).ToArray());
		Assert.All(results, r => Assert.Equal(WmsSourceId, r.Table!.DataSourceId));
	}

	[Fact]
	public async Task SearchAsync_限定数据源_按倍数过采样()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		var qdrant = new FakeQdrant(TablePoint(101, "wms_storage_receipt", 0.80));
		var service = new MetadataSemanticSearchService(new FakeEmbedding(), qdrant, ctx);

		await service.SearchAsync("入库单据", 10, null, new[] { WmsSourceId });

		// 过滤只能发生在召回之后，因此向向量库请求的量必须显著大于 topK，
		// 否则跨源候选会挤占名额导致作用域内候选不足。
		Assert.True(
			qdrant.LastLimit > 10,
			$"限定作用域时应过采样，实际 limit={qdrant.LastLimit}");
	}

	[Fact]
	public async Task SearchAsync_限定数据源_过滤后截断到TopK()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		var qdrant = new FakeQdrant(
			TablePoint(101, "wms_storage_receipt", 0.90),
			TablePoint(102, "wms_inventory", 0.80),
			TablePoint(103, "wms_delivery_receipt", 0.70));

		var service = new MetadataSemanticSearchService(new FakeEmbedding(), qdrant, ctx);

		var results = await service.SearchAsync("入库单据", 2, null, new[] { WmsSourceId });

		Assert.Equal(2, results.Count);
		// 截断须保留分数顺序（高分为先）。
		Assert.Equal(new[] { 101L, 102L }, results.Select(r => r.Table!.Id).ToArray());
	}

	[Fact]
	public async Task SearchAsync_多数据源授权_保留全部授权源候选()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		var qdrant = new FakeQdrant(
			TablePoint(201, "mes_eqp_spare_warehouse_enter", 0.95),
			TablePoint(101, "wms_storage_receipt", 0.80));

		var service = new MetadataSemanticSearchService(new FakeEmbedding(), qdrant, ctx);

		var results = await service.SearchAsync("入库单据", 10, null, new[] { WmsSourceId, PmisSourceId });

		Assert.Equal(2, results.Count);
	}

	[Fact]
	public async Task SearchAsync_空作用域_返回空且不抛异常()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		var qdrant = new FakeQdrant(TablePoint(101, "wms_storage_receipt", 0.80));
		var service = new MetadataSemanticSearchService(new FakeEmbedding(), qdrant, ctx);

		var results = await service.SearchAsync("入库单据", 10, null, Array.Empty<long>());

		// 空集合 = 确实没有可用数据源。此处不抛异常（处置权交调用方：
		// QueryPlanDataSourceScope 会以 403 终止），但绝不能回退成「不过滤」。
		Assert.Empty(results);
		Assert.Equal(0, qdrant.QueryCount);
	}

	[Fact]
	public async Task SearchAsync_不限定数据源_召回上限等于TopK()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		var qdrant = new FakeQdrant(
			TablePoint(201, "mes_eqp_spare_warehouse_enter", 0.95),
			TablePoint(101, "wms_storage_receipt", 0.80));

		var service = new MetadataSemanticSearchService(new FakeEmbedding(), qdrant, ctx);

		var results = await service.SearchAsync("入库单据", 5);

		// Golden / 评估器路径：不限定作用域 => 不过采样、不过滤，逐字节同旧版。
		Assert.Equal(5, qdrant.LastLimit);
		Assert.Equal(2, results.Count);
		Assert.Equal(201L, results[0].Table!.Id);
	}


	// ------------------------------------------------------------------
	// 上下文构建层：透传
	// ------------------------------------------------------------------

	[Fact]
	public async Task MetadataContextBuilder_把作用域透传给检索服务()
	{
		var search = new CapturingSemanticSearch();
		var builder = new MetadataContextBuilder(search);

		await builder.BuildAsync("按类型统计入库单据数量", new[] { WmsSourceId });

		Assert.Equal(new[] { WmsSourceId }, search.LastDataSourceIds);
	}

	[Fact]
	public async Task MetadataContextBuilder_不限定作用域_走原三参调用()
	{
		var search = new CapturingSemanticSearch();
		var builder = new MetadataContextBuilder(search);

		await builder.BuildAsync("按类型统计入库单据数量");

		// 两参重载在 null 时必须落到原三参调用（而非四参传 null），
		// 以便任何自定义实现都保持旧行为。
		Assert.Null(search.LastDataSourceIds);
		Assert.Equal(1, search.ThreeArgCalls);
		Assert.Equal(0, search.FourArgCalls);
	}

	[Fact]
	public async Task MetadataContextBuilder_作用域内无候选_仍产出空上下文不抛异常()
	{
		var search = new CapturingSemanticSearch { Results = new List<MetadataSemanticSearchResult>() };
		var builder = new MetadataContextBuilder(search);

		var context = await builder.BuildAsync("按类型统计入库单据数量", new[] { WmsSourceId });

		Assert.Contains("数据库Metadata知识", context);
	}


	// ------------------------------------------------------------------
	// 理解服务层：透传
	// ------------------------------------------------------------------

	[Fact]
	public async Task QueryUnderstandingService_把作用域透传给上下文构建器()
	{
		var contextBuilder = new CapturingContextBuilder();
		var service = new QueryUnderstandingService(
			contextBuilder,
			new FakeQwen(),
			new QueryIntentNormalizer());

		await service.UnderstandAsync(
			"按类型统计入库单据数量",
			PlatformContext.FromTenant(TenantId),
			new[] { WmsSourceId });

		Assert.Equal(new[] { WmsSourceId }, contextBuilder.LastDataSourceIds);
	}


	// ------------------------------------------------------------------
	// 编排层：作用域解析口径（与 QueryPlanDataSourceScope 对齐）
	// ------------------------------------------------------------------

	[Fact]
	public async Task BIConversationService_显式请求数据源_作用域收敛到该数据源()
	{
		var scope = await CaptureScopeAsync(
			requestedDataSourceId: PmisSourceId,
			authorizedDataSourceIds: new[] { WmsSourceId, PmisSourceId });

		Assert.Equal(new[] { PmisSourceId }, scope);
	}

	[Fact]
	public async Task BIConversationService_未指定数据源_作用域收敛到授权集合()
	{
		var scope = await CaptureScopeAsync(
			requestedDataSourceId: null,
			authorizedDataSourceIds: new[] { WmsSourceId });

		Assert.Equal(new[] { WmsSourceId }, scope);
	}

	[Fact]
	public async Task BIConversationService_无授权信息_作用域为null_行为不变()
	{
		var scope = await CaptureScopeAsync(
			requestedDataSourceId: null,
			authorizedDataSourceIds: null);

		// Golden / 评估器 / 内部兼容路径：不限定作用域 => 检索行为逐字节不变。
		Assert.Null(scope);
	}

	[Fact]
	public async Task BIConversationService_空授权集合_作用域为空集合而非null()
	{
		var scope = await CaptureScopeAsync(
			requestedDataSourceId: null,
			authorizedDataSourceIds: Array.Empty<long>());

		// 关键：空授权集合若降级为 null，未授权数据源会重新进入提示词。
		// 必须保持空集合，让理解阶段产出空上下文、由下游以 403 终止。
		Assert.NotNull(scope);
		Assert.Empty(scope!);
	}

	[Fact]
	public async Task BIConversationService_仅显式请求_无授权清单_作用域为请求数据源()
	{
		var scope = await CaptureScopeAsync(
			requestedDataSourceId: WmsSourceId,
			authorizedDataSourceIds: null);

		Assert.Equal(new[] { WmsSourceId }, scope);
	}


	// ------------------------------------------------------------------
	// 夹具
	// ------------------------------------------------------------------

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private static async Task SeedAsync(SuperBIContext ctx)
	{
		// MetadataTable 有 TenantId / DataSourceId 两个外键，SQLite 下必须先把
		// 租户与数据源补齐，否则 SaveChanges 会报 FOREIGN KEY constraint failed。
		ctx.Tenants.Add(new Tenant
		{
			Id = TenantId,
			TenantCode = "scope-test",
			TenantName = "ScopeTest",
			Enabled = true,
			CreatedTime = DateTime.UtcNow
		});

		ctx.DataSources.Add(new DataSource
		{
			Id = WmsSourceId,
			TenantId = TenantId,
			Name = "WMS",
			NormalizedName = "wms",
			DbType = "mysql",
			ConnectionString = "x",
			Enabled = true
		});

		ctx.DataSources.Add(new DataSource
		{
			Id = PmisSourceId,
			TenantId = TenantId,
			Name = "PMIS",
			NormalizedName = "pmis",
			DbType = "mysql",
			ConnectionString = "x",
			Enabled = true
		});

		ctx.MetadataTables.Add(new MetadataTable
		{
			Id = 101,
			TenantId = TenantId,
			DataSourceId = WmsSourceId,
			TableName = "wms_storage_receipt",
			TableComment = "入库凭证"
		});
		ctx.MetadataTables.Add(new MetadataTable
		{
			Id = 102,
			TenantId = TenantId,
			DataSourceId = WmsSourceId,
			TableName = "wms_inventory",
			TableComment = "库存"
		});
		ctx.MetadataTables.Add(new MetadataTable
		{
			Id = 103,
			TenantId = TenantId,
			DataSourceId = WmsSourceId,
			TableName = "wms_delivery_receipt",
			TableComment = "出库凭证"
		});
		ctx.MetadataTables.Add(new MetadataTable
		{
			Id = 201,
			TenantId = TenantId,
			DataSourceId = PmisSourceId,
			TableName = "mes_eqp_spare_warehouse_enter",
			TableComment = "备件入库"
		});
		ctx.MetadataTables.Add(new MetadataTable
		{
			Id = 202,
			TenantId = TenantId,
			DataSourceId = PmisSourceId,
			TableName = "mes_eqp_spare_receipt_info",
			TableComment = "备件入库明细"
		});

		await ctx.SaveChangesAsync();
	}

	private static VectorSearchResult TablePoint(long tableId, string tableName, double score)
		=> new()
		{
			Id = $"table:{tableId}",
			Score = score,
			Payload = new Dictionary<string, object>
			{
				["type"] = "table",
				["metadataId"] = tableId,
				["tableId"] = tableId,
				["table"] = tableName
			}
		};

	/// <summary>
	/// 构造一个只关心「理解阶段收到什么作用域」的 BIConversationService：
	/// 理解服务捕获作用域后立即抛异常，避免为测试拉起整条 QueryPlan 管线。
	/// </summary>
	private static async Task<IReadOnlyCollection<long>?> CaptureScopeAsync(
		long? requestedDataSourceId,
		IReadOnlyCollection<long>? authorizedDataSourceIds)
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var understanding = new CapturingUnderstanding();

		var service = new BIConversationService(
			understanding,
			null!,
			null!,
			null!,
			ctx,
			null!,
			null!);

		try
		{
			await service.ExecuteAsync(
				"按类型统计入库单据数量",
				TenantId,
				requestedDataSourceId,
				authorizedDataSourceIds);
		}
		catch
		{
			// 理解阶段之后的依赖均为占位 null，抛异常属预期；
			// 断言只针对「理解之前」已捕获的作用域。
		}

		return understanding.LastDataSourceIds;
	}


	private sealed class FakeEmbedding : IEmbeddingService
	{
		public Task<float[]> GenerateAsync(string text, string textType = "document", CancellationToken ct = default)
			=> Task.FromResult(new float[] { 1, 2, 3, 4 });

		public int Dimension => 4;

		public string ModelName => "fake";
	}

	private sealed class FakeQdrant : IQdrantService
	{
		private readonly List<VectorSearchResult> _points;

		public FakeQdrant(params VectorSearchResult[] points) => _points = points.ToList();

		/// <summary>最近一次查询请求的 limit（用于断言过采样）。</summary>
		public int LastLimit { get; private set; }

		/// <summary>查询调用次数。</summary>
		public int QueryCount { get; private set; }

		public Task CreateCollectionAsync() => Task.CompletedTask;
		public Task<bool> ExistsAsync() => Task.FromResult(true);
		public Task RecreateCollectionAsync() => Task.CompletedTask;

		public Task UpsertAsync(string id, float[] vector, Dictionary<string, object> payload, CancellationToken ct = default)
			=> Task.CompletedTask;

		public Task<List<VectorSearchResult>> QueryAsync(float[] vector, int limit = 10)
		{
			QueryCount++;
			LastLimit = limit;
			return Task.FromResult(_points.Take(limit).ToList());
		}

		public Task DeleteAsync(string id) => Task.CompletedTask;

		public Task<IReadOnlyList<string>> ListPointIdsAsync(CancellationToken cancellationToken = default)
			=> Task.FromResult((IReadOnlyList<string>)Array.Empty<string>());
	}

	private sealed class CapturingSemanticSearch : IMetadataSemanticSearchService
	{
		public IReadOnlyCollection<long>? LastDataSourceIds { get; private set; }

		public int ThreeArgCalls { get; private set; }

		public int FourArgCalls { get; private set; }

		public List<MetadataSemanticSearchResult> Results { get; set; } = new();

		public Task<List<MetadataSemanticSearchResult>> SearchAsync(
			string question,
			int topK = 10,
			LocaleContext? locale = null)
		{
			ThreeArgCalls++;
			return Task.FromResult(Results);
		}

		public Task<List<MetadataSemanticSearchResult>> SearchAsync(
			string question,
			int topK,
			LocaleContext? locale,
			IReadOnlyCollection<long>? dataSourceIds)
		{
			FourArgCalls++;
			LastDataSourceIds = dataSourceIds;
			return Task.FromResult(Results);
		}

		public Task<List<MetadataSemanticSearchResult>> SearchByKeywordAsync(string keyword, int limit = 30)
			=> Task.FromResult(new List<MetadataSemanticSearchResult>());

		public Task<List<MetadataSemanticSearchResult>> SearchByKeywordSubstringAsync(string keyword, int limit = 30)
			=> Task.FromResult(new List<MetadataSemanticSearchResult>());

		public Task<MetadataTable?> ResolveTableByNameAsync(
			string tableName,
			IReadOnlyCollection<long>? authorizedDataSourceIds = null)
			=> Task.FromResult<MetadataTable?>(null);
	}

	private sealed class CapturingContextBuilder : IMetadataContextBuilder
	{
		public IReadOnlyCollection<long>? LastDataSourceIds { get; private set; }

		public Task<string> BuildAsync(string question)
			=> Task.FromResult("数据库Metadata知识:");

		public Task<string> BuildAsync(string question, IReadOnlyCollection<long>? dataSourceIds)
		{
			LastDataSourceIds = dataSourceIds;
			return Task.FromResult("数据库Metadata知识:");
		}
	}

	private sealed class CapturingUnderstanding : IQueryUnderstandingService
	{
		public IReadOnlyCollection<long>? LastDataSourceIds { get; private set; }

		public Task<QueryIntent> UnderstandAsync(string question)
			=> Task.FromResult(new QueryIntent { OriginalQuestion = question });

		public Task<QueryIntent> UnderstandAsync(string question, PlatformContext platformContext)
			=> Task.FromResult(new QueryIntent { OriginalQuestion = question });

		public Task<QueryIntent> UnderstandAsync(
			string question,
			PlatformContext platformContext,
			IReadOnlyCollection<long>? dataSourceIds)
		{
			LastDataSourceIds = dataSourceIds;
			throw new InvalidOperationException("scope captured");
		}
	}

	private sealed class FakeQwen : IQwenService
	{
		public Task<string> GenerateSqlAsync(string prompt)
			=> Task.FromResult(
				"""
				{
				  "Metric": null,
				  "Metrics": [],
				  "Filters": [],
				  "Dimensions": [],
				  "OrderBy": null,
				  "OrderDirection": null,
				  "Limit": null,
				  "Explanation": ""
				}
				""");
	}
}
