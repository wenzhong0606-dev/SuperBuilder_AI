using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Dashboard;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Organization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P6.3 QueryPlanWidgetDataResolver 单测（手写轻量 fake，不依赖 Moq）。
/// 覆盖：无取数（NoQuery）、Decision Gate 阻断（Blocked）、正常执行（Proceed）。
/// </summary>
public class QueryPlanWidgetDataResolverTests
{
	private sealed class FakeUnderstanding : IQueryUnderstandingService
	{
		public Task<QueryIntent> UnderstandAsync(string question) =>
			Task.FromResult(new QueryIntent { OriginalQuestion = question });

		public Task<QueryIntent> UnderstandAsync(string question, PlatformContext platformContext) =>
			Task.FromResult(new QueryIntent { OriginalQuestion = question });
	}

	private sealed class FakePipeline : IQueryPlanPipeline
	{
		private readonly QueryPlanPipelineResult _result;
		public FakePipeline(QueryPlanPipelineResult result) => _result = result;
		public Task<QueryPlanPipelineResult> RunAsync(string question, QueryIntent intent) =>
			Task.FromResult(_result);
	}

	private sealed class FakeDialectResolver : ISqlDialectResolver
	{
		public ISqlDialect Resolve(string dbType) => null!;
	}

	private sealed class FakeSqlBuilder : ISqlQueryBuilder
	{
		public Task<SqlQuery> BuildAsync(QueryPlan plan, ISqlDialect dialect) =>
			Task.FromResult(new SqlQuery { Sql = "select 1" });
	}

	private sealed class FakeExec : IQueryExecutionService
	{
		public Task<QueryResult> ExecuteAsync(SqlQuery query, long dataSourceId) =>
			Task.FromResult(new QueryResult
			{
				Success = true,
				Rows = { new Dictionary<string, object?> { ["region"] = "East", ["amount"] = 100 } },
			});
	}

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
	public async Task Resolve_NullQuery_ReturnsNoQuery()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var resolver = new SuperBuilder_AI.Services.BI.Dashboard.QueryPlanWidgetDataResolver(
			new FakeUnderstanding(),
			new FakePipeline(new QueryPlanPipelineResult()),
			ctx,
			new FakeDialectResolver(),
			new FakeSqlBuilder(),
			new FakeExec());

		var result = await resolver.ResolveAsync(null, PlatformContext.System, new List<FilterDsl>());

		Assert.Equal("NoQuery", result.Decision);
		Assert.False(result.Resolved);
	}

	[Fact]
	public async Task Resolve_PipelineBlocked_ReturnsBlocked()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var blocked = new QueryPlanPipelineResult
		{
			EarlyResponse = new BIResponse
			{
				Success = false,
				Question = "q",
				ErrorMessage = "blocked by decision gate",
			},
		};
		var resolver = new SuperBuilder_AI.Services.BI.Dashboard.QueryPlanWidgetDataResolver(
			new FakeUnderstanding(),
			new FakePipeline(blocked),
			ctx,
			new FakeDialectResolver(),
			new FakeSqlBuilder(),
			new FakeExec());

		var query = new WidgetQueryDsl { Question = "各区域销售额" };
		var result = await resolver.ResolveAsync(query, PlatformContext.System, new List<FilterDsl>());

		Assert.Equal("Blocked", result.Decision);
		Assert.False(result.Resolved);
		Assert.Equal("blocked by decision gate", result.Error);
	}

	[Fact]
	public async Task Resolve_PipelineProceed_ExecutesAndReturnsRows()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.DataSources.Add(new SuperBuilder_AI.Models.Metadata.DataSource
		{
			Id = 1,
			DbType = "sqlserver",
			ConnectionString = "x",
		});
		await ctx.SaveChangesAsync();

		var proceed = new QueryPlanPipelineResult { Plan = new QueryPlan { DataSourceId = 1 } };
		var resolver = new SuperBuilder_AI.Services.BI.Dashboard.QueryPlanWidgetDataResolver(
			new FakeUnderstanding(),
			new FakePipeline(proceed),
			ctx,
			new FakeDialectResolver(),
			new FakeSqlBuilder(),
			new FakeExec());

		var query = new WidgetQueryDsl { Question = "各区域销售额" };
		var result = await resolver.ResolveAsync(query, PlatformContext.System, new List<FilterDsl>());

		Assert.Equal("Proceed", result.Decision);
		Assert.True(result.Resolved);
		Assert.Equal(1, result.Rows.Count);
		Assert.Contains("region", result.Columns);
	}
}
