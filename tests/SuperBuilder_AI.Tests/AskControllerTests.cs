using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>P11.0 Ask 问数端点测试（注入假服务，无 LLM / 无 DB，确定性）。</summary>
public class AskControllerTests
{
	private sealed class FakeBi : IBIConversationService
	{
		public BIResponse? Captured;
		public Task<BIResponse> AskAsync(string question, long dataSourceId)
		{
			Captured = new BIResponse { Success = true, Question = question };
			return Task.FromResult(Captured);
		}
	}

	private sealed class FakeIdentity : IIdentityService
	{
		public bool Allow = true;
		public Task SeedAsync(CancellationToken ct = default) => Task.CompletedTask;
		public Task<IdentityResult> CreateUserAsync(long tenantId, string username, string displayName, string email, string[]? roleCodes, CancellationToken ct = default)
			=> throw new System.NotImplementedException();
		public Task<IdentityResult> AssignRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default)
			=> throw new System.NotImplementedException();
		public Task<IdentityResult> RevokeRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default)
			=> throw new System.NotImplementedException();
		public Task<IReadOnlyList<string>> GetPermissionsAsync(long tenantId, long userId, CancellationToken ct = default)
			=> throw new System.NotImplementedException();
		public Task<bool> HasPermissionAsync(long tenantId, long userId, string permissionCode, CancellationToken ct = default)
			=> Task.FromResult(Allow);
	}

	private static ClaimsPrincipal Authenticated(long tenantId, long userId)
		=> new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
			new Claim("tid", tenantId.ToString()),
		}, "Bearer"));

	private static AskController Build(bool allow, out FakeBi bi)
	{
		bi = new FakeBi();
		var ctrl = new AskController(bi, new FakeIdentity { Allow = allow });
		ctrl.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = Authenticated(tenantId: 3, userId: 5) },
		};
		return ctrl;
	}

	[Fact]
	public async Task Ask_With_Permission_Returns_Ok_With_BIResponse()
	{
		var ctrl = Build(allow: true, out var bi);
		var result = await ctrl.Ask(new AskRequest { Question = "本月销售额" });

		var ok = Assert.IsType<OkObjectResult>(result);
		var resp = Assert.IsType<BIResponse>(ok.Value);
		Assert.Equal("本月销售额", resp.Question);
		Assert.Equal("本月销售额", bi.Captured?.Question);
	}

	[Fact]
	public async Task Ask_Without_Permission_Returns_403()
	{
		var ctrl = Build(allow: false, out _);
		var result = await ctrl.Ask(new AskRequest { Question = "q" });

		var forbid = Assert.IsType<ObjectResult>(result);
		Assert.Equal(403, forbid.StatusCode);
	}

	[Fact]
	public async Task Ask_With_Empty_Question_Returns_400()
	{
		var ctrl = Build(allow: true, out _);
		var result = await ctrl.Ask(new AskRequest { Question = "  " });

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task Ask_Without_Authenticated_User_Returns_401()
	{
		var bi = new FakeBi();
		var ctrl = new AskController(bi, new FakeIdentity { Allow = true });
		ctrl.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) },
		};

		var result = await ctrl.Ask(new AskRequest { Question = "q" });
		Assert.IsType<UnauthorizedObjectResult>(result);
	}
}
