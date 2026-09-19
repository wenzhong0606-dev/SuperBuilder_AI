using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// 安全回归（hardening 验收 #4）：/auth/session/start 与 /auth/session/end 必须是 POST（而非 GET 改状态），
/// 且经 antiforgery 保护；handoff code 不得出现在 URL 查询参数中（须由请求体承载）。
/// </summary>
public sealed class SessionEndpointAuthTests
{
	[Fact]
	public void SessionEndpoints_MustBePost_WithAntiforgery()
	{
		var path = ResolveRepoFile("SuperBuilder_AI.Web/Services/SessionEndpointRoutes.cs");
		Assert.True(File.Exists(path), $"找不到 SessionEndpointRoutes.cs：{path}");
		var content = File.ReadAllText(path);

		// 必须是 POST，不得是 GET。
		Assert.Contains("MapPost(\"/auth/session/start\"", content, StringComparison.Ordinal);
		Assert.Contains("MapPost(\"/auth/session/end\"", content, StringComparison.Ordinal);
		Assert.DoesNotContain("MapGet(\"/auth/session/start\"", content, StringComparison.Ordinal);
		Assert.DoesNotContain("MapGet(\"/auth/session/end\"", content, StringComparison.Ordinal);

		// 须启用 antiforgery 校验（handler 内手动 IAntiforgery.ValidateRequestAsync，不依赖 MVC 特性）。
		Assert.Contains("ValidateRequestAsync", content, StringComparison.Ordinal);

		// handoff code 必须来自请求体（[FromForm]），不得出现在 URL 查询参数（[FromQuery]）。
		Assert.Contains("[FromForm]", content, StringComparison.Ordinal);
		Assert.DoesNotContain("[FromQuery]", content, StringComparison.Ordinal);
	}

	private static string ResolveRepoFile(string relativePath)
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !dir.GetDirectories("SuperBuilder_AI.Web").Any())
			dir = dir.Parent;
		Assert.NotNull(dir);
		return Path.Combine(dir!.FullName, relativePath);
	}
}
