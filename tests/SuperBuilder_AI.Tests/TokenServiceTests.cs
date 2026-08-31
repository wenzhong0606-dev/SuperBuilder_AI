using System.Threading.Tasks;
using SuperBuilder_AI.Services.Auth;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>P11.0 令牌服务单元测试（无 DB / 无 LLM，确定性）。</summary>
public class TokenServiceTests
{
	private const string Key = "unit-test-signing-key";

	[Fact]
	public void Issue_Then_Validate_Roundtrips_Claims()
	{
		var svc = new TokenService(Key);
		var token = svc.Issue(tenantId: 7, userId: 42, "alice", new[] { "dashboard:view", "app:create" });

		var principal = svc.Validate(token);
		Assert.NotNull(principal);
		Assert.Equal(7, principal!.TenantId);
		Assert.Equal(42, principal.UserId);
		Assert.Equal("alice", principal.Username);
		Assert.Contains("dashboard:view", principal.Permissions);
		Assert.Contains("app:create", principal.Permissions);
	}

	[Fact]
	public void Validate_Null_Or_Garbage_Returns_Null()
	{
		var svc = new TokenService(Key);
		Assert.Null(svc.Validate(null));
		Assert.Null(svc.Validate(string.Empty));
		Assert.Null(svc.Validate("not-a-real-token"));
	}

	[Fact]
	public void Validate_Tampered_Token_Returns_Null()
	{
		var svc = new TokenService(Key);
		var token = svc.Issue(1, 2, "bob", new[] { "dashboard:view" });
		var tampered = token + "x";

		Assert.Null(svc.Validate(tampered));
	}

	[Fact]
	public void Validate_Wrong_Key_Returns_Null()
	{
		var issuer = new TokenService(Key);
		var validator = new TokenService("a-different-key");

		var token = issuer.Issue(1, 2, "bob", new[] { "dashboard:view" });
		Assert.Null(validator.Validate(token));
	}

	[Fact]
	public async Task Issue_Is_Deterministic_For_Identical_Payloads()
	{
		var svc = new TokenService(Key);
		// 相同载荷（同一秒精度内）应生成完全相同的令牌 —— 确定性是正确行为
		var a = svc.Issue(1, 2, "bob", System.Array.Empty<string>());
		var b = svc.Issue(1, 2, "bob", System.Array.Empty<string>());
		Assert.Equal(a, b);
		Assert.NotNull(svc.Validate(a));
		Assert.NotNull(svc.Validate(b));

		// 不同载荷（权限不同）应生成不同令牌
		var c = svc.Issue(1, 2, "bob", new[] { "dashboard:view" });
		Assert.NotEqual(a, c);
		Assert.NotNull(svc.Validate(c));
		await Task.CompletedTask;
	}
}
