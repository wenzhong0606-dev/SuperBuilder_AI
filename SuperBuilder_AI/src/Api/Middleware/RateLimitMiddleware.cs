using System;
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
///
/// <para><b>RL-1/RL-2 修复</b>：计数不再存放于中间件的 <c>static</c> 字典（原实现永不清理 → 内存泄漏），
/// 改由 <see cref="IRateLimitStore"/> 承载；默认 <see cref="MemoryRateLimitStore"/> 会定期逐出过期窗口，
/// 且计数结构不可变（更新委托无副作用，避免并发计数失真）。
/// 注意该默认实现为进程内存、<b>仅单实例有效</b>——多实例部署前须替换为分布式存储实现。</para>
/// </summary>
public sealed class RateLimitMiddleware
{
	private readonly RequestDelegate _next;
	private readonly RateLimitOptions _options;
	private readonly IRateLimitStore _store;

	/// <param name="store">
	/// 限流存储（RL-1：定期清理过期条目，杜绝内存泄漏；RL-2：可替换为分布式实现以支持多实例）。
	/// </param>
	public RateLimitMiddleware(RequestDelegate next, IOptions<RateLimitOptions> options, IRateLimitStore store)
	{
		_next = next;
		_options = options.Value;
		_store = store;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		// AUTH-1：大小写不敏感，与 ASP.NET 路由语义对齐，避免 /API/** 变体绕过限流。
		if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
		{
			await _next(context);
			return;
		}

		var isLogin = context.Request.Path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase);
		var limit = isLogin ? _options.LoginLimit : _options.GlobalLimit;
		var window = TimeSpan.FromSeconds(isLogin ? _options.LoginWindowSeconds : _options.GlobalWindowSeconds);
		var key = ResolveKey(context);
		var now = DateTime.UtcNow;

		RateLimitCounter entry;
		try
		{
			// RL-1/RL-2：经由可清理、可替换的存储实现计数（原子操作且更新委托无副作用）。
			entry = _store.Increment(key, window, now);
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

}
