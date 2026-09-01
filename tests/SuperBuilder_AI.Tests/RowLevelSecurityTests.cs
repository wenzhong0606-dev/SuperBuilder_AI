using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.BI;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class RowLevelSecurityTests
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
		db.Tenants.Add(new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = true });
		db.Users.AddRange(new User { Id = 10, TenantId = 1, Username = "u1" }, new User { Id = 11, TenantId = 1, Username = "u2" });
		db.Roles.Add(new Role { Id = 20, TenantId = 1, Code = "east", Name = "East" });
		db.UserRoles.Add(new UserRole { Id = 30, TenantId = 1, UserId = 10, RoleId = 20 });
		db.DataSources.Add(new DataSource { Id = 100, TenantId = 1, Name = "ds", Enabled = true, DbType = "sqlserver", ConnectionString = "x" });
		db.MetadataTables.Add(new MetadataTable
		{
			Id = 200, TenantId = 1, DataSourceId = 100, TableName = "sales",
			Columns = { new MetadataColumn { Id = 300, ColumnName = "region", DataType = "varchar" } }
		});
		await db.SaveChangesAsync();
	}

	private static QueryPlan Plan() => new()
	{
		DataSourceId = 100,
		Tables = { new QueryTable { MetadataTableId = 200, DataSourceId = 100, TableName = "sales" } },
		Fields = { new QueryField { MetadataColumnId = 300, ColumnName = "region", DataType = "varchar" } }
	};

	[Fact]
	public async Task GovernedTable_RequiresApplicableAllow_AndAddsDeny()
	{
		await using var db = CreateContext(out var connection);
		await using var _ = connection;
		await SeedAsync(db);
		db.RowLevelSecurityPolicies.AddRange(
			new RowLevelSecurityPolicy { Id = 1, TenantId = 1, DataSourceId = 100, MetadataTableId = 200, MetadataColumnId = 300, SubjectType = RowPolicySubjectType.Role, SubjectId = 20, Effect = RowPolicyEffect.Allow, Value = "east" },
			new RowLevelSecurityPolicy { Id = 2, TenantId = 1, DataSourceId = 100, MetadataTableId = 200, MetadataColumnId = 300, SubjectType = RowPolicySubjectType.Everyone, Effect = RowPolicyEffect.Deny, Value = "blocked" });
		await db.SaveChangesAsync();
		var identity = new DataSourceExecutionIdentityAccessor { Current = new DataSourceExecutionIdentity(1, 10) };
		var service = new RowLevelSecurityService(db, identity);
		var plan = Plan();

		await service.ApplyAsync(plan, 1, 10);
		Assert.Equal(2, plan.MandatoryRowFilters.Count);
		Assert.Contains(plan.MandatoryRowFilters, x => !x.Deny && x.Value == "east");
		Assert.Contains(plan.MandatoryRowFilters, x => x.Deny && x.Value == "blocked");

		var denied = Plan();
		identity.Current = new DataSourceExecutionIdentity(1, 11);
		var ex = await Assert.ThrowsAsync<SuperBuilderException>(() => service.ApplyAsync(denied, 1, 11));
		Assert.Equal(ErrorCodes.RowPolicyForbidden, ex.ErrorCode);
	}

	[Fact]
	public async Task AttributePolicy_MatchesExecutionAttribute_AndFingerprintChangesWithPolicy()
	{
		await using var db = CreateContext(out var connection);
		await using var _ = connection;
		await SeedAsync(db);
		var policy = new RowLevelSecurityPolicy { Id = 3, TenantId = 1, DataSourceId = 100, MetadataTableId = 200, MetadataColumnId = 300, SubjectType = RowPolicySubjectType.Attribute, SubjectKey = "region", SubjectValue = "east", Effect = RowPolicyEffect.Allow, Value = "east" };
		db.RowLevelSecurityPolicies.Add(policy);
		await db.SaveChangesAsync();
		var identity = new DataSourceExecutionIdentityAccessor { Current = new DataSourceExecutionIdentity(1, 10, new System.Collections.Generic.Dictionary<string, string> { ["region"] = "east" }) };
		var service = new RowLevelSecurityService(db, identity);

		var before = await service.GetPolicyFingerprintAsync(1, 10);
		await service.ApplyAsync(Plan(), 1, 10);
		policy.Version++;
		policy.UpdatedTime = policy.UpdatedTime.AddSeconds(1);
		await db.SaveChangesAsync();
		var after = await service.GetPolicyFingerprintAsync(1, 10);
		Assert.NotEqual(before, after);
	}

	[Fact]
	public async Task SqlBuilder_AlwaysAndsMandatoryFilters_AndParameterizesValues()
	{
		var plan = Plan();
		plan.Filters.Add(new QueryFilter { Field = "region", Operator = "=", Value = "west" });
		plan.MandatoryRowFilters.AddRange(new[]
		{
			new MandatoryRowFilter { PolicyId = 1, MetadataTableId = 200, MetadataColumnId = 300, TableName = "sales", Field = "region", Operator = "=", Value = "east' OR 1=1--" },
			new MandatoryRowFilter { PolicyId = 3, MetadataTableId = 200, MetadataColumnId = 300, TableName = "sales", Field = "region", Operator = "=", Value = "north" },
			new MandatoryRowFilter { PolicyId = 2, MetadataTableId = 200, MetadataColumnId = 300, TableName = "sales", Field = "region", Operator = "=", Value = "blocked", Deny = true }
		});

		var query = await new SqlQueryBuilder().BuildAsync(plan, new SqlServerDialect());
		Assert.Contains("[region] = @p0 AND ([sales].[region] = @p100000 OR [sales].[region] = @p100001) AND NOT ([sales].[region] = @p100002)", query.Sql);
		Assert.DoesNotContain("OR 1=1", query.Sql);
		Assert.Equal("east' OR 1=1--", query.Parameters["@p100000"]);
	}

	[Fact]
	public async Task SqlBuilder_QualifiesRlsAcrossJoinAndAggregate()
	{
		var plan = Plan();
		plan.Fields[0].Aggregation = "COUNT";
		plan.Tables.Add(new QueryTable { MetadataTableId = 201, DataSourceId = 100, TableName = "customers" });
		plan.Joins.Add(new QueryJoin { LeftTableId = 200, LeftColumnId = 301, LeftTableName = "sales", LeftColumnName = "customer_id", RightTableId = 201, RightColumnId = 401, RightTableName = "customers", RightColumnName = "id" });
		plan.MandatoryRowFilters.AddRange(new[]
		{
			new MandatoryRowFilter { PolicyId = 1, MetadataTableId = 200, TableName = "sales", Field = "region", Value = "east" },
			new MandatoryRowFilter { PolicyId = 2, MetadataTableId = 201, TableName = "customers", Field = "segment", Value = "enterprise" }
		});

		var query = await new SqlQueryBuilder().BuildAsync(plan, new SqlServerDialect());
		Assert.Contains("COUNT([region])", query.Sql);
		Assert.Contains("INNER JOIN [customers]", query.Sql);
		Assert.Contains("([sales].[region] = @p100000) AND ([customers].[segment] = @p100001)", query.Sql);
	}
}
