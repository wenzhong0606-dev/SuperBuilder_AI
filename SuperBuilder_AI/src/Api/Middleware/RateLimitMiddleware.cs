using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SuperBuilder_AI.Middleware;

/// <summary>
/// 极简固定窗口限流中间件（P11.0 安全轨道）。
///
/// <para>
/// 仅对 <c>/api/*</c> 按客户端 IP（或令牌标识）做固定窗口计数，超过阈值返回 429。
/// 阈值宽松（默认 120 次/分钟），不会冲击 Golden 单次运行；纯内存实现、零外部依赖。
/// 异常静默，限流判定失败时不阻断主链路。
/// </para>
/// </summary>
public sealed class RateLimitMiddleware
{
	private readonly RequestDelegate _next;
	private static readonly ConcurrentDictionary<string, Window> Counters = new();
	private static readonly TimeSpan WindowSize = TimeSpan.FromMinutes(1);
	private const int Limit = 120;

	public RateLimitMiddleware(RequestDelegate next) => _next = next;

	public async Task InvokeAsync(HttpContext context)
	{
		if (!context.Request.Path.StartsWithSegments("/api"))
		{
			await _next(context);
			return;
		}

		var key = ResolveKey(context);
		var now = DateTime.UtcNow;

		try
		{
			var window = Counters.AddOrUpdate(key,
				_ => new Window(now, 1),
				(_, existing) =>
				{
					if (now - existing.Start >= WindowSize)
						return new Window(now, 1);
					existing.Count++;
					return existing;
				});

			if (window.Count > Limit)
			{
				context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
				context.Response.Headers["Retry-After"] = WindowSize.Seconds.ToString();
				return;
			}
		}
		catch
		{
			// 限流失败不阻断主链路
		}

		await _next(context);
	}

	private static string ResolveKey(HttpContext context)
	{
		if (context.Request.Headers.TryGetValue("X-Api-Token", out var token) &&
			!string.IsNullOrWhiteSpace(token.ToString()))
			return "t:" + token.ToString()!;

		var ip = context.Connection.RemoteIpAddress?.ToString();
		if (!string.IsNullOrEmpty(ip)) return "ip:" + ip;
		return "anon";
	}

	private sealed class Window
	{
		public DateTime Start { get; }
		public int Count { get; set; }

		public Window(DateTime start, int count)
		{
			Start = start;
			Count = count;
		}
	}
}
