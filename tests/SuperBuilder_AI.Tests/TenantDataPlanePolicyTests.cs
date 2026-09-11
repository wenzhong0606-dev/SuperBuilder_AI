using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Interfaces.BI.Entity;
using SuperBuilder_AI.Models.BI.Entity;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class TenantDataPlanePolicyTests
{
	[Fact]
	public void Resolve_UsesAuthenticatedTenantAndRejectsAnyDifferentRequestSource()
	{
		var principal = Principal(7);

		var accepted = TenantDataPlanePolicy.Resolve(principal, null, 7);
		var rejected = TenantDataPlanePolicy.Resolve(principal, 7, 8);

		Assert.True(accepted.Authorized);
		Assert.Equal(7, accepted.EffectiveTenantId);
		Assert.False(rejected.Authorized);
		Assert.Equal(7, rejected.EffectiveTenantId);
	}

	[Fact]
	public void ResolvePlatformScope_SameTenant_AuthorizedWithAuthenticatedTenant()
	{
		var accepted = TenantDataPlanePolicy.ResolvePlatformScope(Principal(7), 7);
		Assert.True(accepted.Authorized);
		Assert.Equal(7, accepted.EffectiveTenantId);
	}

	[Fact]
	public void ResolvePlatformScope_CrossTenantRequest_Rejected()
	{
		var rejected = TenantDataPlanePolicy.ResolvePlatformScope(Principal(7), 8);
		Assert.False(rejected.Authorized);
		Assert.Equal(7, rejected.EffectiveTenantId);
	}

	[Fact]
	public void ResolvePlatformScope_OmittedRequest_UsesAuthenticatedTenant()
	{
		var accepted = TenantDataPlanePolicy.ResolvePlatformScope(Principal(7));
		Assert.True(accepted.Authorized);
		Assert.Equal(7, accepted.EffectiveTenantId);
	}

	[Fact]
	public void ResolvePlatformScope_GlobalTemplateIntent_AuthorizedWithAuthenticatedTenant()
	{
		var accepted = TenantDataPlanePolicy.ResolvePlatformScope(Principal(7), 0);
		Assert.True(accepted.Authorized);
		Assert.Equal(7, accepted.EffectiveTenantId);
	}

	[Fact]
	public void ResolvePlatformScope_NoTenantClaim_FallsBackToRequestedTenant()
	{
		// 仅测试直调场景（生产由 AuthMiddleware 注入 tid，不可达）。退化为以请求租户为有效租户。
		var fallback = TenantDataPlanePolicy.ResolvePlatformScope(null, 5);
		Assert.True(fallback.Authorized);
		Assert.Equal(5, fallback.EffectiveTenantId);
	}

	[Fact]
	public async Task BusinessModel_CrossTenantQuery_Returns403WithoutCallingService()
	{
		var registry = new RegistryStub();
		var controller = new BusinessModelController(registry, new MapperStub(), new EntityServiceStub())
		{
			ControllerContext = new ControllerContext { HttpContext = Context(7) }
		};

		var result = await controller.ListEntities(8, CancellationToken.None);

		var forbidden = Assert.IsType<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
		Assert.Equal(0, registry.Calls);
	}

	[Fact]
	public async Task BusinessModel_OmittedTenant_UsesAuthenticatedTenant()
	{
		var registry = new RegistryStub();
		var controller = new BusinessModelController(registry, new MapperStub(), new EntityServiceStub())
		{
			ControllerContext = new ControllerContext { HttpContext = Context(7) }
		};

		var result = await controller.ListEntities(null, CancellationToken.None);

		Assert.IsType<OkObjectResult>(result);
		Assert.Equal(7, registry.LastTenantId);
	}

	private static ClaimsPrincipal Principal(long tenantId) => new(new ClaimsIdentity(
		new[] { new Claim("tid", tenantId.ToString()) }, "Bearer"));

	private static DefaultHttpContext Context(long tenantId)
	{
		var context = new DefaultHttpContext();
		context.User = Principal(tenantId);
		return context;
	}

	private sealed class RegistryStub : IBusinessEntityRegistryService
	{
		public int Calls { get; private set; }
		public long LastTenantId { get; private set; }

		public Task<IReadOnlyList<BusinessEntity>> ListEntitiesAsync(long tenantId, CancellationToken cancellationToken = default)
		{
			Calls++;
			LastTenantId = tenantId;
			return Task.FromResult<IReadOnlyList<BusinessEntity>>(Array.Empty<BusinessEntity>());
		}

		public Task<IReadOnlyList<BusinessDomain>> ListDomainsAsync(long tenantId, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<BusinessDomain>>(Array.Empty<BusinessDomain>());

		public Task<BusinessEntity?> GetAsync(long tenantId, long id, CancellationToken cancellationToken = default)
			=> Task.FromResult<BusinessEntity?>(null);

		public Task<IReadOnlyList<BusinessEntityRelationship>> ListRelationshipsAsync(long tenantId, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<BusinessEntityRelationship>>(Array.Empty<BusinessEntityRelationship>());

		public Task<IReadOnlyList<BusinessMetricView>> ListMetricsAsync(long tenantId, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<BusinessMetricView>>(Array.Empty<BusinessMetricView>());

		public Task<IReadOnlyList<BusinessDimensionView>> ListDimensionsByTenantAsync(long tenantId, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<BusinessDimensionView>>(Array.Empty<BusinessDimensionView>());
	}

	private sealed class MapperStub : IBusinessSemanticMappingService
	{
		public Task<BusinessSemanticResolutionResult> ResolveAsync(long tenantId, long dataSourceId, string query, int topPerDomain = 3, CancellationToken cancellationToken = default)
			=> throw new InvalidOperationException("Mapper must not be called by these tests.");
	}

	private sealed class EntityServiceStub : IBusinessEntityService
	{
		public Task<BusinessEntity?> GetAsync(long tenantId, long id, CancellationToken cancellationToken = default)
			=> Task.FromResult<BusinessEntity?>(null);
		public Task<IReadOnlyList<BusinessEntity>> ListAsync(long tenantId, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<BusinessEntity>>(Array.Empty<BusinessEntity>());
		public Task<BusinessEntity> CreateAsync(BusinessEntity entity, CancellationToken cancellationToken = default)
			=> Task.FromResult(entity);
		public Task<BusinessEntity> UpdateAsync(BusinessEntity entity, CancellationToken cancellationToken = default)
			=> Task.FromResult(entity);
		public Task DeleteAsync(long tenantId, long id, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;
		public Task UpsertMetricsAsync(long tenantId, long entityId, IReadOnlyList<BusinessEntityMetric> metrics, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;
		public Task UpsertDimensionsAsync(long tenantId, long domainId, IReadOnlyList<BusinessEntityDimension> dimensions, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;
	}
}
