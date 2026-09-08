using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Services.BI;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>M6-04 Cache 完整版本：7 维版本上下文与提供器测试（无 LLM / 可离线）。</summary>
public class AskCacheVersionProviderTests
{
	// ---------- AskCacheVersionContext ----------

	[Fact]
	public void BuildCacheKey_IsStableForIdenticalContexts()
	{
		var a = new AskCacheVersionContext("p", "pol", "en-US", "m", "s", "mt", "ds");
		var b = new AskCacheVersionContext("p", "pol", "en-US", "m", "s", "mt", "ds");
		Assert.Equal(a.BuildCacheKey("q"), b.BuildCacheKey("q"));
	}

	[Fact]
	public void BuildCacheKey_ChangesWhenAnyDimensionChanges()
	{
		static string Key(string perm, string pol, string culture, string model, string sem, string meta, string ds)
			=> new AskCacheVersionContext(perm, pol, culture, model, sem, meta, ds).BuildCacheKey("q");

		var baseline = Key("p", "pol", "en-US", "m", "s", "mt", "ds");
		Assert.NotEqual(baseline, Key("px", "pol", "en-US", "m", "s", "mt", "ds")); // 权限
		Assert.NotEqual(baseline, Key("p", "polx", "en-US", "m", "s", "mt", "ds")); // 策略
		Assert.NotEqual(baseline, Key("p", "pol", "zh-CN", "m", "s", "mt", "ds")); // 语言
		Assert.NotEqual(baseline, Key("p", "pol", "en-US", "mx", "s", "mt", "ds")); // 模型
		Assert.NotEqual(baseline, Key("p", "pol", "en-US", "m", "sx", "mt", "ds")); // 语义
		Assert.NotEqual(baseline, Key("p", "pol", "en-US", "m", "s", "mtx", "ds")); // 元数据
		Assert.NotEqual(baseline, Key("p", "pol", "en-US", "m", "s", "mt", "dsx")); // 数据源集合
	}

	[Fact]
	public void Legacy_BackfillsNaForNewDimensions()
	{
		var ctx = AskCacheVersionContext.Legacy("perm1", "pol1", "zh-CN");
		Assert.Equal("perm1", ctx.PermissionFingerprint);
		Assert.Equal("pol1", ctx.RlsPolicyFingerprint);
		Assert.Equal("zh-CN", ctx.Culture);
		Assert.Equal("na", ctx.ModelVersion);
		Assert.Equal("na", ctx.SemanticVersion);
		Assert.Equal("na", ctx.MetadataVersion);
		Assert.Equal("na", ctx.DataSourceVersion);
	}

	// ---------- CompositeAskCacheVersionProvider ----------

	private sealed class FakeRowSecurity : IRowLevelSecurityService
	{
		public string Fingerprint { get; set; } = "pf1";
		public Task<string> GetPolicyFingerprintAsync(long tenantId, long userId, CancellationToken ct = default)
			=> Task.FromResult(Fingerprint);
		public Task ApplyAsync(QueryPlan plan, long tenantId, long userId, CancellationToken ct = default)
			=> Task.CompletedTask;
	}

	private sealed class FixedVersion : IMetadataVersionProvider, ISemanticVersionProvider, IDataSourceCatalogVersionProvider
	{
		private readonly string _v;
		public FixedVersion(string v) => _v = v;
		public Task<string> ResolveAsync(long tenantId, CancellationToken ct = default) => Task.FromResult(_v);
	}

	[Fact]
	public async Task Composite_ResolvesAllDimensions_WithMocks()
	{
		var provider = new CompositeAskCacheVersionProvider(
			rowSecurity: new FakeRowSecurity { Fingerprint = "pf1" },
			qwen: Options.Create(new QwenOptions { Model = "qwen-max" }),
			metadata: new FixedVersion("metaV"),
			semantic: new FixedVersion("semV"),
			dataSource: new FixedVersion("dsV"));

		var ctx = await provider.ResolveAsync(7, 5, new[] { 1L, 2L }, "permX");

		Assert.Equal("permX", ctx.PermissionFingerprint);
		Assert.Equal("pf1", ctx.RlsPolicyFingerprint);
		Assert.Equal("qwen-max", ctx.ModelVersion);
		Assert.Equal("semV", ctx.SemanticVersion);
		Assert.Equal("metaV", ctx.MetadataVersion);
		Assert.Equal("dsV", ctx.DataSourceVersion);
		Assert.Equal(CultureInfo.CurrentUICulture.Name, ctx.Culture);
	}

	[Fact]
	public async Task Composite_NullRowSecurity_FallsBackToLegacy()
	{
		var provider = new CompositeAskCacheVersionProvider(
			qwen: Options.Create(new QwenOptions { Model = "qwen-plus" }),
			metadata: new FixedVersion("m"), semantic: new FixedVersion("s"), dataSource: new FixedVersion("d"));
		var ctx = await provider.ResolveAsync(7, 5, Array.Empty<long>(), "permX");
		Assert.Equal("legacy", ctx.RlsPolicyFingerprint);
		Assert.Equal("permX", ctx.PermissionFingerprint);
	}

	[Fact]
	public async Task Composite_NullSubProviders_FallBackToNa()
	{
		var provider = new CompositeAskCacheVersionProvider(rowSecurity: new FakeRowSecurity());
		var ctx = await provider.ResolveAsync(7, 5, Array.Empty<long>(), "permX");
		Assert.Equal("na", ctx.ModelVersion);
		Assert.Equal("na", ctx.SemanticVersion);
		Assert.Equal("na", ctx.MetadataVersion);
		Assert.Equal("na", ctx.DataSourceVersion);
	}

	[Fact]
	public async Task Composite_NullQwen_FallsBackToNaModel()
	{
		var provider = new CompositeAskCacheVersionProvider(
			rowSecurity: new FakeRowSecurity(),
			metadata: new FixedVersion("m"), semantic: new FixedVersion("s"), dataSource: new FixedVersion("d"));
		var ctx = await provider.ResolveAsync(7, 5, Array.Empty<long>(), "permX");
		Assert.Equal("na", ctx.ModelVersion);
	}

	// ---------- 子提供器（SQLite 验证 EF 查询真实可执行） ----------

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	[Fact]
	public async Task SubProviders_Sqlite_ResolveAndReactToChanges()
	{
		await using var ctx = CreateContext(out var conn);
		ctx.Tenants.Add(new Tenant { Id = 7, TenantCode = "tenant-7", TenantName = "Tenant 7" });
		ctx.Tenants.Add(new Tenant { Id = 8, TenantCode = "tenant-8", TenantName = "Tenant 8" });
		await ctx.SaveChangesAsync();

		var ds = new DataSource { TenantId = 7, Name = "DS7", NormalizedName = "ds7", DbType = "MYSQL", Enabled = true };
		var tbl = new MetadataTable { TenantId = 7, DataSource = ds, TableName = "t" };
		var col = new MetadataColumn { MetadataTable = tbl, ColumnName = "c" };
		var sem = new SemanticLabel { TenantId = 7, ConceptType = "MetadataSemantic", ConceptId = 1, Culture = "zh-CN", LabelKind = "DisplayName", Value = "销售" };
		ctx.DataSources.Add(ds);
		ctx.MetadataTables.Add(tbl);
		ctx.MetadataColumns.Add(col);
		ctx.SemanticLabels.Add(sem);
		await ctx.SaveChangesAsync();

		var meta = new MetadataVersionProvider(ctx);
		var semP = new SemanticVersionProvider(ctx);
		var dsP = new DataSourceCatalogVersionProvider(ctx);

		// 初始：table(1)+column(1)=2；sem(1)；ds 目录指纹 = "{Id}:1:1;"（每行 "Id:Enabled:RowVersion;" 拼接）
		Assert.Equal("2", await meta.ResolveAsync(7));
		Assert.Equal("1", await semP.ResolveAsync(7));
		Assert.Equal($"{ds.Id}:1:1;", await dsP.ResolveAsync(7));

		// 改动数据源（RowVersion 自增）→ 目录指纹变化
		ds.Enabled = false;
		await ctx.SaveChangesAsync();
		Assert.Equal($"{ds.Id}:0:2;", await dsP.ResolveAsync(7));

		// 租户隔离：租户 8 独立，不影响租户 7
		var ds8 = new DataSource { TenantId = 8, Name = "DS8", NormalizedName = "ds8", DbType = "MYSQL", Enabled = true };
		var tbl8 = new MetadataTable { TenantId = 8, DataSource = ds8, TableName = "t8" };
		ctx.DataSources.Add(ds8);
		ctx.MetadataTables.Add(tbl8);
		await ctx.SaveChangesAsync();
		Assert.Equal($"{ds8.Id}:1:1;", await dsP.ResolveAsync(8));
		Assert.Equal($"{ds.Id}:0:2;", await dsP.ResolveAsync(7));
	}

	[Fact]
	public async Task SemanticVersion_IncludesGlobalLabels()
	{
		await using var ctx = CreateContext(out var conn);
		ctx.SemanticLabels.Add(new SemanticLabel { TenantId = 0, ConceptType = "MetadataSemantic", ConceptId = 99, Culture = "zh-CN", LabelKind = "DisplayName", Value = "全局" });
		ctx.SemanticLabels.Add(new SemanticLabel { TenantId = 7, ConceptType = "MetadataSemantic", ConceptId = 1, Culture = "zh-CN", LabelKind = "DisplayName", Value = "租户" });
		await ctx.SaveChangesAsync();

		// 租户 7 的语义版本 = 全局(1) + 自身(1) = "2"
		Assert.Equal("2", await new SemanticVersionProvider(ctx).ResolveAsync(7));
	}
}
