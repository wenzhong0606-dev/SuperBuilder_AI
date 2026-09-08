using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class DataSourceAuthorizationServiceTests
{
	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var context = new SuperBIContext(new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options);
		context.Database.EnsureCreated();
		return context;
	}

	private static async Task SeedAsync(SuperBIContext db)
	{
		db.Tenants.AddRange(
			new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = true },
			new Tenant { Id = 2, TenantCode = "t2", TenantName = "T2", Enabled = true });
		db.Users.AddRange(
			new User { Id = 10, TenantId = 1, Username = "u1" },
			new User { Id = 20, TenantId = 2, Username = "u2" });
		db.Roles.Add(new Role { Id = 30, TenantId = 1, Code = "analyst", Name = "Analyst" });
		db.UserRoles.Add(new UserRole { Id = 40, TenantId = 1, UserId = 10, RoleId = 30 });
		db.DataSources.AddRange(
			new DataSource { Id = 100, TenantId = 1, Name = "A", DbType = "sqlserver", ConnectionString = "x", Enabled = true },
			new DataSource { Id = 200, TenantId = 2, Name = "B", DbType = "sqlserver", ConnectionString = "x", Enabled = true });
		await db.SaveChangesAsync();
	}

	[Fact]
	public async Task UserAndRoleGrants_AreTenantScoped_AndRevocationIsImmediate()
	{
		await using var db = CreateContext(out var connection);
		await using var _ = connection;
		await SeedAsync(db);
		var service = new DataSourceAuthorizationService(db);

		await service.GrantAsync(1, 100, DataSourceGrantSubjectType.Role, 30);
		Assert.True(await service.IsAuthorizedAsync(1, 10, 100));
		await service.RevokeAsync(1, 100, DataSourceGrantSubjectType.Role, 30);
		Assert.False(await service.IsAuthorizedAsync(1, 10, 100));

		await service.GrantAsync(1, 100, DataSourceGrantSubjectType.User, 10);
		await service.GrantAsync(1, 100, DataSourceGrantSubjectType.User, 10);
		Assert.Single(db.DataSourceAccessGrants);
		Assert.False(await service.IsAuthorizedAsync(2, 20, 100));
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.GrantAsync(1, 200, DataSourceGrantSubjectType.User, 10));
	}

	private sealed class DenyAuthorization : IDataSourceAuthorizationService
	{
		public Task<IReadOnlyList<long>> GetAuthorizedDataSourceIdsAsync(long tenantId, long userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>(Array.Empty<long>());
		public Task<bool> IsAuthorizedAsync(long tenantId, long userId, long dataSourceId, CancellationToken ct = default) => Task.FromResult(false);
		public Task GrantAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task RevokeAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task RevokeBySubjectAsync(long tenantId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task<IReadOnlyList<DataSourceAccessGrant>> DetectOrphanGrantsAsync(long tenantId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DataSourceAccessGrant>>(Array.Empty<DataSourceAccessGrant>());
	}

	[Fact]
	public async Task ConnectionFactory_DeniesForgedExecutionIdentity()
	{
		await using var db = CreateContext(out var connection);
		await using var _ = connection;
		await SeedAsync(db);
		var identity = new DataSourceExecutionIdentityAccessor { Current = new DataSourceExecutionIdentity(1, 10) };
		var factory = new DataSourceConnectionFactory(db, new DenyAuthorization(), identity);

		await Assert.ThrowsAsync<UnauthorizedAccessException>(() => factory.CreateAsync(100));
		await Assert.ThrowsAsync<UnauthorizedAccessException>(() => factory.CreateAsync(200));
	}
}
