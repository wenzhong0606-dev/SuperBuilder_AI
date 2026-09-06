using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M1 closure gate: the database, rather than only API write paths, owns the
/// tenant and metadata referential-integrity invariants.
/// </summary>
public sealed class M1ClosureIntegrityTests
{
	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var context = new SuperBIContext(options);
		context.Database.EnsureCreated();
		return context;
	}

	[Fact]
	public void Required_Closure_Columns_Are_NonNullable_In_The_Ef_Model()
	{
		using var context = CreateContext(out var connection);
		using var _ = connection;

		Assert.False(context.Model.FindEntityType(typeof(Tenant))!.FindProperty(nameof(Tenant.TenantCode))!.IsNullable);
		Assert.False(context.Model.FindEntityType(typeof(Tenant))!.FindProperty(nameof(Tenant.TenantName))!.IsNullable);
		Assert.False(context.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.SecurityStamp))!.IsNullable);
		Assert.False(context.Model.FindEntityType(typeof(DataSource))!.FindProperty(nameof(DataSource.TenantId))!.IsNullable);
		Assert.False(context.Model.FindEntityType(typeof(DataSource))!.FindProperty(nameof(DataSource.Name))!.IsNullable);
		Assert.False(context.Model.FindEntityType(typeof(DataSource))!.FindProperty(nameof(DataSource.NormalizedName))!.IsNullable);
		Assert.False(context.Model.FindEntityType(typeof(DataSource))!.FindProperty(nameof(DataSource.DbType))!.IsNullable);
		Assert.False(context.Model.FindEntityType(typeof(DataSource))!.FindProperty(nameof(DataSource.ConnectionString))!.IsNullable);
		Assert.False(context.Model.FindEntityType(typeof(MetadataColumn))!.FindProperty(nameof(MetadataColumn.MetadataTableId))!.IsNullable);
	}

	[Fact]
	public async Task User_With_Orphan_Tenant_Is_Rejected_By_Database()
	{
		await using var context = CreateContext(out var connection);
		await using var _ = connection;
		context.Users.Add(new User { TenantId = 999, Username = "orphan-user" });

		await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
	}

	[Fact]
	public async Task DataSource_With_Orphan_Tenant_Is_Rejected_By_Database()
	{
		await using var context = CreateContext(out var connection);
		await using var _ = connection;
		context.DataSources.Add(new DataSource
		{
			TenantId = 999,
			Name = "orphan",
			NormalizedName = "orphan",
			DbType = "MYSQL",
			ConnectionString = "x",
		});

		await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
	}

	[Fact]
	public async Task MetadataTable_And_DataSource_CrossTenant_Combination_Is_Rejected()
	{
		await using var context = CreateContext(out var connection);
		await using var _ = connection;
		context.Tenants.AddRange(
			new Tenant { Id = 1, TenantCode = "t1", TenantName = "Tenant 1" },
			new Tenant { Id = 2, TenantCode = "t2", TenantName = "Tenant 2" });
		context.DataSources.Add(new DataSource
		{
			Id = 10,
			TenantId = 1,
			Name = "main",
			NormalizedName = "main",
			DbType = "MYSQL",
			ConnectionString = "x",
		});
		await context.SaveChangesAsync();

		context.MetadataTables.Add(new MetadataTable
		{
			TenantId = 2,
			DataSourceId = 10,
			TableName = "cross_tenant",
		});

		await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
	}

	[Fact]
	public async Task MetadataColumn_With_Orphan_Table_Is_Rejected_By_Database()
	{
		await using var context = CreateContext(out var connection);
		await using var _ = connection;
		context.MetadataColumns.Add(new MetadataColumn
		{
			MetadataTableId = 999,
			ColumnName = "orphan_column",
		});

		await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
	}
}
