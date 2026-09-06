using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Caching;
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
		public long? CapturedRequestedDataSourceId;
		public IReadOnlyCollection<long>? CapturedAuthorizedDataSourceIds;
		public int Calls;
		public Task<BIResponse> AskAsync(string question, long tenantId, long? requestedDataSourceId = null, IReadOnlyCollection<long>? authorizedDataSourceIds = null)
		{
			Calls++;
			Captured = new BIResponse { Success = true, Question = question };
			CapturedRequestedDataSourceId = requestedDataSourceId;
			CapturedAuthorizedDataSourceIds = authorizedDataSourceIds;
			return Task.FromResult(Captured);
		}
	}

	private sealed class FakeDataSourceAuthorization : IDataSourceAuthorizationService
	{
		public IReadOnlyList<long> Allowed { get; set; } = Array.Empty<long>();
		public Task<IReadOnlyList<long>> GetAuthorizedDataSourceIdsAsync(long tenantId, long userId, CancellationToken ct = default) => Task.FromResult(Allowed);
		public Task<bool> IsAuthorizedAsync(long tenantId, long userId, long dataSourceId, CancellationToken ct = default) => Task.FromResult(Allowed.Contains(dataSourceId));
		public Task GrantAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task RevokeAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task RevokeBySubjectAsync(long tenantId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task<IReadOnlyList<DataSourceAccessGrant>> DetectOrphanGrantsAsync(long tenantId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DataSourceAccessGrant>>(Array.Empty<DataSourceAccessGrant>());
	}

	private sealed class FakeRowSecurity : IRowLevelSecurityService
	{
		public string Fingerprint { get; set; } = "v1";
		public Task<string> GetPolicyFingerprintAsync(long tenantId, long userId, CancellationToken ct = default) => Task.FromResult(Fingerprint);
		public Task ApplyAsync(QueryPlan plan, long tenantId, long userId, CancellationToken ct = default) => Task.CompletedTask;
	}

	/// <summary>固定返回 7 维版本上下文的假提供器，用于验证 Ask 缓存键折叠完整维度。</summary>
	private sealed class FakeVersionProvider : IAskCacheVersionProvider
	{
		public Task<AskCacheVersionContext> ResolveAsync(long tenantId, long userId, IReadOnlyCollection<long> authorizedDataSourceIds, string? permissionFingerprint, CancellationToken ct = default)
			=> Task.FromResult(new AskCacheVersionContext(
				permissionFingerprint ?? "perm", "pol", "en-US", "qwen-plus", "sem", "meta", "ds"));
	}

	/// <summary>记录缓存读写使用的数据源Id，用于验证缓存键与执行约束同源（P0-01）。</summary>
	private sealed class FakeCache : IAskResponseCache
	{
		public List<long> GetDataSourceIds { get; } = new();
		public List<long> SetDataSourceIds { get; } = new();
		public List<string> GetQuestions { get; } = new();
		public BIResponse? Get(long tenantId, string question, long dataSourceId)
		{
			GetDataSourceIds.Add(dataSourceId);
			GetQuestions.Add(question);
			return null;
		}
		public void Set(long tenantId, string question, long dataSourceId, BIResponse response)
			=> SetDataSourceIds.Add(dataSourceId);
		public (long Hits, long Misses) Snapshot() => (0, 0);
		public void Clear() { }
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
		public Task<IdentityResult> SetPasswordAsync(long tenantId, long userId, string password, CancellationToken ct = default)
			=> throw new System.NotImplementedException();
		public Task<IdentityResult> SetUserStatusAsync(long tenantId, long userId, UserStatus newStatus, CancellationToken ct = default)
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

	private static AskController Build(bool allow, out FakeBi bi, IAskResponseCache? cache = null)
	{
		bi = new FakeBi();
		var ctrl = new AskController(bi, new FakeIdentity { Allow = allow }, cache);
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
		var result = await ctrl.Ask(new AskRequest { Question = "请查询销售额" });

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

		var result = await ctrl.Ask(new AskRequest { Question = "请查询销售额" });
		Assert.IsType<UnauthorizedObjectResult>(result);
	}

	[Fact]
	public async Task Ask_WithDataSourceId_ThreadsSameDataSourceToBiAndCache()
	{
		var cache = new FakeCache();
		var ctrl = Build(allow: true, out var bi, cache);
		var result = await ctrl.Ask(new AskRequest { Question = "本月销售额", DataSourceId = 5 });

		Assert.IsType<OkObjectResult>(result);
		// P0-01：请求的数据源必须原样透传给 BI 服务（执行连接约束）。
		Assert.Equal(5, bi.CapturedRequestedDataSourceId);
		// P0-01：缓存键必须与执行约束同源（不再用推断值，避免“B 结果缓存到键 A”）。
		Assert.Contains(5, cache.GetDataSourceIds);
		Assert.Contains(5, cache.SetDataSourceIds);
	}

	[Fact]
	public async Task Ask_WithoutDataSourceId_PassesNullAndUsesZeroCacheKey()
	{
		var cache = new FakeCache();
		var ctrl = Build(allow: true, out var bi, cache);
		var result = await ctrl.Ask(new AskRequest { Question = "本月销售额" });

		Assert.IsType<OkObjectResult>(result);
		// 未指定数据源：保持默认推断行为，不向 BI 服务传递约束。
		Assert.Null(bi.CapturedRequestedDataSourceId);
		// 缓存键使用 0（默认），与执行侧“无约束推断”路径一致。
		Assert.Contains(0, cache.GetDataSourceIds);
		Assert.Contains(0, cache.SetDataSourceIds);
	}

	[Fact]
	public async Task Ask_ExplicitUnauthorizedDataSource_Returns403BeforeCacheAndBi()
	{
		var bi = new FakeBi();
		var cache = new FakeCache();
		var authorization = new FakeDataSourceAuthorization { Allowed = new[] { 7L } };
		var ctrl = new AskController(bi, new FakeIdentity(), cache, authorization)
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext { User = Authenticated(3, 5) }
			}
		};

		var result = await ctrl.Ask(new AskRequest { Question = "请查询销售额", DataSourceId = 8 });

		var forbidden = Assert.IsType<ObjectResult>(result);
		Assert.Equal(403, forbidden.StatusCode);
		Assert.Equal(0, bi.Calls);
		Assert.Empty(cache.GetDataSourceIds);
	}

	[Fact]
	public async Task Ask_RevokedGrant_BlocksPreviouslyCacheableRequest()
	{
		var bi = new FakeBi();
		var cache = new FakeCache();
		var authorization = new FakeDataSourceAuthorization { Allowed = new[] { 7L } };
		var ctrl = new AskController(bi, new FakeIdentity(), cache, authorization)
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext { User = Authenticated(3, 5) }
			}
		};

		Assert.IsType<OkObjectResult>(await ctrl.Ask(new AskRequest { Question = "请查询销售额" }));
		Assert.Equal(7, bi.CapturedRequestedDataSourceId);
		Assert.Contains(7, cache.GetDataSourceIds);
		authorization.Allowed = Array.Empty<long>();

		var result = await ctrl.Ask(new AskRequest { Question = "请查询销售额" });
		Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
		Assert.Equal(1, bi.Calls);
		Assert.Single(cache.GetDataSourceIds);
	}

	[Fact]
	public async Task Ask_MultipleAuthorizedSources_BypassesAmbiguousCache()
	{
		var bi = new FakeBi();
		var cache = new FakeCache();
		var ctrl = new AskController(
			bi,
			new FakeIdentity(),
			cache,
			new FakeDataSourceAuthorization { Allowed = new[] { 7L, 8L } })
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext { User = Authenticated(3, 5) }
			}
		};

		Assert.IsType<OkObjectResult>(await ctrl.Ask(new AskRequest { Question = "请查询销售额" }));
		Assert.Null(bi.CapturedRequestedDataSourceId);
		Assert.Equal(new[] { 7L, 8L }, bi.CapturedAuthorizedDataSourceIds);
		Assert.Empty(cache.GetDataSourceIds);
		Assert.Empty(cache.SetDataSourceIds);
	}

	[Fact]
	public async Task Ask_PolicyFingerprintChange_UsesDifferentCacheKey()
	{
		var bi = new FakeBi();
		var cache = new FakeCache();
		var rowSecurity = new FakeRowSecurity();
		var ctrl = new AskController(bi, new FakeIdentity(), cache,
			new FakeDataSourceAuthorization { Allowed = new[] { 7L } }, rowSecurity)
		{
			ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Authenticated(3, 5) } }
		};

		await ctrl.Ask(new AskRequest { Question = "请查询销售额", DataSourceId = 7 });
		rowSecurity.Fingerprint = "v2";
		await ctrl.Ask(new AskRequest { Question = "请查询销售额", DataSourceId = 7 });

		Assert.Equal(2, cache.GetQuestions.Count);
		Assert.NotEqual(cache.GetQuestions[0], cache.GetQuestions[1]);
		Assert.Contains("policy:v1", cache.GetQuestions[0]);
		Assert.Contains("policy:v2", cache.GetQuestions[1]);
	}

	[Fact]
	public async Task Ask_Question_Too_Short_Returns_400()
	{
		var ctrl = Build(allow: true, out _);
		var result = await ctrl.Ask(new AskRequest { Question = "q" });
		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task Ask_Question_Too_Long_Returns_400()
	{
		var ctrl = Build(allow: true, out _);
		var result = await ctrl.Ask(new AskRequest { Question = new string('x', 2001) });
		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task Ask_ConversationId_Too_Long_Returns_400()
	{
		var ctrl = Build(allow: true, out _);
		var result = await ctrl.Ask(new AskRequest { Question = "请查询销售额", ConversationId = new string('a', 200) });
		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task Ask_Refine_History_Too_Many_Rounds_Returns_400()
	{
		var ctrl = Build(allow: true, out _);
		var history = new List<AskRefineTurn>();
		for (var i = 0; i < 21; i++)
			history.Add(new AskRefineTurn { Role = "user", Content = $"轮次{i}" });

		var result = await ctrl.Refine(new AskRefineRequest
		{
			Instruction = "只看华东地区",
			History = history,
		});

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task Ask_WithVersionProvider_BuildsFullSevenDimensionCacheKey()
	{
		var cache = new FakeCache();
		var version = new FakeVersionProvider();
		var ctrl = new AskController(
			new FakeBi(), new FakeIdentity(), cache,
			new FakeDataSourceAuthorization { Allowed = new[] { 7L } },
			new FakeRowSecurity(), null, version)
		{
			ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Authenticated(3, 5) } }
		};

		await ctrl.Ask(new AskRequest { Question = "请查询销售额", DataSourceId = 7 });

		var key = cache.GetQuestions[0];
		Assert.Contains("perm:", key);
		Assert.Contains("policy:", key);
		Assert.Contains("culture:", key);
		Assert.Contains("model:qwen-plus", key);
		Assert.Contains("semantic:sem", key);
		Assert.Contains("metadata:meta", key);
		Assert.Contains("ds:ds", key);
	}
}
