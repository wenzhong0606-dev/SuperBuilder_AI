using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// Phase 3：自主学习纠错（显式纠正落库 + 下次同问句自动回放）。
/// 覆盖 隔离边界 / 聚合累加 / 锁表句合成 / 问句归一化 / 过短问句拒绝学习。
/// </summary>
public class CorrectionLearningServiceTests
{
	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private const string Question = "最近十条入库凭证";

	[Fact]
	public async Task Capture_ThenResolve_ReplaysForSameUser()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var service = new CorrectionLearningService(ctx);
		await service.CaptureAsync(
			7, 42, 1, Question, CorrectionKind.TableOverride,
			new CorrectionPayload { TableName = "wms_storage_receipt" });

		var resolution = await service.ResolveAsync(7, 42, "帮我查最近十条入库凭证");

		Assert.True(resolution.Any);
		Assert.Equal("wms_storage_receipt", resolution.TableOverride);
	}

	[Fact]
	public async Task Resolve_DoesNotLeakAcrossTenantOrUser()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var service = new CorrectionLearningService(ctx);
		await service.CaptureAsync(
			7, 42, 1, Question, CorrectionKind.TableOverride,
			new CorrectionPayload { TableName = "wms_storage_receipt" });

		Assert.False((await service.ResolveAsync(8, 42, Question)).Any);
		Assert.False((await service.ResolveAsync(7, 43, Question)).Any);
		Assert.False((await service.ResolveAsync(7, null, Question)).Any);
	}

	[Fact]
	public async Task Capture_SameKeyTwice_AggregatesIntoOneRule()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var service = new CorrectionLearningService(ctx);
		var payload = new CorrectionPayload { TableName = "wms_storage_receipt" };

		await service.CaptureAsync(7, 42, 1, Question, CorrectionKind.TableOverride, payload);
		await service.CaptureAsync(7, 42, 1, Question, CorrectionKind.TableOverride, payload);

		var rules = await ctx.QueryCorrectionRules.ToListAsync();
		Assert.Single(rules);
		Assert.Equal(2, rules[0].HitCount);
	}

	[Fact]
	public async Task MarkMatched_IncrementsHitCount()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var service = new CorrectionLearningService(ctx);
		await service.CaptureAsync(
			7, 42, 1, Question, CorrectionKind.TableOverride,
			new CorrectionPayload { TableName = "wms_storage_receipt" });

		var resolution = await service.ResolveAsync(7, 42, Question);
		await service.MarkMatchedAsync(resolution);

		var rule = await ctx.QueryCorrectionRules.SingleAsync();
		Assert.Equal(2, rule.HitCount);
		Assert.NotNull(rule.LastMatchedAt);
	}

	[Fact]
	public async Task Resolve_RestoresValueMapPayload()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var service = new CorrectionLearningService(ctx);
		await service.CaptureAsync(
			7, 42, 1, Question, CorrectionKind.ValueMap,
			new CorrectionPayload
			{
				ColumnName = "type",
				ValueMap = new() { ["1"] = "采购入库", ["2"] = "调拨入库" },
			});

		var resolution = await service.ResolveAsync(7, 42, Question);
		var rule = resolution.ValueMapFor("TYPE");

		Assert.NotNull(rule);
		Assert.Equal("采购入库", rule!.Payload.ValueMap!["1"]);
	}

	[Fact]
	public void ComposeLearnedQuestion_InjectsTableLockPhrase()
	{
		using var ctx = CreateContext(out var connection);
		connection.Dispose();
		var service = new CorrectionLearningService(ctx);

		var resolution = new CorrectionResolution
		{
			Corrections =
			{
				new LearnedCorrection
				{
					Kind = CorrectionKind.TableOverride,
					Payload = new CorrectionPayload { TableName = "wms_storage_receipt" },
				},
			},
		};

		var composed = service.ComposeLearnedQuestion(Question, resolution);
		Assert.Contains("查询物理表必须使用 wms_storage_receipt", composed, StringComparison.Ordinal);

		// 已含锁表句式时不重复拼接（防止多轮回放累积）。
		var again = service.ComposeLearnedQuestion(composed, resolution);
		Assert.Equal(composed, again);
	}

	[Fact]
	public async Task Capture_RejectsTooShortQuestion()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var service = new CorrectionLearningService(ctx);
		await service.CaptureAsync(
			7, 42, 1, "查", CorrectionKind.TableOverride,
			new CorrectionPayload { TableName = "wms_storage_receipt" });

		Assert.Empty(await ctx.QueryCorrectionRules.ToListAsync());
	}

	[Fact]
	public void NormalizeQuestion_StripsWhitespacePunctuationAndCase()
	{
		Assert.Equal("最近十条入库凭证", CorrectionLearningService.NormalizeQuestion("最近十条入库凭证？"));
		Assert.Equal("abc", CorrectionLearningService.NormalizeQuestion(" A, B; C. "));
		Assert.Equal(string.Empty, CorrectionLearningService.NormalizeQuestion("   "));
	}
}
