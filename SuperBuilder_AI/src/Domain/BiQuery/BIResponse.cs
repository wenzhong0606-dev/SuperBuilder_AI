using System.Text.Json.Serialization;

namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// Ask 会话状态稳定枚举（M6-02 输入与状态契约）。
/// 以字符串形式序列化（JsonStringEnumConverter），与历史 wire 值
/// "AwaitingClarification" / "Completed" 保持兼容，前端（独立 string 副本）无需改动。
/// </summary>
public enum ConversationStatus
{
	AwaitingClarification,
	Completed,
	Failed,
	Expired,
	Cancelled,
}

/// <summary>
/// Ask 单轮行为分类（M6-03 澄清与语义学习）。
/// 以字符串形式序列化（JsonStringEnumConverter），与历史 wire 值保持兼容，前端（独立 string 副本）无需改动。
/// </summary>
public enum AskBehavior
{
	/// <summary>首问或全新问题（无待澄清上下文）。</summary>
	NewQuestion,
	/// <summary>在待澄清会话中补充消歧信息。</summary>
	Clarification,
	/// <summary>纠正上一轮结果（如「改成按月统计」「不对，应该是华东」）。</summary>
	Correction,
	/// <summary>实体/术语别名确认（如「入库单就是入库凭证」）。</summary>
	Confirmation,
	/// <summary>取消/清空当前澄清会话。</summary>
	Cancel,
}

/// <summary>
/// 结构化澄清详情（M6-03）：待澄清会话必须保存原问题、候选实体、Limit/Order/Filter、授权数据源与待确认槽位，
/// 不能只保存拼接后的自然语言。循环检测用于避免对同一计划/槽位/候选集无限返回同一种 Medium。
/// </summary>
public sealed class ClarificationDetail
{
	/// <summary>原始问题（未被改写）。</summary>
	public string? OriginalQuestion { get; set; }

	/// <summary>缺失的待确认槽位（人类可读，如「业务实体：入库单 / 入库凭证 歧义」）。</summary>
	public string? PendingSlot { get; set; }

	/// <summary>候选实体（表/业务对象）名称。</summary>
	public List<string> CandidateEntities { get; set; } = new();

	/// <summary>候选指标（度量）名称。</summary>
	public List<string> CandidateMetrics { get; set; } = new();

	/// <summary>候选维度（分组/切片）名称。</summary>
	public List<string> CandidateDimensions { get; set; } = new();

	/// <summary>候选时间范围/粒度。</summary>
	public List<string> CandidateTime { get; set; } = new();

	/// <summary>本轮授权数据源集合（用于限定候选范围）。</summary>
	public List<long> AuthorizedDataSourceIds { get; set; } = new();

	/// <summary>循环检测命中：对同一计划/槽位/候选集连续澄清超过阈值，避免无限返回同一种 Medium。</summary>
	public bool LoopDetected { get; set; }

	/// <summary>本会话已发生的澄清轮数（用于循环检测与前端提示）。</summary>
	public int RepeatCount { get; set; }
}

/// <summary>
/// BI 对话响应。
/// </summary>
public sealed class BIResponse
{
	public string? ConversationId { get; set; }

	[JsonConverter(typeof(JsonStringEnumConverter))]
	public ConversationStatus ConversationStatus { get; set; } = ConversationStatus.Completed;
	public string? RewrittenQuestion { get; set; }

	/// <summary>
	/// 单轮行为分类（M6-03）：NewQuestion / Clarification / Correction / Confirmation / Cancel。
	/// 默认首问；由 AskController 依据会话服务解析结果写入。
	/// </summary>
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public AskBehavior AskBehavior { get; set; } = AskBehavior.NewQuestion;

	/// <summary>
	/// 结构化澄清详情（M6-03）：仅在待澄清/确认/纠正等行为下填充，供前端渲染候选与缺失槽位。
	/// </summary>
	public ClarificationDetail? Clarification { get; set; }
	/// <summary>
	/// 是否成功。
	/// </summary>
	public bool Success
	{
		get;
		set;
	}

	/// <summary>
	/// 用户原始问题。
	/// </summary>
	public string? Question
	{
		get;
		set;
	}

	/// <summary>
	/// 最终生成的 SQL。
	/// </summary>
	public string? Sql
	{
		get;
		set;
	}

	/// <summary>
	/// 查询返回的数据。
	/// </summary>
	public QueryResult? Data
	{
		get;
		set;
	}

	/// <summary>
	/// AI 对查询结果的自然语言回答。
	/// </summary>
	public QueryAnswer? Answer
	{
		get;
		set;
	}

	/// <summary>
	/// 错误信息。
	/// </summary>
	public string? ErrorMessage
	{
		get;
		set;
	}

	/// <summary>
	/// QueryPlan Explainability。
	///
	/// 包含：
	/// - QueryPlan
	/// - Validation
	/// - RepairTrace
	/// - Confidence
	/// - Decision
	/// - Explanation Summary
	///
	/// 即使 QueryPlan 最终被 Decision Gate 拒绝，
	/// Explanation 仍然可以返回给上层进行诊断和解释。
	/// </summary>
	public QueryPlanExplanation? Explanation
	{
		get;
		set;
	}

	/// <summary>
	/// 本次 Ask 调用总耗时（毫秒，M6-05 指标）。由 <c>BIConversationService.ExecuteAsync</c> 填充；
	/// 为新增加法字段，不改变既有 wire 结构，前端（独立 string 副本）可忽略。
	/// </summary>
	public long DurationMs { get; set; }

	/// <summary>
	/// 本次 Ask 调用内分段耗时（M6-05 指标：LLM/Metadata/Plan/SQL/DB/Repair）。
	/// 为新增加法字段，不改变既有 wire 结构。
	/// </summary>
	public AskSegmentTimings? SegmentTimings { get; set; }
}
