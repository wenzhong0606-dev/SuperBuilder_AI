using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.BI.Entity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.BI.Entity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M0-06：BusinessEntity 聚合写入须校验每条 PhysicalBinding 的 Tenant → DataSource → Table → Column 完整链。
/// </summary>
public class BusinessEntityServiceTests
{
	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private static (long dsId, long tableId, long columnId) SeedChain(SuperBIContext ctx, long tenantId)
	{
		ctx.Tenants.Add(new Tenant { Id = tenantId, TenantCode = $"t{tenantId}", TenantName = $"Tenant {tenantId}" });
		var ds = new DataSource { Id = tenantId * 100 + 1, TenantId = tenantId, Name = $"ds{tenantId}", NormalizedName = $"ds{tenantId}", DbType = "SQLSERVER", ConnectionString = "x" };
		var table = new MetadataTable { Id = tenantId * 100 + 2, TenantId = tenantId, DataSourceId = ds.Id, TableName = $"t{tenantId}" };
		var column = new MetadataColumn { Id = tenantId * 100 + 3, MetadataTableId = table.Id, ColumnName = $"c{tenantId}" };
		ctx.DataSources.Add(ds);
		ctx.MetadataTables.Add(table);
		ctx.MetadataColumns.Add(column);
		ctx.SaveChanges();
		return (ds.Id, table.Id, column.Id);
	}

	[Fact]
	public async Task Create_ValidBindingChain_Succeeds()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var (dsId, tableId, columnId) = SeedChain(ctx, 5);

		var entity = new BusinessEntity
		{
			TenantId = 5,
			Name = "入库凭证",
			Keys = new List<BusinessEntityKey>
			{
				new BusinessEntityKey
				{
					Name = "Id",
					PhysicalBindings = new List<PhysicalBinding>
					{
						new PhysicalBinding
						{
							DataSourceId = dsId,
							MetadataTableId = tableId,
							MetadataColumnId = columnId,
							PhysicalRole = "Key",
							IsActive = true,
						},
					},
				},
			},
		};

		var saved = await new BusinessEntityService(ctx).CreateAsync(entity);
		Assert.Equal(5, saved.TenantId);
		Assert.True(await ctx.BusinessEntities.AnyAsync(b => b.Id == saved.Id && b.TenantId == 5));
	}

	[Fact]
	public async Task Create_CrossTenantBindingChain_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 租户 5 与租户 7 各自完整的链
		SeedChain(ctx, 5);
		var (ds7, table7, col7) = SeedChain(ctx, 7);

		// 实体归属租户 5，但 PhysicalBinding 指向租户 7 的 DataSource → 链不一致/越权
		var entity = new BusinessEntity
		{
			TenantId = 5,
			Name = "伪造",
			Keys = new List<BusinessEntityKey>
			{
				new BusinessEntityKey
				{
					Name = "Id",
					PhysicalBindings = new List<PhysicalBinding>
					{
						new PhysicalBinding
						{
							DataSourceId = ds7,
							MetadataTableId = table7,
							MetadataColumnId = col7,
							PhysicalRole = "Key",
							IsActive = true,
						},
					},
				},
			},
		};

		await Assert.ThrowsAsync<System.InvalidOperationException>(
			() => new BusinessEntityService(ctx).CreateAsync(entity));
	}

	[Fact]
	public async Task UpsertMetrics_ByEntity_MergesByName_AndKeepsBindings()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var (dsId, tableId, columnId) = SeedChain(ctx, 5);
		var entity = new BusinessEntity { TenantId = 5, Name = "销售订单", BusinessDomain = "销售" };
		ctx.BusinessEntities.Add(entity);
		await ctx.SaveChangesAsync();

		var metric = new BusinessEntityMetric { BusinessEntityId = entity.Id, Name = "销售额", Aggregation = "sum" };
		ctx.BusinessEntityMetrics.Add(metric);
		await ctx.SaveChangesAsync();
		ctx.BusinessEntityMetrics.First(m => m.Name == "销售额").PhysicalBindings.Add(new PhysicalBinding
		{
			DataSourceId = dsId, MetadataTableId = tableId, MetadataColumnId = columnId, PhysicalRole = "Measure", IsActive = true,
		});
		await ctx.SaveChangesAsync();

		var svc = new BusinessEntityService(ctx);
		await svc.UpsertMetricsAsync(5, entity.Id, new List<BusinessEntityMetric>
		{
			new() { Name = "销售额", Aggregation = "sum", Expression = "sum(amount)" },
			new() { Name = "订单数", Aggregation = "count" },
		}, CancellationToken.None);

		var metrics = await ctx.BusinessEntityMetrics.Where(m => m.BusinessEntityId == entity.Id).OrderBy(m => m.Name).ToListAsync();
		Assert.Equal(2, metrics.Count);
		var sales = metrics.Single(m => m.Name == "销售额");
		Assert.Equal("sum(amount)", sales.Expression);
		Assert.Equal(1, await ctx.PhysicalBindings.CountAsync(p => p.BusinessEntityMetricId == sales.Id));
		Assert.True(metrics.Any(m => m.Name == "订单数"));
	}

	[Fact]
	public async Task UpsertMetrics_Rejects_WhenEntityBelongsToOtherTenant()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		SeedChain(ctx, 5);
		var entity = new BusinessEntity { TenantId = 5, Name = "销售订单" };
		ctx.BusinessEntities.Add(entity);
		await ctx.SaveChangesAsync();

		var svc = new BusinessEntityService(ctx);
		await Assert.ThrowsAsync<KeyNotFoundException>(() =>
			svc.UpsertMetricsAsync(7, entity.Id, new List<BusinessEntityMetric> { new() { Name = "x" } }, CancellationToken.None));
	}

	[Fact]
	public async Task UpsertDimensions_ByDomain_MergesByName()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		SeedChain(ctx, 5);
		ctx.BusinessDomains.Add(new BusinessDomain { Id = 50, TenantId = 5, Name = "销售域" });
		await ctx.SaveChangesAsync();

		var svc = new BusinessEntityService(ctx);
		await svc.UpsertDimensionsAsync(5, 50, new List<BusinessEntityDimension>
		{
			new() { Name = "仓库", Description = "货位维度" },
			new() { Name = "入库日期", Expression = "date_trunc('month', created_at)" },
		}, CancellationToken.None);

		var dims = await ctx.BusinessEntityDimensions.Where(d => d.BusinessDomainId == 50).ToListAsync();
		Assert.Equal(2, dims.Count);
		Assert.Equal("date_trunc('month', created_at)", dims.Single(d => d.Name == "入库日期").Expression);

		await svc.UpsertDimensionsAsync(5, 50, new List<BusinessEntityDimension> { new() { Name = "仓库" } }, CancellationToken.None);
		dims = await ctx.BusinessEntityDimensions.Where(d => d.BusinessDomainId == 50).ToListAsync();
		Assert.Single(dims);
		Assert.Equal("仓库", dims[0].Name);
	}
}
