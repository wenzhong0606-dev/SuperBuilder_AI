using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using SuperBuilder_AI.Components.Models;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class AuthStoreTests
{
	[Theory]
	[InlineData(0)]
	[InlineData(500)]
	[InlineData(503)]
	public async Task Validate_preserves_session_when_server_cannot_confirm_token(int status)
	{
		var state = new AppState { Token = "still-possibly-valid", TenantId = 7, UserId = 9 };
		var store = new AuthStore(new NoopJsRuntime(), state, new StatusApiClient(status));

		var valid = await store.ValidateAsync();

		Assert.True(valid);
		Assert.Equal("still-possibly-valid", state.Token);
		Assert.Equal(7, state.TenantId);
	}

	[Fact]
	public async Task Validate_clears_session_only_when_server_returns_401()
	{
		var state = new AppState { Token = "expired", TenantId = 7, UserId = 9 };
		var store = new AuthStore(new NoopJsRuntime(), state, new StatusApiClient(401));

		var valid = await store.ValidateAsync();

		Assert.False(valid);
		Assert.False(state.IsAuthenticated);
		Assert.Equal(0, state.TenantId);
	}

	private sealed class NoopJsRuntime : IJSRuntime
	{
		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
			=> ValueTask.FromResult(default(TValue)!);

		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
			=> ValueTask.FromResult(default(TValue)!);
	}

	private sealed class StatusApiClient(int status) : IApiClient
	{
		public Task<(JsonElement? Data, int Status, string? Error)> GetJsonAsync(string relativeUrl, CancellationToken ct = default)
			=> Task.FromResult<(JsonElement?, int, string?)>((null, status, status == 200 ? null : "temporary"));

		public Task<(AuthResult? Result, string? Error)> LoginAsync(string username, long tenantId, string? password = null, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(TenantSwitchResult? Result, string? Error)> SwitchTenantAsync(long tenantId, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(SelfRegistrationResult? Result, string? Error)> RegisterSelfAsync(string tenantCode, string tenantName, string adminUsername, string adminEmail, string adminPassword, string? adminDisplayName = null, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(SelfRegistrationConfigView? Result, string? Error)> GetSelfRegistrationConfigAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<string?> AskRawAsync(string question, long? dataSourceId, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<AskOutcome> AskAsync(string question, long? dataSourceId, string? conversationId = null, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<AskOutcome> RefineAsync(string? question, string instruction, IEnumerable<RefineTurn>? history, long? dataSourceId, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, string? Code, string? Error)> PublishAppAsync(long tenantId, string dslJson, string? code, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct = default) where T : class => throw new NotSupportedException();
		public Task<(bool Ok, int Status, string? Error)> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, int Status, string? Error)> PostAsync(string relativeUrl, object? body = null, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, int Status, string? Error)> PutAsync(string relativeUrl, object? body, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, int Status, string? Error)> PatchAsync(string relativeUrl, object? body = null, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, int Status, string? Error)> DeleteAsync(string relativeUrl, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(string? Text, int Status, string? Error)> GetTextAsync(string relativeUrl, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(DemoInstallPlan? Result, string? Error)> GetDemoDataPlanAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(DemoInstallResult? Result, string? Error)> InstallDemoDataAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(string? Culture, string? Error)> GetUserLanguageAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(string? Culture, string? Error)> SetUserLanguageAsync(string culture, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(IReadOnlyList<AdminLanguageView>? Result, string? Error)> GetAdminLanguagesAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, string? Error)> CreateLanguageAsync(AdminLanguageCreate model, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, string? Error)> UpdateLanguageAsync(long id, AdminLanguageUpdate model, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, string? Error)> SetLanguageEnabledAsync(long id, bool enabled, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(bool Ok, string? Error)> ReorderLanguagesAsync(IReadOnlyList<long> orderedIds, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<(IReadOnlyList<PublicLanguageView>? Result, string? Error)> GetPublicLanguagesAsync(CancellationToken ct = default) => throw new NotSupportedException();
	}
}
