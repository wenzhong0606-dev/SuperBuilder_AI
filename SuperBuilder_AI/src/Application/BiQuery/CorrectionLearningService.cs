using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 自主学习纠错服务：显式纠正落库 + 下次同问句自动回放。
///
/// <para>
/// 匹配键为「归一化问句」（去空白/标点、统一小写）的子串包含关系，同 (TenantId, UserId, Kind, Pattern, DataSourceId)
/// 聚合为一条规则并累加命中次数。严格 tenant+user 隔离，且只在显式纠正时写入。
/// </para>
/// </summary>
public class CorrectionLearningService : ICorrectionLearningService
{
	/// <summary>过短的模式容易误命中（如「查一下」），不予学习。</summary>
	private const int MinPatternLength = 4;

	private readonly SuperBIContext _context;
	private readonly ILogger<CorrectionLearningService>? _logger;

	public CorrectionLearningService(
		SuperBIContext context,
		ILogger<CorrectionLearningService>? logger = null)
	{
		_context = context;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<CorrectionResolution> ResolveAsync(
		long tenantId,
		long? userId,
		string question,
		CancellationToken cancellationToken = default)
	{
		var resolution = new CorrectionResolution();
		if (tenantId <= 0 || userId is null or <= 0 || string.IsNullOrWhiteSpace(question))
			return resolution;

		var normalized = NormalizeQuestion(question);
		if (normalized.Length < MinPatternLength) return resolution;

		var rules = await _context.QueryCorrectionRules
			.AsNoTracking()
			.Where(r => r.TenantId == tenantId && r.UserId == userId)
			.ToListAsync(cancellationToken);

		foreach (var rule in rules)
		{
			if (string.IsNullOrWhiteSpace(rule.TriggerPattern)) continue;
			if (!normalized.Contains(rule.TriggerPattern, StringComparison.Ordinal)) continue;

			resolution.Corrections.Add(new LearnedCorrection
			{
				RuleId = rule.Id,
				Kind = rule.Kind,
				DataSourceId = rule.DataSourceId,
				Payload = DeserializePayload(rule.PayloadJson),
				HitCount = rule.HitCount,
			});
		}

		return resolution;
	}

	/// <inheritdoc />
	public async Task CaptureAsync(
		long tenantId,
		long userId,
		long? dataSourceId,
		string question,
		CorrectionKind kind,
		CorrectionPayload payload,
		CancellationToken cancellationToken = default)
	{
		if (tenantId <= 0 || userId <= 0 || payload is null) return;

		var normalized = NormalizeQuestion(question);
		if (normalized.Length < MinPatternLength) return;

		var payloadJson = JsonSerializer.Serialize(payload);

		try
		{
			var existing = await _context.QueryCorrectionRules
				.FirstOrDefaultAsync(
					r => r.TenantId == tenantId
						&& r.UserId == userId
						&& r.Kind == kind
						&& r.TriggerPattern == normalized
						&& r.DataSourceId == dataSourceId,
					cancellationToken);

			if (existing is null)
			{
				_context.QueryCorrectionRules.Add(new QueryCorrectionRule
				{
					TenantId = tenantId,
					UserId = userId,
					DataSourceId = dataSourceId,
					TriggerPattern = normalized,
					Kind = kind,
					PayloadJson = payloadJson,
					HitCount = 1,
					LastMatchedAt = DateTime.UtcNow,
				});
			}
			else
			{
				existing.PayloadJson = payloadJson;
				existing.HitCount += 1;
				existing.LastMatchedAt = DateTime.UtcNow;
			}

			await _context.SaveChangesAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			// 学习落库失败不阻断主查询：纠正本次仍已生效（合成问题路径）。
			_logger?.LogWarning(ex, "纠错规则落库失败。Kind={Kind}", kind);
		}
	}

	/// <inheritdoc />
	public string ComposeLearnedQuestion(string question, CorrectionResolution resolution)
	{
		var table = resolution?.TableOverride;
		if (string.IsNullOrWhiteSpace(table)) return question;

		// 已含锁表句式则不重复拼接（避免多轮累积）。
		if (question.Contains("查询物理表必须使用", StringComparison.OrdinalIgnoreCase))
			return question;

		return string.Join(
			"；",
			question,
			$"保持原查询意图，查询物理表必须使用 {table}，不要再使用上一轮选错的表");
	}

	/// <inheritdoc />
	public async Task MarkMatchedAsync(
		CorrectionResolution resolution,
		CancellationToken cancellationToken = default)
	{
		if (resolution is null || !resolution.Any) return;

		try
		{
			var ids = resolution.Corrections.Select(c => c.RuleId).Distinct().ToList();
			var rules = await _context.QueryCorrectionRules
				.Where(r => ids.Contains(r.Id))
				.ToListAsync(cancellationToken);

			foreach (var rule in rules)
			{
				rule.HitCount += 1;
				rule.LastMatchedAt = DateTime.UtcNow;
			}

			await _context.SaveChangesAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger?.LogWarning(ex, "纠错规则命中统计回写失败。");
		}
	}

	/// <summary>问句归一化：去空白、去标点符号、统一小写（中英文共存安全）。</summary>
	public static string NormalizeQuestion(string? question)
	{
		if (string.IsNullOrWhiteSpace(question)) return string.Empty;

		var builder = new StringBuilder(question.Length);
		foreach (var ch in question)
		{
			if (char.IsWhiteSpace(ch)) continue;
			if (char.IsPunctuation(ch) || char.IsSymbol(ch)) continue;
			builder.Append(char.ToLowerInvariant(ch));
		}

		return builder.ToString();
	}

	private static CorrectionPayload DeserializePayload(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return new CorrectionPayload();
		try
		{
			return JsonSerializer.Deserialize<CorrectionPayload>(
				json,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
				?? new CorrectionPayload();
		}
		catch (JsonException)
		{
			return new CorrectionPayload();
		}
	}
}
