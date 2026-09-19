using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// 安全回归（hardening 验收 #2）：会话 id 不得经 window.__sbSessionId 暴露给浏览器端 JavaScript。
/// 该全局变量曾被 XSS 读取并重放；Phase 1 后改为服务端在初始 HTTP 请求读取 cookie，
/// 通过 Blazor 组件参数（非 JS 全局）注入电路，JS 永远接触不到 sessionId。
/// </summary>
public sealed class SessionIdExposureTests
{
	[Fact]
	public void HostPage_MustNotExposeSessionIdToJavaScript()
	{
		var hostPath = ResolveRepoFile("SuperBuilder_AI.Web/Pages/_Host.cshtml");
		Assert.True(File.Exists(hostPath), $"找不到 _Host.cshtml：{hostPath}");

		var content = File.ReadAllText(hostPath);
		// 验收 #2：页面不得经 window 全局变量暴露会话 id（既无赋值也无 JS 读取语句）。
		Assert.DoesNotContain("window.__sbSessionId =", content, System.StringComparison.Ordinal);
		Assert.DoesNotContain("__sbSessionId", content, System.StringComparison.Ordinal);
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
