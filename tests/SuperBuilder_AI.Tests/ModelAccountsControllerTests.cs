using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Application.ModelAccounts;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Security;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M7-07 ModelAccountsController 单测（手写 SQLite 内存库，不依赖 Moq）。
/// 覆盖：CRUD 闭环、掩码返回（明文绝不下发）、重复冲突、跨租户 403、
/// 设默认唯一性、删除、GetById 404、租户级列表隔离。
/// </summary>
public class ModelAccountsControllerTests
{
	private const long Tenant5 = 5;
	private const long Tenant7 = 7;

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private static ModelAccountsController Build(SuperBIContext db, long? tid = null)
	{
		var svc = new ModelAccountService(db, new AesGcmSecretStore(new byte[32]));
		var c = new ModelAccountsController(db, svc);
		if (tid.HasValue)
		{
			c.ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext
				{
					User = new ClaimsPrincipal(new ClaimsIdentity(new[]
					{
						new Claim("tid", tid.Value.ToString()),
						new Claim(ClaimTypes.NameIdentifier, "1"),
					})),
				},
			};
		}
		return c;
	}

	[Fact]
	public async Task Create_Returns_Created_With_Masked_Summary()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var controller = Build(ctx);

		var result = await controller.Create(
			new CreateModelAccountRequest(Tenant5, "Qwen", "qwen-plus", "sk-abcdefghij1234"),
			CancellationToken.None) as CreatedAtActionResult;
		Assert.NotNull(result);
		Assert.Equal((int)HttpStatusCode.Created, result!.StatusCode);

		var summary = Assert.IsType<ModelAccountSummary>(result.Value);
		Assert.Contains("***", summary.MaskedKey);
		Assert.DoesNotContain("abcdefghij", summary.MaskedKey);

		// 库内仅存密文，明文绝不落库。
		var entity = await ctx.ModelAccounts.IgnoreQueryFilters().SingleAsync(m => m.Id == summary.Id);
		Assert.StartsWith("v1:", entity.EncryptedKey);
		Assert.DoesNotContain("sk-abcdefghij1234", entity.EncryptedKey + entity.MaskedKey);
	}

	[Fact]
	public async Task List_Returns_Only_Tenant_Scope_And_Masked()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var controller = Build(ctx);

		await controller.Create(new CreateModelAccountRequest(Tenant5, "Qwen", "qwen-plus", "k1"), CancellationToken.None);
		await controller.Create(new CreateModelAccountRequest(Tenant5, "OpenAI", "gpt-4o", "k2"), CancellationToken.None);

		var result = await controller.List(Tenant5, CancellationToken.None) as OkObjectResult;
		Assert.NotNull(result);
		var list = Assert.IsType<List<ModelAccountSummary>>(result!.Value);
		Assert.Equal(2, list.Count);
		Assert.All(list, s => Assert.Contains("***", s.MaskedKey));
	}

	[Fact]
	public async Task Create_Duplicate_Conflict()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var controller = Build(ctx);

		await controller.Create(new CreateModelAccountRequest(Tenant5, "Qwen", "qwen-plus", "k1"), CancellationToken.None);
		var dup = await controller.Create(new CreateModelAccountRequest(Tenant5, "Qwen", "qwen-plus", "k2"), CancellationToken.None)
			as ObjectResult;
		Assert.NotNull(dup);
		Assert.Equal((int)HttpStatusCode.Conflict, dup!.StatusCode);
	}

	[Fact]
	public async Task CrossTenant_Request_Rejected_With_403()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		// 认证租户为 5，却试图以请求体 tenantId=7 越权创建。
		var controller = Build(ctx, tid: Tenant5);

		var result = await controller.Create(
			new CreateModelAccountRequest(Tenant7, "Qwen", "qwen-plus", "k7"),
			CancellationToken.None) as ObjectResult;
		Assert.NotNull(result);
		Assert.Equal((int)HttpStatusCode.Forbidden, result!.StatusCode);

		// 越权请求不得写出任何数据。
		Assert.False(await ctx.ModelAccounts.IgnoreQueryFilters().AnyAsync(m => m.TenantId == Tenant7));
	}

	[Fact]
	public async Task CrossTenant_List_Rejected_With_403()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var controller = Build(ctx, tid: Tenant5);
		// User tid=5 但请求租户 7 → 作用域解析未授权 → 返回 403（不泄露他租户数据）。
		var result = await controller.List(Tenant7, CancellationToken.None) as ObjectResult;
		Assert.NotNull(result);
		Assert.Equal((int)HttpStatusCode.Forbidden, result!.StatusCode);
	}

	[Fact]
	public async Task SetDefault_Keeps_Single_Default()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var controller = Build(ctx);

		var a = (CreatedAtActionResult)await controller.Create(new CreateModelAccountRequest(Tenant5, "Qwen", "qwen-plus", "k1"), CancellationToken.None);
		var b = (CreatedAtActionResult)await controller.Create(new CreateModelAccountRequest(Tenant5, "OpenAI", "gpt-4o", "k2"), CancellationToken.None);
		var aId = ((ModelAccountSummary)a.Value!).Id;
		var bId = ((ModelAccountSummary)b.Value!).Id;

		var set = await controller.SetDefault(bId, Tenant5, CancellationToken.None) as OkObjectResult;
		Assert.NotNull(set);

		var list = (List<ModelAccountSummary>)((OkObjectResult)await controller.List(Tenant5, CancellationToken.None))!.Value!;
		Assert.True(list.Single(x => x.Id == bId).IsDefault);
		Assert.False(list.Single(x => x.Id == aId).IsDefault);
	}

	[Fact]
	public async Task Delete_Removes_Binding()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var controller = Build(ctx);

		var a = (CreatedAtActionResult)await controller.Create(new CreateModelAccountRequest(Tenant5, "Qwen", "qwen-plus", "k1"), CancellationToken.None);
		var aId = ((ModelAccountSummary)a.Value!).Id;

		var del = await controller.Delete(aId, Tenant5, CancellationToken.None) as NoContentResult;
		Assert.NotNull(del);

		var get = await controller.GetById(aId, Tenant5, CancellationToken.None);
		Assert.IsType<NotFoundResult>(get);
	}

	[Fact]
	public async Task GetById_NotFound_Returns_404()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var controller = Build(ctx);

		var get = await controller.GetById(999, Tenant5, CancellationToken.None);
		Assert.IsType<NotFoundResult>(get);
	}
}
