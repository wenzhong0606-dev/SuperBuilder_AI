using System.Text.Json;
using Microsoft.JSInterop;
using SuperBuilder_AI.Components.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// Ask 会话持久化：把对话轮次（<see cref="AskTurn"/> 列表）序列化进 localStorage，
/// 刷新/重进页面后可恢复上下文（S3-2）。按租户 + 用户隔离，保留最近 <see cref="MaxTurns"/> 轮。
/// 全程容错——持久化失败只记录、不阻断问数。
/// </summary>
public sealed class AskSessionStore
{
	private readonly IJSRuntime _js;

	public AskSessionStore(IJSRuntime js) => _js = js;

	private const int MaxTurns = 50;

	// 字段（Question/Response/Error/...）也需序列化，故开启 IncludeFields。
	private static readonly JsonSerializerOptions Opt = new()
	{
		IncludeFields = true,
		PropertyNameCaseInsensitive = true,
		DefaultBufferSize = 1024
	};

	private static string Key(long tenantId, long userId) => $"ask-session:{tenantId}:{userId}";

	/// <summary>保存最近 N 轮（截取尾部，避免 localStorage 超出配额）。</summary>
	public async Task SaveAsync(long tenantId, long userId, List<AskTurn> turns)
	{
		try
		{
			if (turns.Count == 0) { await ClearAsync(tenantId, userId); return; }
			var slice = turns.Count > MaxTurns ? turns.Skip(turns.Count - MaxTurns).ToList() : turns;
			var json = JsonSerializer.Serialize(slice, Opt);
			await _js.InvokeVoidAsync("SuperBuilder.setLocalJson", Key(tenantId, userId), json);
		}
		catch
		{
			// 持久化失败不应阻断问数主流程
		}
	}

	/// <summary>恢复已保存的轮次；失败或无记录返回空列表。</summary>
	public async Task<List<AskTurn>> LoadAsync(long tenantId, long userId)
	{
		try
		{
			var json = await _js.InvokeAsync<string?>("SuperBuilder.getLocalJson", Key(tenantId, userId));
			if (string.IsNullOrWhiteSpace(json)) return new List<AskTurn>();
			var list = JsonSerializer.Deserialize<List<AskTurn>>(json, Opt);
			return list ?? new List<AskTurn>();
		}
		catch
		{
			return new List<AskTurn>();
		}
	}

	public async Task ClearAsync(long tenantId, long userId)
	{
		try { await _js.InvokeVoidAsync("SuperBuilder.removeLocal", Key(tenantId, userId)); }
		catch { /* ignore */ }
	}
}
