using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M1-05：Metadata / Vector 字段与数据完整性测试。
/// </summary>
public class MetadataVectorIntegrityTests
{
	#region 受控词表与结构化辅助（纯单元）

	[Fact]
	public void ParseList_去空白去空项去重()
	{
		var list = MetadataSemantic.ParseList("销售金额, 收入;营业额\n, 收入");
		Assert.Equal(3, list.Count);
		Assert.Contains("销售金额", list);
		Assert.Contains("收入", list);
		Assert.Contains("营业额", list);
	}

	[Fact]
	public void FormatList_规范化为逗号分隔()
	{
		var text = MetadataSemantic.FormatList(new[] { "a", "b", "c" });
		Assert.Equal("a, b, c", text);
	}

	[Fact]
	public void VocabularyValidator_识别合法与非法值()
	{
		Assert.True(MetadataVocabularyValidator.IsValidRelationshipType("OneToMany"));
		Assert.True(MetadataVocabularyValidator.IsValidRelationshipType("onetoone"));
		Assert.False(MetadataVocabularyValidator.IsValidRelationshipType("nonsense"));
		Assert.True(MetadataVocabularyValidator.IsValidPhysicalRole("Key"));
		Assert.False(MetadataVocabularyValidator.IsValidPhysicalRole("Bogus"));
	}

	[Fact]
	public void SemanticSource_默认Manual且可解析()
	{
		Assert.Equal(SemanticSource.Manual, new MetadataSemantic().Source);
		Assert.True(Enum.TryParse<SemanticSource>("ai", ignoreCase: true, out _));
	}

	#endregion

	#region 数据库约束（SQLite :memory:）

	private static SuperBIContext CreateContext()
	{
		var connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>()
			.UseSqlite(connection)
			.Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		// 播种父行以满足 DataSource / Tenant 外键。
		ctx.Tenants.Add(new SuperBuilder_AI.Models.Organization.Tenant
		{
			Id = 1,
			TenantCode = "seed",
			TenantName = "seed"
		});
		ctx.DataSources.Add(new SuperBuilder_AI.Models.Metadata.DataSource
		{
			Id = 1,
			TenantId = 1,
			Name = "ds",
			NormalizedName = "ds",
			DbType = "MYSQL"
		});
		ctx.SaveChanges();
		return ctx;
	}

	[Fact]
	public void MetadataTable_同DataSource_Catalog_Schema_Table_唯一()
	{
		using var ctx = CreateContext();
		ctx.MetadataTables.Add(new MetadataTable
		{
			TenantId = 1,
			DataSourceId = 1,
			CatalogName = "c",
			SchemaName = "s",
			TableName = "t"
		});
		ctx.MetadataTables.Add(new MetadataTable
		{
			TenantId = 1,
			DataSourceId = 1,
			CatalogName = "c",
			SchemaName = "s",
			TableName = "t"
		});
		Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
	}

	[Fact]
	public void MetadataTable_不同Catalog_Schema_允许同名()
	{
		using var ctx = CreateContext();
		ctx.MetadataTables.Add(new MetadataTable
		{
			TenantId = 1,
			DataSourceId = 1,
			CatalogName = "c1",
			SchemaName = "s1",
			TableName = "t"
		});
		ctx.MetadataTables.Add(new MetadataTable
		{
			TenantId = 1,
			DataSourceId = 1,
			CatalogName = "c2",
			SchemaName = "s2",
			TableName = "t"
		});
		ctx.SaveChanges();
		Assert.Equal(2, ctx.MetadataTables.Count());
	}

	[Fact]
	public void MetadataTable_空Catalog_Schema_不唯一约束_兼容存量()
	{
		using var ctx = CreateContext();
		// 过滤唯一索引仅在 Catalog/Schema 非空时生效，
		// 因此两个 NULL Catalog/Schema + 同名表可共存（兼容旧数据）。
		ctx.MetadataTables.Add(new MetadataTable
		{
			TenantId = 1,
			DataSourceId = 1,
			TableName = "t"
		});
		ctx.MetadataTables.Add(new MetadataTable
		{
			TenantId = 1,
			DataSourceId = 1,
			TableName = "t"
		});
		ctx.SaveChanges();
		Assert.Equal(2, ctx.MetadataTables.Count());
	}

	[Fact]
	public void MetadataColumn_ColumnName_必填()
	{
		using var ctx = CreateContext();
		var table = new MetadataTable
		{
			TenantId = 1,
			DataSourceId = 1,
			TableName = "t"
		};
		ctx.MetadataTables.Add(table);
		ctx.SaveChanges();

		ctx.MetadataColumns.Add(new MetadataColumn
		{
			MetadataTableId = table.Id,
			ColumnName = null
		});
		Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
	}

	[Fact]
	public void MetadataColumn_新增字段可持久化()
	{
		using var ctx = CreateContext();
		var table = new MetadataTable
		{
			TenantId = 1,
			DataSourceId = 1,
			TableName = "t"
		};
		ctx.MetadataTables.Add(table);
		ctx.SaveChanges();

		ctx.MetadataColumns.Add(new MetadataColumn
		{
			MetadataTableId = table.Id,
			ColumnName = "amount",
			Ordinal = 2,
			NativeType = "decimal",
			Precision = 18,
			Scale = 4
		});
		ctx.SaveChanges();

		var col = ctx.MetadataColumns.Single();
		Assert.Equal(2, col.Ordinal);
		Assert.Equal("decimal", col.NativeType);
		Assert.Equal(18, col.Precision);
		Assert.Equal(4, col.Scale);
	}

	[Fact]
	public void MetadataSemantic_Confidence_越界被CHECK拒绝()
	{
		using var ctx = CreateContext();
		var table = new MetadataTable
		{
			TenantId = 1,
			DataSourceId = 1,
			TableName = "t"
		};
		ctx.MetadataTables.Add(table);
		ctx.SaveChanges();

		var column = new MetadataColumn
		{
			MetadataTableId = table.Id,
			ColumnName = "c"
		};
		ctx.MetadataColumns.Add(column);
		ctx.SaveChanges();

		ctx.MetadataSemantics.Add(new MetadataSemantic
		{
			MetadataColumnId = column.Id,
			Confidence = 2.5m
		});
		Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
	}

	[Fact]
	public void MetadataSemantic_Confidence_边界值通过()
	{
		using var ctx = CreateContext();
		var table = new MetadataTable
		{
			TenantId = 1,
			DataSourceId = 1,
			TableName = "t"
		};
		ctx.MetadataTables.Add(table);
		ctx.SaveChanges();

		var column = new MetadataColumn
		{
			MetadataTableId = table.Id,
			ColumnName = "c"
		};
		ctx.MetadataColumns.Add(column);
		ctx.SaveChanges();

		ctx.MetadataSemantics.Add(new MetadataSemantic
		{
			MetadataColumnId = column.Id,
			Confidence = 1.0m
		});
		ctx.SaveChanges();
		Assert.Equal(1.0m, ctx.MetadataSemantics.Single().Confidence);
	}

	[Fact]
	public void LearningRecord_TenantId_为必填外键()
	{
		using var ctx = CreateContext();
		ctx.LearningRecords.Add(new MetadataLearningRecord
		{
			// 引用不存在的租户，应触发 FK 约束失败。
			TenantId = 999,
			Question = "q"
		});
		Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
	}

	[Fact]
	public void LearningRecord_同租户一致可写入()
	{
		using var ctx = CreateContext();
		ctx.LearningRecords.Add(new MetadataLearningRecord
		{
			TenantId = 1,
			Question = "q"
		});
		ctx.SaveChanges();
		Assert.Single(ctx.LearningRecords);
	}

	#endregion

	#region 向量状态记录与孤儿检测（Fake 服务）

	private sealed class FakeEmbedding : IEmbeddingService
	{
		private readonly bool _throw;
		public FakeEmbedding(bool throwOnGenerate = false)
			=> _throw = throwOnGenerate;

		public int Dimension => 4;
		public string ModelName => "fake";
		public Task<float[]> GenerateAsync(string text, string textType = "document")
		{
			if (_throw)
			{
				throw new InvalidOperationException("embedding failed");
			}

			return Task.FromResult(new float[] { 1, 2, 3, 4 });
		}
	}

	private sealed class FakeQdrant : IQdrantService
	{
		public List<string> PointIds { get; set; } = new();
		public List<string> Upserted { get; } = new();

		public Task CreateCollectionAsync() => Task.CompletedTask;
		public Task<bool> ExistsAsync() => Task.FromResult(true);
		public Task RecreateCollectionAsync() => Task.CompletedTask;
		public Task UpsertAsync(string id, float[] vector, Dictionary<string, object> payload)
		{
			Upserted.Add(id);
			return Task.CompletedTask;
		}
		public Task<List<VectorSearchResult>> QueryAsync(float[] vector, int limit = 10)
			=> Task.FromResult(new List<VectorSearchResult>());
		public Task DeleteAsync(string id) => Task.CompletedTask;
		public Task<IReadOnlyList<string>> ListPointIdsAsync(CancellationToken cancellationToken = default)
			=> Task.FromResult((IReadOnlyList<string>)PointIds);
	}

	[Fact]
	public async Task MetadataVectorService_成功时记录Synced与维度()
	{
		var embedding = new FakeEmbedding();
		var qdrant = new FakeQdrant();
		var svc = new MetadataVectorService(embedding, qdrant);

		var table = new MetadataTable
		{
			Id = 1,
			TenantId = 1,
			DataSourceId = 1,
			TableName = "t",
			SearchText = "table text"
		};
		var column = new MetadataColumn
		{
			Id = 10,
			MetadataTableId = 1,
			ColumnName = "c",
			SearchText = "col text"
		};
		var semantic = new MetadataSemantic
		{
			Id = 100,
			MetadataColumnId = 10,
			SearchText = "sem text"
		};
		column.Semantic = semantic;
		table.Columns = new List<MetadataColumn> { column };

		await svc.IndexAsync(table);

		Assert.Equal("Synced", table.VectorStatus);
		Assert.Equal(4, table.VectorDimension);
		Assert.Equal("Synced", column.VectorStatus);
		Assert.Equal("Synced", semantic.VectorStatus);
		Assert.Equal(3, qdrant.Upserted.Count);
	}

	[Fact]
	public async Task MetadataVectorService_嵌入失败时标记Failed并记录错误类型()
	{
		var embedding = new FakeEmbedding(throwOnGenerate: true);
		var qdrant = new FakeQdrant();
		var svc = new MetadataVectorService(embedding, qdrant);

		var table = new MetadataTable
		{
			Id = 1,
			TenantId = 1,
			DataSourceId = 1,
			TableName = "t",
			SearchText = "table text"
		};

		await svc.IndexAsync(table);

		Assert.Equal("Failed", table.VectorStatus);
		Assert.Equal("InvalidOperationException", table.VectorErrorCode);
	}

	[Fact]
	public async Task DetectOrphans_返回Qdrant中多余Point()
	{
		using var ctx = CreateContext();
		ctx.MetadataTables.Add(new MetadataTable
		{
			TenantId = 1,
			DataSourceId = 1,
			TableName = "t",
			VectorId = "v1"
		});
		ctx.SaveChanges();

		var qdrant = new FakeQdrant
		{
			PointIds = new List<string> { "v1", "orphan" }
		};
		var svc = new MetadataVectorIndexService(
			ctx,
			qdrant,
			null!,
			Options.Create(new QdrantOptions()));

		var result = await svc.DetectOrphansAsync();

		Assert.Equal(1, result.OrphanCount);
		Assert.Contains("orphan", result.OrphanIds);
		Assert.Equal(1, result.DatabaseVectorCount);
	}

	#endregion
}
