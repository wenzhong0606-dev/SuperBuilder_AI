using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Platform;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M0-02 补齐契约：GET api/semantic-labels/{id} 单测（真实 SemanticLabelService + SQLite 内存库）。
/// 重点验证数据面租户策略 + 404 语义 + 共享标签(TenantId=0)对任意租户可见（M0-06 关系链一致性）。
/// 覆盖：200 所属租户可见、200 共享标签可见、404 不存在、403 跨租户越权。
/// </summary>
public class SemanticLabelControllerTests
{
	private const long Tenant5 = 5;

	private static ClaimsPrincipal AsTenant(long tid) =>
		new(new ClaimsIdentity(new[] { new Claim("tid", tid.ToString()) }));

	private sealed class FakeLocalization : ILocalizationService
	{
		public LocaleContext Resolve(string? culture) => LocaleContext.Default;
		public IReadOnlyList<string> BuildFallbackChain(LocaleContext? locale) => new[] { "zh-CN" };
		public IReadOnlyList<LocaleContext> SupportedLocales => new[] { LocaleContext.Default };
		public string GetString(string key, LocaleContext? locale = null) => key;
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

	private static SemanticLabelController Build(SuperBIContext db, ClaimsPrincipal user)
	{
		var localization = new FakeLocalization();
		var labels = new SemanticLabelService(db, localization);
		var recall = new SemanticLabelRecallService(db, localization);
		var controller = new SemanticLabelController(labels, recall, localization);
		controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
		return controller;
	}

	[Fact]
	public async Task GetById_Returns_200_WhenOwned()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var label = new SemanticLabel
		{
			TenantId = Tenant5,
			ConceptType = "Entity",
			ConceptId = 1,
			Culture = "zh-CN",
			LabelKind = "DisplayName",
			Value = "销售订单",
		};
		ctx.SemanticLabels.Add(label);
		await ctx.SaveChangesAsync();

		var controller = Build(ctx, AsTenant(Tenant5));
		var result = await controller.GetById(label.Id, Tenant5, CancellationToken.None) as OkObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task GetById_Returns_200_WhenShared_TenantZero()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var label = new SemanticLabel
		{
			TenantId = 0,
			ConceptType = "Entity",
			ConceptId = 2,
			Culture = "zh-CN",
			LabelKind = "DisplayName",
			Value = "共享标签",
		};
		ctx.SemanticLabels.Add(label);
		await ctx.SaveChangesAsync();

		// 租户 5 读取共享标签(TenantId=0) → 可见。
		var controller = Build(ctx, AsTenant(Tenant5));
		var result = await controller.GetById(label.Id, Tenant5, CancellationToken.None) as OkObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task GetById_Returns_404_WhenMissing()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx, AsTenant(Tenant5));
		var result = await controller.GetById(999, Tenant5, CancellationToken.None);
		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task GetById_Returns_403_WhenCrossTenant()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var label = new SemanticLabel
		{
			TenantId = Tenant5,
			ConceptType = "Entity",
			ConceptId = 1,
			Culture = "zh-CN",
			LabelKind = "DisplayName",
			Value = "销售订单",
		};
		ctx.SemanticLabels.Add(label);
		await ctx.SaveChangesAsync();

		// 认证租户 5，却请求租户 7 → 数据面越权。
		var controller = Build(ctx, AsTenant(Tenant5));
		var result = await controller.GetById(label.Id, 7, CancellationToken.None);
		var objectResult = Assert.IsType<ObjectResult>(result);
		Assert.Equal(403, objectResult.StatusCode);
	}
}
