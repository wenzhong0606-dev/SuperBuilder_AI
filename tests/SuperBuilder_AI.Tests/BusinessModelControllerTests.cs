using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Persistence;
using SuperBuilder_AI.Interfaces.BI.Entity;
using SuperBuilder_AI.Models.BI.Entity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.BI.Entity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M0-02 补齐契约 + M7-05 CRUD 真实持久化单测。
/// 读端点用语义化 Fake 注册表驱动，验证数据面租户策略（TenantDataPlanePolicy）+ 404 语义；
/// 写端点用真实 SuperBIContext（SQLite 内存库）+ 真实 BusinessEntityService，验证真实持久化闭环与租户隔离。
/// </summary>
public class BusinessModelControllerTests
{
	private const long Tenant5 = 5;

	private static ClaimsPrincipal AsTenant(long tid) =>
		new(new ClaimsIdentity(new[] { new Claim("tid", tid.ToString()) }));

	private sealed class FakeRegistry : IBusinessEntityRegistryService
	{
		private readonly Dictionary<long, BusinessEntity> _store = new();
		public void Seed(BusinessEntity e) => _store[e.Id] = e;
		public Task<IReadOnlyList<BusinessEntity>> ListEntitiesAsync(long tenantId, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<BusinessEntity>>(new List<BusinessEntity>());
		public Task<IReadOnlyList<BusinessDomain>> ListDomainsAsync(long tenantId, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<BusinessDomain>>(new List<BusinessDomain>());
		public Task<BusinessEntity?> GetAsync(long tenantId, long id, CancellationToken ct = default)
			=> Task.FromResult(_store.TryGetValue(id, out var e) && e.TenantId == tenantId ? e : null);
		public Task<IReadOnlyList<BusinessEntityRelationship>> ListRelationshipsAsync(long tenantId, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<BusinessEntityRelationship>>(new List<BusinessEntityRelationship>());
		public Task<IReadOnlyList<BusinessMetricView>> ListMetricsAsync(long tenantId, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<BusinessMetricView>>(new List<BusinessMetricView>());
		public Task<IReadOnlyList<BusinessDimensionView>> ListDimensionsByTenantAsync(long tenantId, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<BusinessDimensionView>>(new List<BusinessDimensionView>());
	}

	private sealed class FakeMapper : IBusinessSemanticMappingService
	{
		public Task<BusinessSemanticResolutionResult> ResolveAsync(
			long tenantId, long dataSourceId, string query, int topPerDomain = 3, CancellationToken ct = default)
			=> Task.FromResult(new BusinessSemanticResolutionResult());
	}

	private sealed class FakeEntityService : IBusinessEntityService
	{
		public Task<BusinessEntity?> GetAsync(long tenantId, long id, CancellationToken ct = default)
			=> Task.FromResult<BusinessEntity?>(null);
		public Task<IReadOnlyList<BusinessEntity>> ListAsync(long tenantId, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<BusinessEntity>>(new List<BusinessEntity>());
		public Task<BusinessEntity> CreateAsync(BusinessEntity entity, CancellationToken ct = default)
			=> Task.FromResult(entity);
		public Task<BusinessEntity> UpdateAsync(BusinessEntity entity, CancellationToken ct = default)
			=> Task.FromResult(entity);
		public Task DeleteAsync(long tenantId, long id, CancellationToken ct = default)
			=> Task.CompletedTask;
	}

	private static BusinessModelController Build(FakeRegistry registry, ClaimsPrincipal user)
	{
		var controller = new BusinessModelController(registry, new FakeMapper(), new FakeEntityService());
		controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
		return controller;
	}

	private static BusinessModelController BuildWithDb(SuperBIContext db, ClaimsPrincipal user)
	{
		var controller = new BusinessModelController(new FakeRegistry(), new FakeMapper(), new BusinessEntityService(db));
		controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
		return controller;
	}

	private static SuperBIContext CreateDb(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var ctx = NewContext(connection);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	/// <summary>在已开启的同一连接上新建独立上下文，模拟生产 per-request 作用域（避免同上下文重复键跟踪冲突）。</summary>
	private static SuperBIContext NewContext(SqliteConnection connection)
		=> new SuperBIContext(new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options);

	private static async Task SeedTenant(SuperBIContext db, long tid)
	{
		db.Tenants.Add(new Tenant { Id = tid });
		await db.SaveChangesAsync();
	}

	#region 读端点契约（M0-02）

	[Fact]
	public async Task GetEntity_Returns_200_WhenOwned()
	{
		var registry = new FakeRegistry();
		registry.Seed(new BusinessEntity { Id = 1, TenantId = Tenant5, Name = "销售订单" });
		var controller = Build(registry, AsTenant(Tenant5));

		var result = await controller.GetEntity(1, Tenant5, CancellationToken.None) as OkObjectResult;
		Assert.NotNull(result);
		var entity = Assert.IsType<BusinessEntity>(result!.Value);
		Assert.Equal(Tenant5, entity.TenantId);
	}

	[Fact]
	public async Task GetEntity_Returns_404_WhenMissing()
	{
		var registry = new FakeRegistry();
		var controller = Build(registry, AsTenant(Tenant5));

		var result = await controller.GetEntity(999, Tenant5, CancellationToken.None);
		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task GetEntity_Returns_403_WhenCrossTenant()
	{
		var registry = new FakeRegistry();
		registry.Seed(new BusinessEntity { Id = 1, TenantId = Tenant5, Name = "销售订单" });
		var controller = Build(registry, AsTenant(Tenant5));

		var result = await controller.GetEntity(1, 7, CancellationToken.None);
		var objectResult = Assert.IsType<ObjectResult>(result);
		Assert.Equal(403, objectResult.StatusCode);
	}

	#endregion

	#region M12-15 关系端点契约

	private static BusinessModelController BuildWithRealRegistry(SuperBIContext db, ClaimsPrincipal user)
	{
		var registry = new BusinessEntityRegistryService(new BusinessEntityService(db), new BusinessEntityRepository(db));
		var controller = new BusinessModelController(registry, new FakeMapper(), new FakeEntityService());
		controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
		return controller;
	}

	[Fact]
	public async Task ListRelationships_Returns_403_WhenCrossTenant()
	{
		var registry = new FakeRegistry();
		var controller = Build(registry, AsTenant(Tenant5));

		var result = await controller.ListRelationships(7, CancellationToken.None);
		var obj = Assert.IsType<ObjectResult>(result);
		Assert.Equal(403, obj.StatusCode);
	}

	[Fact]
	public async Task ListRelationships_Returns_200_AndRelationships_WhenOwned()
	{
		var db = CreateDb(out var connection);
		await SeedTenant(db, Tenant5);
		var entities = new BusinessEntityService(db);
		var e1 = await entities.CreateAsync(new BusinessEntity { TenantId = Tenant5, BusinessKey = "bk_a", Name = "实体A" }, CancellationToken.None);
		var e2 = await entities.CreateAsync(new BusinessEntity { TenantId = Tenant5, BusinessKey = "bk_b", Name = "实体B" }, CancellationToken.None);

		db.BusinessEntityRelationships.Add(new BusinessEntityRelationship
		{
			SourceEntityId = e1.Id,
			TargetEntityId = e2.Id,
			Name = "rel_ab",
			RelationshipType = "one-to-many",
		});
		await db.SaveChangesAsync();

		var controller = BuildWithRealRegistry(NewContext(connection), AsTenant(Tenant5));
		var result = await controller.ListRelationships(Tenant5, CancellationToken.None) as OkObjectResult;
		Assert.NotNull(result);
		var rels = Assert.IsAssignableFrom<IReadOnlyList<BusinessEntityRelationship>>(result!.Value);
		var rel = Assert.Single(rels);
		Assert.Equal(e1.Id, rel.SourceEntityId);
		Assert.Equal(e2.Id, rel.TargetEntityId);
		Assert.Equal("rel_ab", rel.Name);
	}

	[Fact]
	public async Task ListRelationships_Returns_200_Empty_WhenNoRelationships()
	{
		var db = CreateDb(out _);
		await SeedTenant(db, Tenant5);
		var controller = BuildWithRealRegistry(db, AsTenant(Tenant5));

		var result = await controller.ListRelationships(Tenant5, CancellationToken.None) as OkObjectResult;
		Assert.NotNull(result);
		var rels = Assert.IsAssignableFrom<IReadOnlyList<BusinessEntityRelationship>>(result!.Value);
		Assert.Empty(rels);
	}

	#endregion

	#region M12-16 指标 / 维度治理端点契约（真实持久化）

	/// <summary>种一个业务域 + 一个实体 + 两个指标（其一为计算字段）+ 一个维度。</summary>
	private static async Task SeedGovernance(SuperBIContext db, long tid)
	{
		var domain = new BusinessDomain { TenantId = tid, Name = "仓储域", Description = "库存与出入库" };
		db.BusinessDomains.Add(domain);
		await db.SaveChangesAsync();

		var entity = new BusinessEntity
		{
			TenantId = tid,
			BusinessKey = "bk_stock",
			Name = "库存",
			DisplayName = "库存实体",
			BusinessDomain = "仓储域",
		};
		db.BusinessEntities.Add(entity);
		await db.SaveChangesAsync();

		db.BusinessEntityMetrics.Add(new BusinessEntityMetric
		{
			BusinessEntityId = entity.Id,
			Name = "库存量",
			DisplayName = "库存量",
			SemanticType = "quantity",
			Aggregation = "sum",
		});
		db.BusinessEntityMetrics.Add(new BusinessEntityMetric
		{
			BusinessEntityId = entity.Id,
			Name = "周转率",
			SemanticType = "ratio",
			Aggregation = "avg",
			IsCalculated = true,
		});
		db.BusinessEntityDimensions.Add(new BusinessEntityDimension
		{
			TenantId = tid,
			BusinessDomainId = domain.Id,
			Name = "仓库",
			Description = "仓库维度",
		});
		await db.SaveChangesAsync();
	}

	[Fact]
	public async Task ListMetrics_Returns_403_WhenCrossTenant()
	{
		var controller = Build(new FakeRegistry(), AsTenant(Tenant5));

		var result = await controller.ListMetrics(7, CancellationToken.None);
		var obj = Assert.IsType<ObjectResult>(result);
		Assert.Equal(403, obj.StatusCode);
	}

	[Fact]
	public async Task ListMetrics_Returns_200_And_Projects_Entity_And_Flags()
	{
		var db = CreateDb(out var connection);
		await SeedTenant(db, Tenant5);
		await SeedGovernance(db, Tenant5);

		var controller = BuildWithRealRegistry(NewContext(connection), AsTenant(Tenant5));
		var result = await controller.ListMetrics(Tenant5, CancellationToken.None) as OkObjectResult;
		Assert.NotNull(result);
		var metrics = Assert.IsAssignableFrom<IReadOnlyList<BusinessMetricView>>(result!.Value);
		Assert.Equal(2, metrics.Count);

		var stock = Assert.Single(metrics, m => m.Name == "库存量");
		Assert.Equal("库存", stock.EntityName);
		Assert.Equal("库存实体", stock.EntityDisplayName);
		Assert.Equal("仓储域", stock.BusinessDomain);
		Assert.Equal("sum", stock.Aggregation);
		Assert.False(stock.IsCalculated);
		Assert.Equal(0, stock.PhysicalBindingCount);

		var ratio = Assert.Single(metrics, m => m.Name == "周转率");
		Assert.True(ratio.IsCalculated);
	}

	[Fact]
	public async Task ListMetrics_Excludes_Other_Tenant()
	{
		var db = CreateDb(out var connection);
		await SeedTenant(db, Tenant5);
		// 第二个租户需显式 TenantCode：Tenants.TenantCode 唯一约束，默认空串会冲突。
		db.Tenants.Add(new Tenant { Id = 7, TenantCode = "t7" });
		await db.SaveChangesAsync();
		await SeedGovernance(db, Tenant5);
		await SeedGovernance(db, 7);

		var controller = BuildWithRealRegistry(NewContext(connection), AsTenant(7));
		var result = await controller.ListMetrics(7, CancellationToken.None) as OkObjectResult;
		Assert.NotNull(result);
		var metrics = Assert.IsAssignableFrom<IReadOnlyList<BusinessMetricView>>(result!.Value);

		// 两租户各 2 条同名指标：隔离正确时应恰好返回本租户的 2 条，且实体归属为本租户实体。
		Assert.Equal(2, metrics.Count);
		var entityIds = metrics.Select(m => m.BusinessEntityId).Distinct().ToList();
		Assert.Single(entityIds);
		var ownEntityIds = await NewContext(connection).BusinessEntities
			.Where(e => e.TenantId == 7).Select(e => e.Id).ToListAsync();
		Assert.Contains(entityIds[0], ownEntityIds);
	}

	[Fact]
	public async Task ListDimensions_Returns_200_With_Domain_Name()
	{
		var db = CreateDb(out var connection);
		await SeedTenant(db, Tenant5);
		await SeedGovernance(db, Tenant5);

		var controller = BuildWithRealRegistry(NewContext(connection), AsTenant(Tenant5));
		var result = await controller.ListDimensionsByTenant(Tenant5, CancellationToken.None) as OkObjectResult;
		Assert.NotNull(result);
		var dims = Assert.IsAssignableFrom<IReadOnlyList<BusinessDimensionView>>(result!.Value);
		var dim = Assert.Single(dims);
		Assert.Equal("仓库", dim.Name);
		Assert.Equal("仓储域", dim.DomainName);
		Assert.Equal("仓库维度", dim.Description);
	}

	[Fact]
	public async Task ListDimensions_Returns_403_WhenCrossTenant()
	{
		var controller = Build(new FakeRegistry(), AsTenant(Tenant5));

		var result = await controller.ListDimensionsByTenant(7, CancellationToken.None);
		var obj = Assert.IsType<ObjectResult>(result);
		Assert.Equal(403, obj.StatusCode);
	}

	#endregion

	#region M7-05 写端点（真实持久化闭环）

	[Fact]
	public async Task CreateEntity_Returns_Created_AndPersists()
	{
		using var db = CreateDb(out _);
		await SeedTenant(db, Tenant5);
		var controller = BuildWithDb(db, AsTenant(Tenant5));
		var dto = new BusinessEntityUpsertRequest { BusinessKey = "bk_sales", Name = "销售订单", DisplayName = "订单" };

		var result = await controller.CreateEntity(dto, Tenant5, CancellationToken.None);
		var created = Assert.IsType<CreatedAtActionResult>(result);
		Assert.Equal(201, created.StatusCode);
		var entity = Assert.IsType<BusinessEntity>(created.Value);
		Assert.True(entity.Id > 0);

		var persisted = await new BusinessEntityService(db).GetAsync(Tenant5, entity.Id, CancellationToken.None);
		Assert.NotNull(persisted);
		Assert.Equal("销售订单", persisted!.Name);
		Assert.Equal("bk_sales", persisted.BusinessKey);
		Assert.Equal(Tenant5, persisted.TenantId);
	}

	[Fact]
	public async Task CreateEntity_Returns_400_WhenMissingRequired()
	{
		using var db = CreateDb(out _);
		await SeedTenant(db, Tenant5);
		var controller = BuildWithDb(db, AsTenant(Tenant5));
		var dto = new BusinessEntityUpsertRequest { BusinessKey = "bk", Name = "" };

		var result = await controller.CreateEntity(dto, Tenant5, CancellationToken.None);
		var bad = Assert.IsType<BadRequestObjectResult>(result);
		Assert.Equal(400, bad.StatusCode);
	}

	[Fact]
	public async Task UpdateEntity_Returns_Ok_AndPersists()
	{
		var db = CreateDb(out var connection);
		await SeedTenant(db, Tenant5);
		var created = await BuildWithDb(db, AsTenant(Tenant5))
			.CreateEntity(new BusinessEntityUpsertRequest { BusinessKey = "bk_u", Name = "原名称" }, Tenant5, CancellationToken.None);
		var id = Assert.IsType<BusinessEntity>(((CreatedAtActionResult)created).Value).Id;

		// 新上下文模拟生产 per-request 作用域，避免同上下文重复键跟踪冲突
		var updateController = BuildWithDb(NewContext(connection), AsTenant(Tenant5));
		var result = await updateController.UpdateEntity(id, new BusinessEntityUpsertRequest { BusinessKey = "bk_u", Name = "新名称" }, Tenant5, CancellationToken.None);
		var ok = Assert.IsType<OkObjectResult>(result);
		Assert.Equal("新名称", Assert.IsType<BusinessEntity>(ok.Value).Name);

		var persisted = await new BusinessEntityService(NewContext(connection)).GetAsync(Tenant5, id, CancellationToken.None);
		Assert.Equal("新名称", persisted!.Name);
	}

	[Fact]
	public async Task UpdateEntity_Returns_404_WhenMissing()
	{
		using var db = CreateDb(out _);
		await SeedTenant(db, Tenant5);
		var controller = BuildWithDb(db, AsTenant(Tenant5));

		var result = await controller.UpdateEntity(999, new BusinessEntityUpsertRequest { BusinessKey = "bk", Name = "x" }, Tenant5, CancellationToken.None);
		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task DeleteEntity_Returns_204_AndRemoves()
	{
		using var db = CreateDb(out _);
		await SeedTenant(db, Tenant5);
		var controller = BuildWithDb(db, AsTenant(Tenant5));
		var created = await controller.CreateEntity(new BusinessEntityUpsertRequest { BusinessKey = "bk_d", Name = "待删" }, Tenant5, CancellationToken.None);
		var id = Assert.IsType<BusinessEntity>(((CreatedAtActionResult)created).Value).Id;

		var result = await controller.DeleteEntity(id, Tenant5, CancellationToken.None);
		Assert.IsType<NoContentResult>(result);

		var persisted = await new BusinessEntityService(db).GetAsync(Tenant5, id, CancellationToken.None);
		Assert.Null(persisted);
	}

	[Fact]
	public async Task CreateEntity_Returns_403_WhenCrossTenant()
	{
		using var db = CreateDb(out _);
		await SeedTenant(db, Tenant5);
		var controller = BuildWithDb(db, AsTenant(Tenant5));

		var result = await controller.CreateEntity(new BusinessEntityUpsertRequest { BusinessKey = "bk", Name = "x" }, 7, CancellationToken.None);
		var obj = Assert.IsType<ObjectResult>(result);
		Assert.Equal(403, obj.StatusCode);
	}

	[Fact]
	public async Task UpdateEntity_Returns_403_WhenCrossTenant()
	{
		using var db = CreateDb(out _);
		await SeedTenant(db, Tenant5);
		var controller = BuildWithDb(db, AsTenant(Tenant5));

		var result = await controller.UpdateEntity(1, new BusinessEntityUpsertRequest { BusinessKey = "bk", Name = "x" }, 7, CancellationToken.None);
		var obj = Assert.IsType<ObjectResult>(result);
		Assert.Equal(403, obj.StatusCode);
	}

	#endregion
}
