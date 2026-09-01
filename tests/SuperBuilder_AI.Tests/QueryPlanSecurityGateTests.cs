using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.BI;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class QueryPlanSecurityGateTests
{
	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var db = new SuperBIContext(new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options);
		db.Database.EnsureCreated();
		return db;
	}

	private static async Task SeedAsync(SuperBIContext db)
	{
		db.Tenants.AddRange(
			new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = true },
			new Tenant { Id = 2, TenantCode = "t2", TenantName = "T2", Enabled = true });
		db.Users.Add(new User { Id = 10, TenantId = 1, Username = "u1" });
		db.DataSources.AddRange(
			new DataSource { Id = 100, TenantId = 1, Name = "main", Enabled = true, DbType = "sqlserver", ConnectionString = "x" },
			new DataSource { Id = 101, TenantId = 2, Name = "foreign", Enabled = true, DbType = "sqlserver", ConnectionString = "x" });
		db.MetadataTables.AddRange(
			new MetadataTable { Id = 200, TenantId = 1, DataSourceId = 100, TableName = "sales", Columns = {
				new MetadataColumn { Id = 300, ColumnName = "region", DataType = "varchar" },
				new MetadataColumn { Id = 301, ColumnName = "customer_id", DataType = "bigint" } } },
			new MetadataTable { Id = 201, TenantId = 1, DataSourceId = 100, TableName = "customers", Columns = {
				new MetadataColumn { Id = 400, ColumnName = "id", DataType = "bigint" },
				new MetadataColumn { Id = 401, ColumnName = "name", DataType = "varchar" } } });
		db.RowLevelSecurityPolicies.Add(new RowLevelSecurityPolicy
		{
			Id = 500, TenantId = 1, DataSourceId = 100, MetadataTableId = 200, MetadataColumnId = 300,
			SubjectType = RowPolicySubjectType.Everyone, Effect = RowPolicyEffect.Allow, Value = "east"
		});
		await db.SaveChangesAsync();
		await new DataSourceAuthorizationService(db).GrantAsync(1, 100, DataSourceGrantSubjectType.User, 10);
	}

	private static QueryPlan Plan() => new()
	{
		EffectiveTenantId = 1,
		DataSourceId = 100,
		Tables =
		{
			new QueryTable { MetadataTableId = 200, DataSourceId = 100, TableName = "sales" },
			new QueryTable { MetadataTableId = 201, DataSourceId = 100, TableName = "customers" }
		},
		Fields = { new QueryField { MetadataColumnId = 300, ColumnName = "region" } },
		Metrics = { new QueryMetric { Name = "customers", Field = "customer_id", Aggregation = "COUNT" } },
		Dimensions = { new QueryDimension { MetadataColumnId = 401, ColumnName = "name" } },
		Filters = { new QueryFilter { Field = "region", Value = "east" } },
		Orders = { new QueryOrder { MetadataColumnId = 401, Field = "name" } },
		Joins = { new QueryJoin { LeftTableId = 200, LeftColumnId = 301, LeftTableName = "sales", LeftColumnName = "customer_id", RightTableId = 201, RightColumnId = 400, RightTableName = "customers", RightColumnName = "id" } }
	};

	private static (RowLevelSecurityService Rls, QueryPlanSecurityGate Gate) Services(SuperBIContext db)
	{
		var identity = new DataSourceExecutionIdentityAccessor { Current = new DataSourceExecutionIdentity(1, 10) };
		var rls = new RowLevelSecurityService(db, identity);
		return (rls, new QueryPlanSecurityGate(db, new DataSourceAuthorizationService(db), rls, identity));
	}

	[Fact]
	public async Task ValidCurrentPlan_PassesFinalGate()
	{
		await using var db = CreateContext(out var connection);
		await using var _ = connection;
		await SeedAsync(db);
		var (rls, gate) = Services(db);
		var plan = Plan();
		await rls.ApplyAsync(plan, 1, 10);

		await gate.ValidateAsync(plan, 1, 10);
	}

	[Theory]
	[InlineData("tenant")]
	[InlineData("source")]
	[InlineData("table")]
	[InlineData("field")]
	[InlineData("join")]
	[InlineData("row-filter")]
	[InlineData("fingerprint")]
	public async Task TamperedOrStalePlan_IsRejectedWithStableCode(string mutation)
	{
		await using var db = CreateContext(out var connection);
		await using var _ = connection;
		await SeedAsync(db);
		var (rls, gate) = Services(db);
		var plan = Plan();
		await rls.ApplyAsync(plan, 1, 10);

		switch (mutation)
		{
			case "tenant": plan.EffectiveTenantId = 2; break;
			case "source": plan.DataSourceId = 101; break;
			case "table": plan.Tables[0].TableName = "secret_sales"; break;
			case "field": plan.Fields[0].ColumnName = "password"; break;
			case "join": plan.Joins[0].RightColumnId = 401; break;
			case "row-filter": plan.MandatoryRowFilters[0].Value = "west"; break;
			case "fingerprint": plan.DataPolicyFingerprint = "STALE"; break;
		}

		var ex = await Assert.ThrowsAsync<SuperBuilderException>(() => gate.ValidateAsync(plan, 1, 10));
		Assert.Equal(ErrorCodes.QueryPlanSecurityRejected, ex.ErrorCode);
		Assert.Equal(403, ex.StatusCode);
	}
}
