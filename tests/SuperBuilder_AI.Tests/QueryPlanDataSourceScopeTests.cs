using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Services.BI;
using SuperBuilder_AI.Models.Metadata;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M5-04：<see cref="QueryPlanDataSourceScope"/> 的纯逻辑单元测试。
///
/// 覆盖从 QueryPlanBuilder 抽出的"选表前数据源候选收敛"全部分支：
///   - 双参数为 null/空 → 原样返回（Golden / 默认推断路径不变）；
///   - authorizedDataSourceIds 过滤生效；
///   - 授权集合排除全部候选 → 抛 SuperBuilderException(DataSourceForbidden, 403)；
///   - requestedDataSourceId 收敛生效；
///   - 收敛后无匹配表 → 抛 InvalidOperationException（明确报错，不静默回退）。
/// </summary>
public class QueryPlanDataSourceScopeTests
{
	private static MetadataSemanticSearchResult Candidate(long dataSourceId)
		=> new MetadataSemanticSearchResult
		{
			VectorType = "table",
			VectorId = $"tbl-{dataSourceId}-{Guid.NewGuid():N}",
			Table = new MetadataTable { Id = dataSourceId * 100, DataSourceId = dataSourceId },
			Score = 0.9,
		};

	private static List<MetadataSemanticSearchResult> Candidates(params long[] dsIds)
		=> dsIds.Select(Candidate).ToList();

	private static IQueryPlanDataSourceScope Scope()
		=> new QueryPlanDataSourceScope();

	[Fact]
	public async Task Scope_NullParams_ReturnsCandidatesUnchanged()
	{
		var candidates = Candidates(1, 2, 3);
		var result = await Scope().ScopeAsync(candidates, null, null, "q");

		Assert.Equal(3, result.Count);
		Assert.All(result, r => Assert.NotNull(r.Table));
	}

	[Fact]
	public async Task Scope_AuthorizedFiltersToAllowedSet()
	{
		var candidates = Candidates(1, 2, 3);
		var result = await Scope().ScopeAsync(candidates, null, new[] { 2L }, "q");

		Assert.Equal(1, result.Count);
		Assert.Equal(2, result[0].Table!.DataSourceId);
	}

	[Fact]
	public async Task Scope_EmptyAuthorized_DeniesWithForbidden()
	{
		var candidates = Candidates(1);
		var ex = await Assert.ThrowsAsync<SuperBuilderException>(
			() => Scope().ScopeAsync(candidates, null, Array.Empty<long>(), "q"));

		Assert.Equal(ErrorCodes.DataSourceForbidden, ex.ErrorCode);
		Assert.Equal(403, ex.StatusCode);
	}

	[Fact]
	public async Task Scope_RequestedConvergesToDataSource()
	{
		var candidates = Candidates(1, 2, 3);
		var result = await Scope().ScopeAsync(candidates, requestedDataSourceId: 2, null, "q");

		Assert.Equal(1, result.Count);
		Assert.Equal(2, result[0].Table!.DataSourceId);
	}

	[Fact]
	public async Task Scope_RequestedNoMatch_ThrowsClearError()
	{
		var candidates = Candidates(1);
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(
			() => Scope().ScopeAsync(candidates, requestedDataSourceId: 99, null, "本周销售"));

		Assert.Contains("所选数据源", ex.Message);
		Assert.Contains("99", ex.Message);
	}

	[Fact]
	public async Task Scope_AuthorizedThenRequested_BothAppliedInOrder()
	{
		// 候选跨 3 个数据源；先按授权收敛到 {2,3}，再按请求收敛到 2。
		var candidates = Candidates(1, 2, 3);
		var result = await Scope().ScopeAsync(
			candidates,
			requestedDataSourceId: 2,
			authorizedDataSourceIds: new[] { 2L, 3L },
			"q");

		Assert.Equal(1, result.Count);
		Assert.Equal(2, result[0].Table!.DataSourceId);
	}
}
