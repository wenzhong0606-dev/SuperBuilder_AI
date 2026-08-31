using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Api.Caching;

/// <summary>
/// 基于 <see cref="IMemoryCache"/> 的 Ask 响应缓存实现（P11.5.1）。
///
/// <para>
/// 设计要点：
/// <list type="bullet">
/// <item><b>键归一化</b>：去首尾空白、折叠连续空白、去尾部中英文标点、大小写不敏感。
/// 使「华东地区的销售额？」「华东地区 的销售额」等字面差异命中同一条目。</item>
/// <item><b>键隔离</b>：<c>tenantId</c> + <c>dataSourceId</c> 参与键构成，跨租户/跨数据源绝不串数据。</item>
/// <item><b>容量控制</b>：<see cref="MemoryCacheOptions.SizeLimit"/> 复用 <see cref="AskCacheOptions.MaxEntries"/>，
/// 每条计 1，超出由 MemoryCache 按 LRU 压缩。</item>
/// <item><b>异常静默</b>：缓存读写失败一律降级为「未命中」，绝不影响主查询链路。</item>
/// </list>
/// </para>
///
/// <para>
/// 说明：本实现是「精确键 + 归一化」缓存，不是向量近似缓存。
/// 语义相似（不同字面）问句暂不复用缓存——近似命中存在把错误答案张冠李戴的风险，
/// 收益/风险比不如精确匹配；后续如需，可在命中失败后再接一层 embedding 近邻召回并加严格守门。
/// </para>
/// </summary>
public sealed class MemoryAskResponseCache : IAskResponseCache
{
	private readonly IMemoryCache _cache;
	private readonly AskCacheOptions _options;
	private long _hits;
	private long _misses;

	public MemoryAskResponseCache(IMemoryCache cache, IOptions<AskCacheOptions> options)
	{
		_cache = cache;
		_options = options?.Value ?? new AskCacheOptions();
	}

	/// <inheritdoc />
	public BIResponse? Get(long tenantId, string question, long dataSourceId)
	{
		if (!IsEnabled()) { Interlocked.Increment(ref _misses); return null; }

		try
		{
			var key = BuildKey(tenantId, question, dataSourceId);
			if (key is null) { Interlocked.Increment(ref _misses); return null; }

			if (_cache.TryGetValue(key, out BIResponse? cached) && cached is not null)
			{
				Interlocked.Increment(ref _hits);
				return cached;
			}
		}
		catch
		{
			// 缓存故障降级为未命中
		}

		Interlocked.Increment(ref _misses);
		return null;
	}

	/// <inheritdoc />
	public void Set(long tenantId, string question, long dataSourceId, BIResponse response)
	{
		if (!IsEnabled()) return;

		try
		{
			// 只缓存成功响应：失败/被闸门阻断的结果不入库，避免瞬时故障被钉死
			if (response is not { Success: true }) return;

			var key = BuildKey(tenantId, question, dataSourceId);
			if (key is null) return;

			var ttl = TimeSpan.FromSeconds(Math.Max(1, _options.TtlSeconds));
			_cache.Set(key, response, new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = ttl,
				Size = 1
			});
		}
		catch
		{
			// 写入失败不影响主链路
		}
	}

	/// <inheritdoc />
	public (long Hits, long Misses) Snapshot() =>
		(Interlocked.Read(ref _hits), Interlocked.Read(ref _misses));

	/// <inheritdoc />
	public void Clear()
	{
		try
		{
			if (_cache is MemoryCache concrete) concrete.Compact(1.0);
		}
		catch
		{
			// 清空失败可忽略
		}
	}

	private bool IsEnabled() => _options.Enabled && _options.TtlSeconds > 0;

	private static string? BuildKey(long tenantId, string question, long dataSourceId)
	{
		var normalized = Normalize(question);
		if (string.IsNullOrEmpty(normalized)) return null;

		var hash = Sha256Hex(normalized);
		return string.Concat("ask:", tenantId.ToString(CultureInfo.InvariantCulture),
			":", dataSourceId.ToString(CultureInfo.InvariantCulture), ":", hash);
	}

	/// <summary>
	/// 问题归一化：折叠空白 → 去尾部中英文标点 → 大小写不敏感。
	/// 目的只是消除「无关紧要的字面差异」，不做同义改写（那属语义层职责）。
	/// </summary>
	internal static string Normalize(string? question)
	{
		if (string.IsNullOrWhiteSpace(question)) return string.Empty;

		var sb = new StringBuilder(question.Length);
		var pendingSpace = false;

		foreach (var ch in question)
		{
			if (char.IsWhiteSpace(ch))
			{
				pendingSpace = sb.Length > 0;
				continue;
			}

			if (pendingSpace)
			{
				sb.Append(' ');
				pendingSpace = false;
			}

			sb.Append(ch);
		}

		var text = sb.ToString().Trim();

		// 反复去掉尾部标点（含中英文问号/句号/感叹号/分号/逗号）
		int cut;
		do
		{
			cut = text.Length;
			if (cut == 0) break;

			var last = text[cut - 1];
			if (last is '?' or '？' or '!' or '！' or '。' or '.' or '；' or ';' or '，' or ',')
				text = text.Substring(0, cut - 1).TrimEnd();
		} while (text.Length != cut);

		return text.ToLowerInvariant();
	}

	private static string Sha256Hex(string input)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return Convert.ToHexString(bytes);
	}
}
