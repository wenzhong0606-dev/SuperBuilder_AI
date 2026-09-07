using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M8-03 FileDownloadService 行为测试：下载必须返回结果、不得静默吞异常
/// （红线：禁止「假成功」——失败时由调用方用 Toast 提示，而非静默成功）。
/// </summary>
public sealed class FileDownloadServiceTests
{
	private sealed class OkJsRuntime : IJSRuntime
	{
		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
			=> ValueTask.FromResult(default(TValue)!);
		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, TimeSpan timeout, object?[]? args)
			=> ValueTask.FromResult(default(TValue)!);
		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
			=> ValueTask.FromResult(default(TValue)!);
	}

	private sealed class ThrowJsRuntime : IJSRuntime
	{
		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
			=> ValueTask.FromException<TValue>(new JSException("boom"));
		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, TimeSpan timeout, object?[]? args)
			=> ValueTask.FromException<TValue>(new JSException("boom"));
		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
			=> ValueTask.FromException<TValue>(new JSException("boom"));
	}

	[Fact]
	public async Task DownloadTextAsync_ReturnsTrue_WhenJsSucceeds()
	{
		var svc = new FileDownloadService(new OkJsRuntime());
		var ok = await svc.DownloadTextAsync("x.csv", "text/csv", "a,b");
		Assert.True(ok);
	}

	[Fact]
	public async Task DownloadTextAsync_ReturnsFalse_WhenJsThrows()
	{
		var svc = new FileDownloadService(new ThrowJsRuntime());
		var ok = await svc.DownloadTextAsync("x.csv", "text/csv", "a,b");
		// 不向上抛异常，返回 false 交由调用方提示（非静默成功）
		Assert.False(ok);
	}
}
