using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Api.Errors;

namespace SuperBuilder_AI.Middleware;

/// <summary>
/// 固定窗口限流中间件（P11.0 安全轨道 / M0-08 强化）。
///
/// <para>键策略（M0-08）：限流置于鉴权之后执行，故登录后请求以 <c>TenantId+UserId</c> 为键，
/// 登录前/匿名请求以可信客户端 IP（经 ForwardedHeaders 还原）+ 设备（User-Agent 哈希）为键；
/// 不再依赖可伪造的 <c>X-Api-Token</c> 头，反向代理下也不会退化为单一共享桶。</para>
///
/// <para>登录路径采用更严格阈值（防爆破），其余 /api 走全局阈值。超阈值返回结构化 429（ApiError + Retry-After）。</para>
/// </summary>
public sealed class RateLimitMiddleware
{
	private readonly RequestDelegate _next;
	private readonly RateLimitOptions _options;
	private static readonly ConcurrentDictionary<string, Window> Counters = new();

	public RateLimitMiddleware(RequestDelegate next, IOptions<RateLimitOptions> options)
	{
		_next = next;
		_options = options.Value;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		if (!context.Request.Path.StartsWithSegments("/api"))
		{
			await _next(context);
			return;
		}

		var isLogin = context.Request.Path.StartsWithSegments("/api/auth/login");
		var limit = isLogin ? _options.LoginLimit : _options.GlobalLimit;
		var window = TimeSpan.FromSeconds(isLogin ? _options.LoginWindowSeconds : _options.GlobalWindowSeconds);
		var key = ResolveKey(context);
		var now = DateTime.UtcNow;

		Window? entry;
		try
		{
			entry = Counters.AddOrUpdate(key,
				_ => new Window(now, 1),
				(_, existing) =>
				{
					if (now - existing.Start >= window) return new Window(now, 1);
					existing.Count++;
					return existing;
				});
		}
		catch
		{
			// 限流判定失败不阻断主链路
			await _next(context);
			return;
		}

		if (entry.Count > limit)
		{
			context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
			context.Response.Headers["Retry-After"] = ((int)window.TotalSeconds).ToString();
			context.Response.ContentType = "application/json; charset=utf-8";
			try
			{
				await context.Response.WriteAsync(JsonSerializer.Serialize(new ApiError
				{
					Code = ErrorCodes.TooManyRequests,
					Message = "请求过于频繁，请稍后再试。",
				}));
			}
			catch
			{
				// 响应已启动则跳过
			}
			return;
		}

		await _next(context);
	}

	/// <summary>
	/// M0-08 限流键：登录后以 TenantId+UserId 维度（由 AuthMiddleware 写入 Items），
	/// 登录前/匿名以可信客户端 IP + 设备维度；绝不接受客户端伪造的令牌头作为键。
	/// </summary>
	private static string ResolveKey(HttpContext context)
	{
		if (context.Items.TryGetValue("TenantId", out var tidObj) &&
			context.Items.TryGetValue("UserId", out var uidObj) &&
			tidObj is long tid && uidObj is long uid && tid > 0 && uid > 0)
		{
			return $"u:{tid}:{uid}";
		}

		var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
		var ua = context.Request.Headers["User-Agent"].ToString();
		var uaHash = ua.Length == 0 ? "na" : Hex(SHA256.HashData(Encoding.UTF8.GetBytes(ua))[..8]);
		return $"ip:{ip}:{uaHash}";
	}

	private static string Hex(ReadOnlySpan<byte> bytes)
	{
		var sb = new StringBuilder(bytes.Length * 2);
		foreach (var b in bytes) sb.Append(b.ToString("x2"));
		return sb.ToString();
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
