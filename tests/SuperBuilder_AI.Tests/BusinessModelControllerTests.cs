using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Interfaces.BI.Entity;
using SuperBuilder_AI.Models.BI.Entity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M0-02 补齐契约：GET api/business-model/entities/{id} 单测。
/// 用语义化 Fake 注册表驱动，重点验证数据面租户策略（TenantDataPlanePolicy）+ 404 语义，不触碰真实查询链路。
/// 覆盖：200 所属租户可见、404 不存在、403 跨租户越权。
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
	}

	private sealed class FakeMapper : IBusinessSemanticMappingService
	{
		public Task<BusinessSemanticResolutionResult> ResolveAsync(
			long tenantId, long dataSourceId, string query, int topPerDomain = 3, CancellationToken ct = default)
			=> Task.FromResult(new BusinessSemanticResolutionResult());
	}

	private static BusinessModelController Build(FakeRegistry registry, ClaimsPrincipal user)
	{
		var controller = new BusinessModelController(registry, new FakeMapper());
		controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
		return controller;
	}

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
		// 认证租户为 5，却请求租户 7 → 数据面越权。
		var controller = Build(registry, AsTenant(Tenant5));

		var result = await controller.GetEntity(1, 7, CancellationToken.None);
		var objectResult = Assert.IsType<ObjectResult>(result);
		Assert.Equal(403, objectResult.StatusCode);
	}
}
