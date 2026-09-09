using System.Text.Json;

namespace SuperBuilder_AI.Components.Models;

/// <summary>
/// 自然语言问数响应（对齐后端 <c>BIResponse</c>）。
/// 行数据 <see cref="QueryResult.Rows"/> 的值在 JSON 反序列化后为 <see cref="JsonElement"/>，
/// 由 <see cref="JsonValue"/> 辅助方法安全转为展示/数值类型。
/// </summary>
public sealed class BIResponse
{
    public string? TurnId { get; set; }
	public string? ConversationId { get; set; }
	public string ConversationStatus { get; set; } = "Completed";
	public string? RewrittenQuestion { get; set; }
	/// <summary>单轮行为分类（M6-03）：NewQuestion / Clarification / Correction / Confirmation / Cancel（字符串副本，与后端枚举值一致）。</summary>
	public string AskBehavior { get; set; } = "NewQuestion";
	/// <summary>结构化澄清详情（M6-03，对齐后端 ClarificationDetail）。</summary>
	public ClarificationDetail? Clarification { get; set; }
	public bool Success { get; set; }
	public string? Question { get; set; }
	public string? Sql { get; set; }
	public QueryResult? Data { get; set; }
	public QueryAnswer? Answer { get; set; }
	public string? ErrorMessage { get; set; }
	/// <summary>可解释性信息（QueryPlan 链路），前端不渲染，保留以便调试。</summary>
	public object? Explanation { get; set; }
}

/// <summary>查询执行结果（对齐后端 <c>QueryResult</c>）。</summary>
public sealed class QueryResult
{
	public bool Success { get; set; }
	/// <summary>每行是一个字段名→值的字典；值类型在反序列化后多为 <see cref="JsonElement"/>。</summary>
	public List<Dictionary<string, object?>> Rows { get; set; } = new();
	public int Count => Rows.Count;
	public string? ErrorMessage { get; set; }
}

/// <summary>AI 对查询结果的回答（对齐后端 <c>QueryAnswer</c>）。</summary>
public sealed class QueryAnswer
{
	public bool Success { get; set; }
	public string Question { get; set; } = "";
	/// <summary>AI 自然语言回答。</summary>
	public string Answer { get; set; } = "";
	/// <summary>数据摘要键值对（用于 KPI 卡）。</summary>
	public Dictionary<string, object?> Summary { get; set; } = new();
	/// <summary>图表建议（驱动 ChartView 渲染）。</summary>
	public List<VisualizationSuggestion> Visualizations { get; set; } = new();
	public long ElapsedMilliseconds { get; set; }
	public string? ErrorMessage { get; set; }
}

/// <summary>图表建议（对齐后端 <c>VisualizationSuggestion</c>）。</summary>
public sealed class VisualizationSuggestion
{
	/// <summary>bar / line / pie / table。</summary>
	public string Type { get; set; } = "";
	public string Title { get; set; } = "";
	public string? XAxis { get; set; }
	public List<string> YAxis { get; set; } = new();
	public string? Reason { get; set; }
}

/// <summary>统一错误响应（对齐后端 <c>ApiError</c>：code / message / traceId / details）。</summary>
public sealed class ApiError
{
	public string? Code { get; set; }
	public string? Message { get; set; }
	public string? TraceId { get; set; }
	public string? Details { get; set; }
}

/// <summary>结构化澄清详情（对齐后端 <c>ClarificationDetail</c>）。</summary>
public sealed class ClarificationDetail
{
	public string? OriginalQuestion { get; set; }
	public string? PendingSlot { get; set; }
	public List<string> CandidateEntities { get; set; } = new();
	public List<string> CandidateMetrics { get; set; } = new();
	public List<string> CandidateDimensions { get; set; } = new();
	public List<string> CandidateTime { get; set; } = new();
	public List<long> AuthorizedDataSourceIds { get; set; } = new();
	public bool LoopDetected { get; set; }
	public int RepeatCount { get; set; }
}

/// <summary>问数结果封装：区分「成功响应」与「传输/服务端错误（统一错误码）」。</summary>
public sealed class AskOutcome
{
	public BIResponse? Response { get; init; }
	public string? Error { get; init; }
	public string? Code { get; init; }
	public string? TraceId { get; init; }
	public bool HasResponse => Response is not null;
}

/// <summary>JSON 值辅助：把 <see cref="JsonElement"/>（或原始类型）安全转为展示/数值形式。</summary>
public static class JsonValue
{
	public static string AsString(object? v)
	{
		if (v is null) return "";
		if (v is JsonElement je)
		{
			return je.ValueKind switch
			{
				JsonValueKind.String => je.GetString() ?? "",
				JsonValueKind.Null or JsonValueKind.Undefined => "",
				_ => je.ToString()
			};
		}
		return v.ToString() ?? "";
	}

	public static double? AsNumber(object? v)
	{
		if (v is null) return null;
		if (v is JsonElement je)
		{
			if (je.ValueKind == JsonValueKind.Number)
				return je.TryGetDouble(out var d) ? d : null;
			if (je.ValueKind == JsonValueKind.String && double.TryParse(je.GetString(), out var ds))
				return ds;
			return null;
		}
		if (v is IConvertible)
		{
			try { return Convert.ToDouble(v); } catch { return null; }
		}
		return null;
	}

	public static bool IsNumber(object? v)
	{
		if (v is JsonElement je) return je.ValueKind == JsonValueKind.Number;
		return v is int or long or double or decimal or float;
	}
}
