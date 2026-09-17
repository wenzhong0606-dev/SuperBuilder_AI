using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// QueryPlan Confidence 的可解释证据。
/// 
/// Phase 2.4
/// 
/// 该对象只描述：
///
/// “为什么这个 QueryPlan 得到这样的 Confidence。”
///
/// 不负责计算 Confidence。
/// 不负责 Decision。
/// </summary>
public sealed class QueryPlanConfidenceEvidence
{
    // ============================================================
    // Semantic Evidence
    // ============================================================

    /// <summary>
    /// 整体 Metadata Semantic Match 分数。
    /// 范围建议为 0~1。
    /// </summary>
    public double SemanticMatchScore { get; set; }

    /// <summary>
    /// Table Match 分数。
    /// </summary>
    public double TableMatchScore { get; set; }

    /// <summary>
    /// Field Match 分数。
    /// </summary>
    public double FieldMatchScore { get; set; }

    /// <summary>
    /// Metric Match 分数。
    /// </summary>
    public double MetricMatchScore { get; set; }

    /// <summary>
    /// Dimension Match 分数。
    /// </summary>
    public double DimensionMatchScore { get; set; }

    /// <summary>
    /// Filter Match 分数。
    /// </summary>
    public double FilterMatchScore { get; set; }


    // ============================================================
    // Candidate Ranking Evidence
    // ============================================================

    /// <summary>
    /// 当前 QueryPlan 相关 Semantic Candidate 的最高评分。
    /// </summary>
    public double CandidateRankingScore { get; set; }

    /// <summary>
    /// Top Candidate 与第二候选之间的分数差。
    /// </summary>
    public double CandidateRankingGap { get; set; }


    // ============================================================
    // Validation Evidence
    // ============================================================

    /// <summary>
    /// Validation 综合评分。
    /// 范围建议为 0~1。
    /// </summary>
    public double ValidationScore { get; set; }

    /// <summary>
    /// Validation Error 数量。
    /// </summary>
    public int ValidationErrorCount { get; set; }

    /// <summary>
    /// Validation Warning 数量。
    /// </summary>
    public int ValidationWarningCount { get; set; }


    // ============================================================
    // Repair Evidence
    // ============================================================

    /// <summary>
    /// 实际执行的 Repair 次数。
    /// </summary>
    public int RepairCount { get; set; }

    /// <summary>
    /// QueryPlan 实际发生变化的次数。
    /// </summary>
    public int ChangedPlanCount { get; set; }

	/// <summary>
	/// Repair Progress 评分。
	///
	/// 用于描述 Repair 是否持续改善 QueryPlan。
	///
	/// 范围：0~1。
	///
	/// 1.0  = Repair Progress 良好
	/// 0.5  = Repair Progress 一般
	/// 0.0  = Repair 没有产生有效改善
	/// </summary>
	public double RepairProgressScore { get; set; }

	/// <summary>
	/// Repair 是否产生了实际的 QueryPlan 改善。
	/// </summary>
	public bool RepairProgressAvailable { get; set; }

	/// <summary>
	/// Repair 是否发生 Stall。
	/// </summary>
	public bool RepairStalled { get; set; }

    /// <summary>
    /// Repair 是否检测到 Loop。
    /// </summary>
    public bool RepairLoopDetected { get; set; }

    /// <summary>
    /// Repair 是否失败。
    /// </summary>
    public bool RepairFailed { get; set; }

    /// <summary>
    /// 是否达到最大 Repair 次数。
    /// </summary>
    public bool MaxRepairAttemptsReached { get; set; }

    /// <summary>
    /// 当前 Repair Trace 最终状态。
    /// </summary>
    public QueryPlanRepairTraceStatus RepairStatus { get; set; }

    /// <summary>
    /// 用户是否通过多轮纠正显式指定了目标物理表且该表已被采纳为主表。
    /// 命中时作为强正证据，把置信度保底至 Medium（明细查询可直接进入 SQL Builder），
    /// 避免“用户已明确纠正”的查询被误判为 Low 而阻断。
    /// 仅在纠正短语 + 计划主表名同时出现于问题时为真，普通查询恒为 false。
    /// </summary>
    public bool TableCorrectionHonored { get; set; }

    /// <summary>
    /// 本次查询是否回放了自动化学习规则（用户历史显式纠正）。
    ///
    /// 命中时作为正证据，把置信度保底至 Medium —— 与 <see cref="TableCorrectionHonored"/>
    /// 同待遇。区别在于：本字段由调用方在 Step 0.5 命中学习规则后显式传入，
    /// 不依赖问题文本推断，因此可以区分「用户当轮纠正」与「系统历史规则回放」，
    /// 也可覆盖值映射 / 外键 / 列展示等不含锁表句式的规则类型。
    /// 未命中学习规则时恒为 false。
    /// </summary>
    public bool LearningApplied { get; set; }

    /// <summary>
    /// 本次命中的学习规则条数（未去重），用于审计与计量。
    /// </summary>
    public int LearningRuleCount { get; set; }

    /// <summary>
    /// 本次命中的学习规则类型（已去重）。空集合表示未命中任何学习规则。
    /// </summary>
    public IReadOnlyList<CorrectionKind> LearningRuleKinds { get; set; } =
        Array.Empty<CorrectionKind>();

    /// <summary>
    /// 命中的学习规则里是否包含表级覆盖（<see cref="CorrectionKind.TableOverride"/>）。
    /// 为真表示本次计划主表由学习规则锁定，而非完全由语义检索得出。
    /// </summary>
    public bool LearningTableOverrideApplied { get; set; }


    // ============================================================
    // Evidence Availability
    // ============================================================

    /// <summary>
    /// Semantic Evidence 是否真实可用。
    /// </summary>
    public bool SemanticEvidenceAvailable { get; set; }

    /// <summary>
    /// Table Evidence 是否真实可用。
    /// </summary>
    public bool TableEvidenceAvailable { get; set; }

    /// <summary>
    /// Field Evidence 是否真实可用。
    /// </summary>
    public bool FieldEvidenceAvailable { get; set; }

    /// <summary>
    /// Metric Evidence 是否真实可用。
    /// </summary>
    public bool MetricEvidenceAvailable { get; set; }

    /// <summary>
    /// Dimension Evidence 是否真实可用。
    /// </summary>
    public bool DimensionEvidenceAvailable { get; set; }

    /// <summary>
    /// Filter Evidence 是否真实可用。
    /// </summary>
    public bool FilterEvidenceAvailable { get; set; }
}