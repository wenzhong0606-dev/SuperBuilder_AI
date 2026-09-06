using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M5-05 列级安全：策略剔除逻辑 + 管线阶段短路验证。
/// 全部使用假对象（不依赖 LLM / 真实元数据 / 数据库）。
/// </summary>
public class QueryPlanColumnSecurityTests
{
	private sealed class FakeClassifier : IColumnSensitivityClassifier
	{
		private readonly HashSet<long> _restricted;
		public FakeClassifier(params long[] restricted) => _restricted = new HashSet<long>(restricted);
		public Task<bool> IsRestrictedAsync(long columnId, string? columnName, long? tableId, ColumnSecurityContext ctx, CancellationToken ct = default)
			=> Task.FromResult(columnId > 0 && _restricted.Contains(columnId));
	}

	private sealed class FakeResolver : IColumnSecurityContextResolver
	{
		private readonly ColumnSecurityContext _ctx;
		public FakeResolver(ColumnSecurityContext ctx) => _ctx = ctx;
		public Task<ColumnSecurityContext> ResolveAsync(QueryPlan plan, CancellationToken ct = default) => Task.FromResult(_ctx);
	}

	private static QueryPlanValidationContext BuildContext()
	{
		return new QueryPlanValidationContext
		{
			TableColumns = new Dictionary<long, List<MetadataColumn>>
			{
				[10] = new List<MetadataColumn>
				{
					new() { Id = 1, ColumnName = "Phone" },
					new() { Id = 2, ColumnName = "Amount" },
					new() { Id = 3, ColumnName = "Name" }
				}
			}
		};
	}

	[Fact]
	public async Task Policy_RestrictedFieldWithoutAuthorization_IsRemoved()
	{
		var plan = new QueryPlan
		{
			Fields = new List<QueryField>
			{
				new() { MetadataColumnId = 1, ColumnName = "Phone" },
				new() { MetadataColumnId = 3, ColumnName = "Name" }
			}
		};
		var ctx = new ColumnSecurityContext(1, 2);
		var policy = new ColumnSecurityPolicy(new FakeClassifier(1));

		var result = await policy.ApplyAsync(plan, ctx, BuildContext(), CancellationToken.None);

		Assert.True(result.Modified);
		Assert.Single(plan.Fields);
		Assert.Equal(3, plan.Fields[0].MetadataColumnId);
		Assert.Contains(result.Blocked, b => b.Collection == nameof(QueryField) && b.ColumnId == 1);
	}

	[Fact]
	public async Task Policy_RestrictedFieldInWhitelist_IsRetained()
	{
		var plan = new QueryPlan
		{
			Fields = new List<QueryField> { new() { MetadataColumnId = 1, ColumnName = "Phone" } }
		};
		var ctx = new ColumnSecurityContext(1, 2) { AuthorizedColumnIds = { 1 } };
		var policy = new ColumnSecurityPolicy(new FakeClassifier(1));

		var result = await policy.ApplyAsync(plan, ctx, BuildContext(), CancellationToken.None);

		Assert.False(result.Modified);
		Assert.Single(plan.Fields);
		Assert.Empty(result.Blocked);
	}

	[Fact]
	public async Task Policy_RestrictedDimension_WholeDimensionRemoved()
	{
		var plan = new QueryPlan
		{
			Dimensions = new List<QueryDimension>
			{
				new() { MetadataColumnId = 1, ColumnName = "Phone", DimensionKeyColumnId = 2, DimensionLabelColumnId = 3 }
			}
		};
		var ctx = new ColumnSecurityContext(1, 2);
		var policy = new ColumnSecurityPolicy(new FakeClassifier(1));

		var result = await policy.ApplyAsync(plan, ctx, BuildContext(), CancellationToken.None);

		Assert.Empty(plan.Dimensions);
		Assert.Contains(result.Blocked, b => b.Collection == nameof(QueryDimension));
	}

	[Fact]
	public async Task Policy_RestrictedOrder_NonMetric_IsRemoved()
	{
		var plan = new QueryPlan
		{
			Orders = new List<QueryOrder> { new() { MetadataColumnId = 1, Field = "Phone" } }
		};
		var ctx = new ColumnSecurityContext(1, 2);
		var policy = new ColumnSecurityPolicy(new FakeClassifier(1));

		var result = await policy.ApplyAsync(plan, ctx, BuildContext(), CancellationToken.None);

		Assert.Empty(plan.Orders);
		Assert.Contains(result.Blocked, b => b.Collection == nameof(QueryOrder));
	}

	[Fact]
	public async Task Policy_RestrictedJoin_WholeJoinRemoved()
	{
		var plan = new QueryPlan
		{
			Joins = new List<QueryJoin>
			{
				new() { LeftColumnId = 1, LeftColumnName = "Phone", RightColumnId = 2, RightColumnName = "Amount" }
			}
		};
		var ctx = new ColumnSecurityContext(1, 2);
		var policy = new ColumnSecurityPolicy(new FakeClassifier(1));

		var result = await policy.ApplyAsync(plan, ctx, BuildContext(), CancellationToken.None);

		Assert.Empty(plan.Joins);
		Assert.Contains(result.Blocked, b => b.Collection == nameof(QueryJoin));
	}

	[Fact]
	public async Task Policy_RestrictedFilter_ByNameLookup_IsRemoved()
	{
		var plan = new QueryPlan
		{
			Filters = new List<QueryFilter> { new() { Field = "Phone" } }
		};
		var ctx = new ColumnSecurityContext(1, 2);
		var policy = new ColumnSecurityPolicy(new FakeClassifier(1));

		var result = await policy.ApplyAsync(plan, ctx, BuildContext(), CancellationToken.None);

		Assert.Empty(plan.Filters);
		Assert.Contains(result.Blocked, b => b.Collection == nameof(QueryFilter) && b.ColumnName == "Phone");
	}

	[Fact]
	public async Task Policy_RestrictedMetric_ByNameLookup_IsRemoved()
	{
		var plan = new QueryPlan
		{
			Metrics = new List<QueryMetric> { new() { Field = "Amount" } }
		};
		var ctx = new ColumnSecurityContext(1, 2);
		var policy = new ColumnSecurityPolicy(new FakeClassifier(2));

		var result = await policy.ApplyAsync(plan, ctx, BuildContext(), CancellationToken.None);

		Assert.Empty(plan.Metrics);
		Assert.Contains(result.Blocked, b => b.Collection == nameof(QueryMetric));
	}

	[Fact]
	public async Task Policy_NoRestrictedColumns_PlanUnchanged()
	{
		var plan = new QueryPlan
		{
			Fields = new List<QueryField> { new() { MetadataColumnId = 3, ColumnName = "Name" } }
		};
		var ctx = new ColumnSecurityContext(1, 2);
		var policy = new ColumnSecurityPolicy(new FakeClassifier(1));

		var result = await policy.ApplyAsync(plan, ctx, BuildContext(), CancellationToken.None);

		Assert.False(result.Modified);
		Assert.Single(plan.Fields);
	}

	[Fact]
	public async Task Policy_AllProjectionsBlocked_RequiresRejection()
	{
		var plan = new QueryPlan
		{
			Fields = new List<QueryField> { new() { MetadataColumnId = 1, ColumnName = "Phone" } }
		};
		var ctx = new ColumnSecurityContext(1, 2);
		var policy = new ColumnSecurityPolicy(new FakeClassifier(1));

		var result = await policy.ApplyAsync(plan, ctx, BuildContext(), CancellationToken.None);

		Assert.True(result.RequiresRejection);
	}

	[Fact]
	public async Task Stage_PlanEmptyAfterBlock_SetsEarlyResponse()
	{
		var plan = new QueryPlan
		{
			Fields = new List<QueryField> { new() { MetadataColumnId = 1, ColumnName = "Phone" } }
		};
		var ctx = new QueryPlanPipelineContext("q", new QueryIntent(), null, null)
		{
			Plan = plan,
			ValidationContext = BuildContext()
		};
		var resolver = new FakeResolver(new ColumnSecurityContext(1, 2));
		var stage = new QueryPlanColumnSecurityStage(resolver, new ColumnSecurityPolicy(new FakeClassifier(1)));

		await stage.ExecuteAsync(ctx, CancellationToken.None);

		Assert.NotNull(ctx.EarlyResponse);
		Assert.False(ctx.EarlyResponse!.Success);
	}

	[Fact]
	public async Task Stage_PlanUnchanged_DoesNotSetEarlyResponse()
	{
		var plan = new QueryPlan
		{
			Fields = new List<QueryField> { new() { MetadataColumnId = 3, ColumnName = "Name" } }
		};
		var ctx = new QueryPlanPipelineContext("q", new QueryIntent(), null, null)
		{
			Plan = plan,
			ValidationContext = BuildContext()
		};
		var resolver = new FakeResolver(new ColumnSecurityContext(1, 2));
		// classifier 标记为受限的列(1)不在 Plan 中 → 零拦截
		var stage = new QueryPlanColumnSecurityStage(resolver, new ColumnSecurityPolicy(new FakeClassifier(1)));

		await stage.ExecuteAsync(ctx, CancellationToken.None);

		Assert.Null(ctx.EarlyResponse);
		Assert.Single(ctx.Plan!.Fields);
	}
}
