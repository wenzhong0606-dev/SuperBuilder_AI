using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Components.Models;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>Phase 1（M8-05）：AuthStore 重构为依赖 IAuthPersistence 后的行为契约单测。</summary>
public sealed class AuthStoreTests
{
	// ───── ValidateAsync：仅 401 清会话，其他状态保留（网络/服务端临时异常不得误登出） ─────

	[Theory]
	[InlineData(0)]
	[InlineData(500)]
	[InlineData(503)]
	public async Task Validate_preserves_session_when_server_cannot_confirm_token(int status)
	{
		var state = new AppState { Token = "still-possibly-valid", TenantId = 7, UserId = 9 };
		var store = new AuthStore(new InMemoryPersistence(), state, new StatusApiClient(status));

		var valid = await store.ValidateAsync();

		Assert.True(valid);
		Assert.Equal("still-possibly-valid", state.Token);
		Assert.Equal(7, state.TenantId);
	}

	[Fact]
	public async Task Validate_clears_session_only_when_server_returns_401()
	{
		var state = new AppState { Token = "expired", TenantId = 7, UserId = 9 };
		var store = new AuthStore(new InMemoryPersistence(), state, new StatusApiClient(401));

		var valid = await store.ValidateAsync();

		Assert.False(valid);
		Assert.False(state.IsAuthenticated);
		Assert.Equal(0, state.TenantId);
	}

	[Fact]
	public async Task Validate_returns_false_when_not_authenticated()
	{
		var state = new AppState(); // 无令牌
		var store = new AuthStore(new InMemoryPersistence(), state, new StatusApiClient(200));

		var valid = await store.ValidateAsync();

		Assert.False(valid);
	}

	// ───── 登录写入 / 还原 / 清除（Phase 1 持久化抽象契约） ─────

	[Fact]
	public async Task SetFromLoginAsync_populates_state_and_persists()
	{
		var state = new AppState();
		var persistence = new InMemoryPersistence();
		var store = new AuthStore(persistence, state, new StatusApiClient(200));

		var result = new AuthResult
		{
			Token = "jwt-abc",
			TenantId = 3,
			HomeTenantId = 3,
			UserId = 11,
			Username = "alice",
			Permissions = new List<string> { "dashboard:view" },
			AvailableCultures = new List<string> { "zh-CN", "en-US" },
			DefaultCulture = "zh-CN",
		};

		await store.SetFromLoginAsync(result);

		Assert.True(state.IsAuthenticated);
		Assert.Equal("jwt-abc", state.Token);
		Assert.Equal(3, state.TenantId);
		Assert.Equal(11, state.UserId);
		var saved = await persistence.LoadAsync();
		Assert.NotNull(saved);
		Assert.Equal("jwt-abc", saved!.Token);
	}

	[Fact]
	public async Task RestoreAsync_loads_into_state_and_marks_restored()
	{
		var persistence = new InMemoryPersistence();
		await persistence.SaveAsync(new SessionData(
			Token: "jwt-xyz", TenantId: 5, HomeTenantId: 5, UserId: 21, Username: "bob",
			Permissions: new List<string> { "app:create" },
			AvailableCultures: new List<string> { "zh-CN" }, DefaultCulture: "zh-CN"));

		var state = new AppState();
		var store = new AuthStore(persistence, state, new StatusApiClient(200));

		await store.RestoreAsync();

		Assert.True(state.IsAuthenticated);
		Assert.Equal("jwt-xyz", state.Token);
		Assert.Equal(21, state.UserId);
		Assert.True(state.SessionRestored);
	}

	[Fact]
	public async Task RestoreAsync_marks_restored_even_when_no_session()
	{
		var state = new AppState();
		var store = new AuthStore(new InMemoryPersistence(), state, new StatusApiClient(200));

		await store.RestoreAsync();

		Assert.False(state.IsAuthenticated);
		Assert.True(state.SessionRestored); // 守卫必须解除，否则首屏卡加载态
	}

	[Fact]
	public async Task ClearAsync_clears_state_and_persistence()
	{
		var persistence = new InMemoryPersistence();
		var state = new AppState { Token = "t", TenantId = 1, UserId = 2, Username = "u" };
		var store = new AuthStore(persistence, state, new StatusApiClient(200));
		await store.SetFromLoginAsync(new AuthResult
		{
			Token = "t", TenantId = 1, HomeTenantId = 1, UserId = 2, Username = "u",
			Permissions = new List<string>(), AvailableCultures = new List<string> { "zh-CN" }, DefaultCulture = "zh-CN"
		});

		await store.ClearAsync();

		Assert.False(state.IsAuthenticated);
		Assert.Equal(0, state.TenantId);
		Assert.Null(await persistence.LoadAsync());
	}

	[Fact]
	public async Task Save_then_new_store_restore_roundtrips()
	{
		var persistence = new InMemoryPersistence();
		var first = new AuthStore(persistence, new AppState(), new StatusApiClient(200));
		await first.SetFromLoginAsync(new AuthResult
		{
			Token = "roundtrip", TenantId = 8, HomeTenantId = 8, UserId = 33, Username = "carol",
			Permissions = new List<string> { "x" }, AvailableCultures = new List<string> { "en-US" }, DefaultCulture = "en-US"
		});

		var secondState = new AppState();
		var second = new AuthStore(persistence, secondState, new StatusApiClient(200));
		await second.RestoreAsync();

		Assert.Equal("roundtrip", secondState.Token);
		Assert.Equal(33, secondState.UserId);
		Assert.Equal("carol", secondState.Username);
	}

	// ───── 测试替身 ─────

	private sealed class InMemoryPersistence : IAuthPersistence
	{
		private SessionData? _data;
		public Task SaveAsync(SessionData data) { _data = data; return Task.CompletedTask; }
		public Task<SessionData?> LoadAsync() => Task.FromResult(_data);
		public Task ClearAsync() { _data = null; return Task.CompletedTask; }
	}

	private sealed class StatusApiClient(int status) : IApiClient
	{
		public Task<(JsonElement? Data, int Status, string? Error, string? Code)> PostJsonAsync(string relativeUrl, object? body = null, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(JsonElement? Data, int Status, string? Error, string? Code)> GetJsonAsync(string relativeUrl, CancellationToken ct = default)
			=> Task.FromResult<(JsonElement?, int, string?, string?)>((null, status, status == 200 ? null : "temporary", null));

		public Task<(AuthResult? Result, string? Error, string? Code)> LoginAsync(string username, long tenantId, string? password = null, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(long Id, string? TenantCode, string? Name, string? Error)> ResolveTenantByCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(TenantSwitchResult? Result, string? Error, string? Code)> SwitchTenantAsync(long tenantId, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(SelfRegistrationResult? Result, string? Error, string? Code)> RegisterSelfAsync(string tenantCode, string tenantName, string adminUsername, string adminEmail, string adminPassword, string? adminDisplayName = null, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(SelfRegistrationConfigView? Result, string? Error)> GetSelfRegistrationConfigAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<string?> AskRawAsync(string question, long? dataSourceId, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<AskOutcome> AskAsync(string question, long? dataSourceId, string? conversationId = null, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<AskOutcome> RefineAsync(string? question, string instruction, IEnumerable<RefineTurn>? history, long? dataSourceId, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, string? Code, string? Error)> PublishAppAsync(long tenantId, string dslJson, string? code, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct = default) where T : class => throw new NotSupportedException();
		public Task<(bool Ok, int Status, string? Error, string? Code)> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, int Status, string? Error, string? Code)> PostAsync(string relativeUrl, object? body = null, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, int Status, string? Error, string? Code)> PutAsync(string relativeUrl, object? body, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, int Status, string? Error, string? Code)> PatchAsync(string relativeUrl, object? body = null, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, int Status, string? Error, string? Code)> DeleteAsync(string relativeUrl, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(string? Text, int Status, string? Error)> GetTextAsync(string relativeUrl, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(DemoInstallPlan? Result, string? Error)> GetDemoDataPlanAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(DemoInstallResult? Result, string? Error, string? Code)> InstallDemoDataAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(string? Culture, string? Error)> GetUserLanguageAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(string? Culture, string? Error, string? Code)> SetUserLanguageAsync(string culture, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(IReadOnlyList<AdminLanguageView>? Result, string? Error)> GetAdminLanguagesAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, string? Error)> CreateLanguageAsync(AdminLanguageCreate model, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, string? Error)> UpdateLanguageAsync(long id, AdminLanguageUpdate model, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, string? Error)> SetLanguageEnabledAsync(long id, bool enabled, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, string? Error)> ReorderLanguagesAsync(IReadOnlyList<long> orderedIds, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(IReadOnlyList<PublicLanguageView>? Result, string? Error)> GetPublicLanguagesAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, int Status, long? JobId, string? Error, string? Code)> StartScanAsync(long dataSourceId, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(ScanJobView? Job, int Status, string? Error, string? Code)> GetScanJobAsync(long dataSourceId, long jobId, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(ScanJobView? Job, int Status, string? Error, string? Code)> GetLatestScanJobAsync(long dataSourceId, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, int Status, string? Error, string? Code)> CancelScanAsync(long dataSourceId, long jobId, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, int Status, long? JobId, string? Error, string? Code)> RetryFailedScanAsync(long dataSourceId, long jobId, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, int Status, string? Error, string? Code)> TestConnectionAsync(long dataSourceId, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, int Status, string? Error, string? Code)> SetDataSourceEnabledAsync(long dataSourceId, bool enabled, CancellationToken ct = default) => throw new NotSupportedException();
	}
}
