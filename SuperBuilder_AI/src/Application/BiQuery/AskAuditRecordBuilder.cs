using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// Ask 审计记录构建器（M6-05）：从一次 Ask 调用的上下文构造
/// <see cref="AskAuditRecord"/>，落库前对敏感字段脱敏。
/// </summary>
public static class AskAuditRecordBuilder
{
	/// <summary>
	/// 构造一条 Ask 审计记录。
	/// </summary>
	/// <param name="correlationId">关联 Id（每次 Ask 调用生成）。</param>
	/// <param name="turn">会话服务解析出的本轮上下文。</param>
	/// <param name="authorizedDataSourceIds">本轮授权数据源集合。</param>
	/// <param name="response">BI 响应（成功态非空；失败态为 null）。</param>
	/// <param name="errorMessage">失败态错误信息（已脱敏）。</param>
	/// <param name="outcome">审计产出结果。</param>
	/// <param name="durationMs">总耗时（毫秒）。</param>
	/// <param name="segmentTimings">分段耗时（M6-05 指标）。</param>
	/// <param name="redactor">脱敏工具；缺省用 <see cref="AskPiiRedactor.Default"/>。</param>
	public static AskAuditRecord Build(
		string? correlationId,
		AskConversationTurn turn,
		IReadOnlyCollection<long>? authorizedDataSourceIds,
		BIResponse? response,
		string? errorMessage,
		AskAuditOutcome outcome,
		long durationMs,
		AskSegmentTimings? segmentTimings,
		AskPiiRedactor? redactor = null)
	{
		var r = redactor ?? AskPiiRedactor.Default;

		var decision = response?.Explanation?.Decision?.Decision;
		var status = response is null
			? outcome.ToString()
			: $"{response.ConversationStatus}.{response.AskBehavior}";

		var original = r.RedactText(turn.OriginalQuestion ?? turn.StandaloneQuestion);
		var rewritten = r.RedactText(response?.RewrittenQuestion);
		var sqlSummary = response?.Sql is null ? null : Truncate(r.RedactSql(response.Sql), 2000);
		var resultSample = r.RedactResultSample(response?.Data);

		var ds = authorizedDataSourceIds is null
			? new List<long>()
			: authorizedDataSourceIds.ToList();

		return new AskAuditRecord(
			Guid.NewGuid(),
			correlationId,
			turn.ConversationId,
			turn.Clarification?.RepeatCount ?? 0,
			original,
			rewritten,
			ds,
			decision,
			sqlSummary,
			null,
			durationMs,
			null,
			status,
			outcome,
			errorMessage,
			segmentTimings,
			resultSample);
	}

	private static string Truncate(string s, int max)
		=> s.Length <= max ? s : s.Substring(0, max) + "…";
}
