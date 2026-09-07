using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Application.ModelAccounts;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Security;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M7-07 ModelAccountService 单测（手写 SQLite 内存库，不依赖 Moq）。
/// 覆盖：密钥 AES-GCM 加密落库 + 掩码返回、首绑默认、重复冲突、租户隔离、
/// 设默认唯一性、明文解析往返、更新轮换 Key、删除。
/// </summary>
public class ModelAccountServiceTests
{
	private const long Tenant1 = 1;
	private const long Tenant2 = 2;

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private static ISecretStore Store() => new AesGcmSecretStore(new byte[32]);
	private static ModelAccountService Svc(SuperBIContext ctx) => new(ctx, Store());

	[Fact]
	public async Task Create_Encrypts_Key_And_Returns_Mask()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var svc = Svc(ctx);

		const string apiKey = "sk-abcdefghij1234";
		var summary = await svc.CreateAsync(
			new CreateModelAccountRequest(Tenant1, "Qwen", "qwen-plus", apiKey),
			CancellationToken.None);

		Assert.Equal("Qwen", summary.Provider);
		// 掩码：保留头 6 + 尾 4，中间以 *** 替代。
		Assert.Contains("***", summary.MaskedKey);
		Assert.StartsWith("sk-ab", summary.MaskedKey);
		Assert.EndsWith("1234", summary.MaskedKey);
		// 明文绝不进入展示 DTO。
		Assert.DoesNotContain("efgh", summary.MaskedKey);

		var entity = await ctx.ModelAccounts.IgnoreQueryFilters().SingleAsync(m => m.Id == summary.Id);
		Assert.StartsWith("v1:", entity.EncryptedKey);
		Assert.NotEqual(apiKey, entity.EncryptedKey);
		Assert.DoesNotContain(apiKey, entity.EncryptedKey);
	}

	[Fact]
	public async Task Create_FirstBinding_Becomes_Default()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var svc = Svc(ctx);

		var a = await svc.CreateAsync(new CreateModelAccountRequest(Tenant1, "Qwen", "qwen-plus", "k1"), CancellationToken.None);
		var b = await svc.CreateAsync(new CreateModelAccountRequest(Tenant1, "OpenAI", "gpt-4o", "k2"), CancellationToken.None);
		Assert.True(a.IsDefault);
		Assert.False(b.IsDefault);
	}

	[Fact]
	public async Task Create_Duplicate_Provider_Model_Conflict()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var svc = Svc(ctx);

		await svc.CreateAsync(new CreateModelAccountRequest(Tenant1, "Qwen", "qwen-plus", "k1"), CancellationToken.None);
		await Assert.ThrowsAsync<SuperBuilderException>(() =>
			svc.CreateAsync(new CreateModelAccountRequest(Tenant1, "Qwen", "qwen-plus", "k2"), CancellationToken.None));
	}

	[Fact]
	public async Task Tenant_Isolation_Enforced()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var svc = Svc(ctx);

		await svc.CreateAsync(new CreateModelAccountRequest(Tenant1, "Qwen", "qwen-plus", "k1"), CancellationToken.None);
		var t2 = await svc.ListAsync(Tenant2, CancellationToken.None);
		Assert.Empty(t2);
		// 跨租户解析明文返回 null。
		Assert.Null(await svc.ResolvePlaintextKeyAsync(Tenant2, "Qwen", "qwen-plus", CancellationToken.None));
	}

	[Fact]
	public async Task SetDefault_Makes_Only_One_Default()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var svc = Svc(ctx);

		var a = await svc.CreateAsync(new CreateModelAccountRequest(Tenant1, "Qwen", "qwen-plus", "k1"), CancellationToken.None);
		var b = await svc.CreateAsync(new CreateModelAccountRequest(Tenant1, "OpenAI", "gpt-4o", "k2"), CancellationToken.None);
		await svc.SetDefaultAsync(Tenant1, b.Id, CancellationToken.None);

		var list = await svc.ListAsync(Tenant1, CancellationToken.None);
		Assert.True(list.Single(x => x.Id == b.Id).IsDefault);
		Assert.False(list.Single(x => x.Id == a.Id).IsDefault);
	}

	[Fact]
	public async Task ResolvePlaintextKey_RoundTrips()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var svc = Svc(ctx);

		const string apiKey = "sk-secret-rotated-9999";
		await svc.CreateAsync(new CreateModelAccountRequest(Tenant1, "DeepSeek", "deepseek-chat", apiKey), CancellationToken.None);
		var plain = await svc.ResolvePlaintextKeyAsync(Tenant1, "DeepSeek", "deepseek-chat", CancellationToken.None);
		Assert.Equal(apiKey, plain);
	}

	[Fact]
	public async Task Delete_Removes_Binding()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var svc = Svc(ctx);

		var a = await svc.CreateAsync(new CreateModelAccountRequest(Tenant1, "Qwen", "qwen-plus", "k1"), CancellationToken.None);
		await svc.DeleteAsync(Tenant1, a.Id, CancellationToken.None);
		Assert.Empty(await svc.ListAsync(Tenant1, CancellationToken.None));
	}

	[Fact]
	public async Task Update_Rotates_Key_And_Updates_Mask()
	{
		await using var ctx = CreateContext(out var conn);
		await using var _ = conn;
		var svc = Svc(ctx);

		var a = await svc.CreateAsync(new CreateModelAccountRequest(Tenant1, "Qwen", "qwen-plus", "sk-oldkey0001"), CancellationToken.None);
		var updated = await svc.UpdateAsync(Tenant1, a.Id, new UpdateModelAccountRequest(ApiKey: "sk-newkey9999"), CancellationToken.None);
		Assert.EndsWith("9999", updated.MaskedKey);
		Assert.DoesNotContain("0001", updated.MaskedKey);

		var plain = await svc.ResolvePlaintextKeyAsync(Tenant1, "Qwen", "qwen-plus", CancellationToken.None);
		Assert.Equal("sk-newkey9999", plain);
	}
}
