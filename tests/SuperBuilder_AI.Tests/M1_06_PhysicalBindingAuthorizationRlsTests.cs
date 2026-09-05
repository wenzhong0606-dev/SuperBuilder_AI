using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.BI.Entity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.BI.Entity;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M1-06：PhysicalBinding / 授权 / RLS 字段与数据完整性测试。
/// </summary>
public class M1_06_PhysicalBindingAuthorizationRlsTests
{
	#region 受控词表（纯单元）

	[Fact]
	public void RlsVocabularyValidator_识别合法运算符()
	{
		Assert.True(RlsVocabularyValidator.IsValidOperator("="));
		Assert.True(RlsVocabularyValidator.IsValidOperator("!="));
		Assert.True(RlsVocabularyValidator.IsValidOperator(">="));
		Assert.True(RlsVocabularyValidator.IsValidOperator("LIKE"));
		Assert.True(RlsVocabularyValidator.IsValidOperator("IN"));
		Assert.True(RlsVocabularyValidator.IsValidOperator("IS NULL"));
		Assert.True(RlsVocabularyValidator.IsValidOperator("IS NOT NULL"));
		// 枚举名别名（大小写不敏感）同样受控
		Assert.True(RlsVocabularyValidator.IsValidOperator("EQUALS"));
		Assert.True(RlsVocabularyValidator.IsValidOperator("contains"));
		Assert.True(RlsVocabularyValidator.IsValidOperator("NOTEQUALS"));
	}

	[Fact]
	public void RlsVocabularyValidator_拒绝非法运算符()
	{
		Assert.False(RlsVocabularyValidator.IsValidOperator(null));
		Assert.False(RlsVocabularyValidator.IsValidOperator(""));
		Assert.False(RlsVocabularyValidator.IsValidOperator("   "));
		Assert.False(RlsVocabularyValidator.IsValidOperator("DROP TABLE"));
		Assert.False(RlsVocabularyValidator.IsValidOperator(" OR 1=1"));
	}

	[Fact]
	public void RlsVocabularyValidator_规范化为SQL符号()
	{
		Assert.Equal("=", RlsVocabularyValidator.NormalizeToSymbol("EQUALS"));
		Assert.Equal("=", RlsVocabularyValidator.NormalizeToSymbol("=="));
		Assert.Equal("!=", RlsVocabularyValidator.NormalizeToSymbol("<>"));
		Assert.Equal("!=", RlsVocabularyValidator.NormalizeToSymbol("NOTEQUALS"));
		Assert.Equal(">", RlsVocabularyValidator.NormalizeToSymbol("GREATERTHAN"));
		Assert.Equal(">=", RlsVocabularyValidator.NormalizeToSymbol("GREATEROREQUAL"));
		Assert.Equal("LIKE", RlsVocabularyValidator.NormalizeToSymbol("CONTAINS"));
		Assert.Equal("IS NOT NULL", RlsVocabularyValidator.NormalizeToSymbol("IS NOT NULL"));
	}

	[Fact]
	public void RlsVocabularyValidator_非法运算符规范化抛异常()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => RlsVocabularyValidator.NormalizeToSymbol("BOGUS"));
	}

	#endregion

	#region 数据库上下文与种子

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var ctx = new SuperBIContext(new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options);
		ctx.Database.EnsureCreated();

		ctx.Tenants.Add(new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = true });
		ctx.DataSources.Add(new DataSource
		{
			Id = 1,
			TenantId = 1,
			Name = "ds",
			NormalizedName = "ds",
			DbType = "MYSQL"
		});
		ctx.MetadataTables.Add(new MetadataTable { Id = 1, TenantId = 1, DataSourceId = 1, TableName = "t" });
		ctx.MetadataColumns.Add(new MetadataColumn { Id = 1, MetadataTableId = 1, ColumnName = "c" });
		ctx.Users.Add(new User { Id = 10, TenantId = 1, Username = "u10" });
		ctx.Users.Add(new User { Id = 20, TenantId = 1, Username = "u20" });
		ctx.Roles.Add(new Role { Id = 30, TenantId = 1, Code = "analyst", Name = "Analyst" });
		ctx.BusinessDomains.Add(new BusinessDomain { Id = 1, TenantId = 1, Name = "销售域" });
		ctx.SaveChanges();
		return ctx;
	}

	#endregion

	#region DataSourceAccessGrant 授权治理

	[Fact]
	public async Task RevokeBySubject_移除该主体跨数据源的全部授权()
	{
		await using var db = CreateContext(out var connection);
		await using var _ = connection;

		var svc = new DataSourceAuthorizationService(db);
		await svc.GrantAsync(1, 1, DataSourceGrantSubjectType.User, 10);
		// 第二数据源（复用同一连接需先建表/数据源行）
		db.DataSources.Add(new DataSource { Id = 2, TenantId = 1, Name = "ds2", NormalizedName = "ds2", DbType = "MYSQL" });
		await db.SaveChangesAsync();
		await svc.GrantAsync(1, 2, DataSourceGrantSubjectType.User, 10);

		Assert.True(await svc.IsAuthorizedAsync(1, 10, 1));
		Assert.True(await svc.IsAuthorizedAsync(1, 10, 2));

		await svc.RevokeBySubjectAsync(1, DataSourceGrantSubjectType.User, 10);

		Assert.False(await svc.IsAuthorizedAsync(1, 10, 1));
		Assert.False(await svc.IsAuthorizedAsync(1, 10, 2));
		Assert.Empty(db.DataSourceAccessGrants.Where(g => g.SubjectType == DataSourceGrantSubjectType.User && g.SubjectId == 10));
	}

	[Fact]
	public async Task RevokeBySubject_只影响目标主体()
	{
		await using var db = CreateContext(out var connection);
		await using var _ = connection;

		var svc = new DataSourceAuthorizationService(db);
		await svc.GrantAsync(1, 1, DataSourceGrantSubjectType.User, 10);
		await svc.GrantAsync(1, 1, DataSourceGrantSubjectType.User, 20);

		await svc.RevokeBySubjectAsync(1, DataSourceGrantSubjectType.User, 10);

		Assert.False(await svc.IsAuthorizedAsync(1, 10, 1));
		Assert.True(await svc.IsAuthorizedAsync(1, 20, 1));
	}

	[Fact]
	public async Task DetectOrphanGrants_标记指向不存在主体的授权()
	{
		await using var db = CreateContext(out var connection);
		await using var _ = connection;

		var svc = new DataSourceAuthorizationService(db);
		await svc.GrantAsync(1, 1, DataSourceGrantSubjectType.User, 10); // 有效：User 10 存在
		// 直接写入一条指向不存在 User(9999) 的授权（SubjectId 为逻辑引用，无 DB 外键约束）
		db.DataSourceAccessGrants.Add(new DataSourceAccessGrant
		{
			TenantId = 1,
			DataSourceId = 1,
			SubjectType = DataSourceGrantSubjectType.User,
			SubjectId = 9999
		});
		await db.SaveChangesAsync();

		var orphans = await svc.DetectOrphanGrantsAsync(1);

		Assert.Single(orphans);
		Assert.Equal(9999L, orphans[0].SubjectId);
	}

	#endregion

	#region PhysicalBinding 写入路径校验（BusinessEntityService）

	private static (long dsId, long tableId, long columnId) SeedChain(SuperBIContext ctx, long tenantId)
	{
		ctx.Tenants.Add(new Tenant { Id = tenantId });
		var ds = new DataSource { Id = tenantId * 100 + 1, TenantId = tenantId, Name = $"ds{tenantId}", NormalizedName = $"ds{tenantId}", DbType = "MYSQL" };
		var table = new MetadataTable { Id = tenantId * 100 + 2, TenantId = tenantId, DataSourceId = ds.Id, TableName = $"t{tenantId}" };
		var column = new MetadataColumn { Id = tenantId * 100 + 3, MetadataTableId = table.Id, ColumnName = $"c{tenantId}" };
		ctx.DataSources.Add(ds);
		ctx.MetadataTables.Add(table);
		ctx.MetadataColumns.Add(column);
		ctx.SaveChanges();
		return (ds.Id, table.Id, column.Id);
	}

	[Fact]
	public async Task Create_BindingPriority_负值被写入路径拒绝()
	{
		await using var db = CreateContext(out var connection);
		await using var _ = connection;
		var (dsId, tableId, columnId) = SeedChain(db, 5);

		var entity = new BusinessEntity
		{
			TenantId = 5,
			Name = "入库凭证",
			Keys = new System.Collections.Generic.List<BusinessEntityKey>
			{
				new BusinessEntityKey
				{
					Name = "Id",
					PhysicalBindings = new System.Collections.Generic.List<PhysicalBinding>
					{
						new PhysicalBinding
						{
							DataSourceId = dsId,
							MetadataTableId = tableId,
							MetadataColumnId = columnId,
							PhysicalRole = "Key",
							IsActive = true,
							Priority = -1
						}
					}
				}
			}
		};

		await Assert.ThrowsAsync<InvalidOperationException>(() => new BusinessEntityService(db).CreateAsync(entity));
	}

	[Fact]
	public async Task Create_BindingPriority_零或正值通过()
	{
		await using var db = CreateContext(out var connection);
		await using var _ = connection;
		var (dsId, tableId, columnId) = SeedChain(db, 6);

		var entity = new BusinessEntity
		{
			TenantId = 6,
			Name = "出库凭证",
			Keys = new System.Collections.Generic.List<BusinessEntityKey>
			{
				new BusinessEntityKey
				{
					Name = "Id",
					PhysicalBindings = new System.Collections.Generic.List<PhysicalBinding>
					{
						new PhysicalBinding
						{
							DataSourceId = dsId,
							MetadataTableId = tableId,
							MetadataColumnId = columnId,
							PhysicalRole = "Key",
							IsActive = true,
							Priority = 5
						}
					}
				}
			}
		};

		var saved = await new BusinessEntityService(db).CreateAsync(entity);
		Assert.Equal(5, saved.Keys.First().PhysicalBindings.First().Priority);
	}

	#endregion

	#region 数据库 CHECK 约束（SQLite :memory:）

	[Fact]
	public void RowLevelSecurityPolicy_非法Operator_被CK_RlsPolicies_Operator拒绝()
	{
		using var ctx = CreateContext(out var connection);
		using var _ = connection;

		ctx.RowLevelSecurityPolicies.Add(new RowLevelSecurityPolicy
		{
			TenantId = 1,
			DataSourceId = 1,
			MetadataTableId = 1,
			MetadataColumnId = 1,
			SubjectType = RowPolicySubjectType.User,
			SubjectId = 10,
			Effect = RowPolicyEffect.Allow,
			Operator = "BOGUS",
			Value = "x"
		});
		Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
	}

	[Fact]
	public void RowLevelSecurityPolicy_合法Operator_通过()
	{
		using var ctx = CreateContext(out var connection);
		using var _ = connection;

		ctx.RowLevelSecurityPolicies.Add(new RowLevelSecurityPolicy
		{
			TenantId = 1,
			DataSourceId = 1,
			MetadataTableId = 1,
			MetadataColumnId = 1,
			SubjectType = RowPolicySubjectType.User,
			SubjectId = 10,
			Effect = RowPolicyEffect.Allow,
			Operator = "=",
			Value = "x"
		});
		ctx.SaveChanges();
		Assert.Single(ctx.RowLevelSecurityPolicies);
	}

	[Fact]
	public void RowLevelSecurityPolicy_User类型缺少SubjectId_被CK_RlsPolicies_SubjectConsistency拒绝()
	{
		using var ctx = CreateContext(out var connection);
		using var _ = connection;

		ctx.RowLevelSecurityPolicies.Add(new RowLevelSecurityPolicy
		{
			TenantId = 1,
			DataSourceId = 1,
			MetadataTableId = 1,
			MetadataColumnId = 1,
			SubjectType = RowPolicySubjectType.User, // 需要 SubjectId
			SubjectId = null,
			SubjectKey = null,
			Effect = RowPolicyEffect.Allow,
			Operator = "=",
			Value = "x"
		});
		Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
	}

	[Fact]
	public void PhysicalBinding_负Priority_被CK_PhysicalBindings_PriorityNonNeg拒绝()
	{
		using var ctx = CreateContext(out var connection);
		using var _ = connection;

		ctx.PhysicalBindings.Add(new PhysicalBinding
		{
			DataSourceId = 1,
			MetadataTableId = 1,
			MetadataColumnId = 1,
			Priority = -1
		});
		Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
	}

	[Fact]
	public void PhysicalBinding_零Priority_通过()
	{
		using var ctx = CreateContext(out var connection);
		using var _ = connection;

		ctx.PhysicalBindings.Add(new PhysicalBinding
		{
			DataSourceId = 1,
			MetadataTableId = 1,
			MetadataColumnId = 1,
			Priority = 0
		});
		ctx.SaveChanges();
		Assert.Equal(0, ctx.PhysicalBindings.Single().Priority);
	}

	#endregion

	#region MetadataSemantic.BusinessDomainId 收敛

	[Fact]
	public void MetadataSemantic_BusinessDomainId_FK可持久化且兼容旧字符串()
	{
		using var ctx = CreateContext(out var connection);
		using var _ = connection;

		ctx.MetadataSemantics.Add(new MetadataSemantic
		{
			MetadataColumnId = 1,
			BusinessDomainId = 1,        // 权威外键
			BusinessDomain = "legacy-string" // 保留作展示兼容
		});
		ctx.SaveChanges();

		var sem = ctx.MetadataSemantics.Single();
		Assert.True(sem.BusinessDomainId == 1);
		Assert.Equal("legacy-string", sem.BusinessDomain);
		// FK 收敛：通过显式 Include 验证导航可达，确认 BusinessDomainId 真正指向业务域行。
		var withNav = ctx.MetadataSemantics.Include(s => s.BusinessDomainRef).Single();
		Assert.Equal(1L, withNav.BusinessDomainRef!.Id);
	}

	[Fact]
	public void BusinessDomain_可持久化并回读()
	{
		using var ctx = CreateContext(out var connection);
		using var _ = connection;

		ctx.BusinessDomains.Add(new BusinessDomain { TenantId = 1, Name = "采购域" });
		ctx.SaveChanges();
		Assert.Contains(ctx.BusinessDomains, d => d.Name == "采购域");
	}

	#endregion
}
