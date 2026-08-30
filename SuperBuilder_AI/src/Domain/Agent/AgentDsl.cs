namespace SuperBuilder_AI.Models.Agent;

/// <summary>Agent 计划状态常量（P9 AI Agent / Copilot）。</summary>
public static class AgentStatuses
{
	/// <summary>草稿：可编辑，不对外发布。</summary>
	public const string Draft = "draft";

	/// <summary>已发布：可被访问与分享。</summary>
	public const string Published = "published";

	/// <summary>已归档：保留数据但不可编辑。</summary>
	public const string Archived = "archived";

	public static IReadOnlyList<string> Supported { get; } =
		new[] { Draft, Published, Archived };
}

/// <summary>Agent DSL 版本号常量（P9）。</summary>
public static class AgentDslVersions
{
	/// <summary>首个正式版本。</summary>
	public const string V1 = "1.0";

	/// <summary>当前版本（新增 DSL 时以此为默认）。</summary>
	public const string Current = V1;

	/// <summary>受支持的版本集合（反序列化白名单）。</summary>
	public static IReadOnlyList<string> Supported { get; } = new[] { V1 };
}

/// <summary>Agent 可调用的工具类型常量（P9）。集中定义，避免魔法字符串散落。</summary>
public static class AgentTools
{
	/// <summary>元数据：探查表/字段/语义映射。</summary>
	public const string Metadata = "metadata";

	/// <summary>语义：业务术语/同义词解析。</summary>
	public const string Semantic = "semantic";

	/// <summary>查询：指标计算与取数。</summary>
	public const string Query = "query";

	/// <summary>看板：可视化编排。</summary>
	public const string Dashboard = "dashboard";

	/// <summary>报表：固定格式报表导出。</summary>
	public const string Report = "report";

	/// <summary>预测：趋势预测与What-If。</summary>
	public const string Forecast = "forecast";

	/// <summary>告警：阈值监控与通知。</summary>
	public const string Alert = "alert";

	/// <summary>流程：跨系统工作流编排。</summary>
	public const string Workflow = "workflow";

	/// <summary>受支持的工具集合（反序列化白名单）。</summary>
	public static IReadOnlyList<string> Supported { get; } =
		new[] { Metadata, Semantic, Query, Dashboard, Report, Forecast, Alert, Workflow };
}

/// <summary>异常原因分析的下钻维度常量（P9）。</summary>
public static class AnalysisDimensions
{
	/// <summary>时间维度。</summary>
	public const string Time = "time";

	/// <summary>区域维度。</summary>
	public const string Region = "region";

	/// <summary>客户维度。</summary>
	public const string Customer = "customer";

	/// <summary>产品维度。</summary>
	public const string Product = "product";

	/// <summary>渠道维度。</summary>
	public const string Channel = "channel";

	/// <summary>受支持的维度集合（反序列化白名单）。</summary>
	public static IReadOnlyList<string> Supported { get; } =
		new[] { Time, Region, Customer, Product, Channel };
}

/// <summary>异常原因分析的方向常量（P9）：同比 / 环比 / 下钻。</summary>
public static class AnalysisDirections
{
	/// <summary>同比：与去年同期对比。</summary>
	public const string YoY = "yoy";

	/// <summary>环比：与上一周期对比。</summary>
	public const string MoM = "mom";

	/// <summary>下钻：沿某维度逐层钻取。</summary>
	public const string Drill = "drill";

	/// <summary>受支持的方向集合（反序列化白名单）。</summary>
	public static IReadOnlyList<string> Supported { get; } = new[] { YoY, MoM, Drill };
}

/// <summary>
/// Agent DSL 根对象（P9 AI Agent / Copilot）。
///
/// 一个 <see cref="AgentDsl"/> 即一份完整的、可序列化的 Agent 执行计划：
/// 描述<strong>选了哪些工具</strong>、<strong>以什么顺序</strong>、<strong>异常原因分析链路</strong>如何展开。
///
/// <para>设计红线：底层不存裸 HTML。所有意图由结构化字段表达，由后续执行器解释。
/// 同一份 DSL 可被 AI 生成与修改（P9.2），亦可被 <see cref="AgentDslSerializer"/> 校验后持久化（P9.1）。</para>
/// </summary>
public sealed class AgentDsl
{
	/// <summary>DSL 版本号，默认 <see cref="AgentDslVersions.Current"/>。</summary>
	public string Version { get; set; } = AgentDslVersions.Current;

	/// <summary>业务编码（同租户内唯一，便于 API 定位与 AI 引用）。</summary>
	public string? Code { get; set; }

	/// <summary>计划名称。</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>计划描述。</summary>
	public string? Description { get; set; }

	/// <summary>原始用户请求（自然语言或结构化意图），用于审计与可解释性。</summary>
	public string? UserRequest { get; set; }

	/// <summary>选中的工具序列（按 <see cref="AgentToolSelection.Order"/> 升序执行）。</summary>
	public List<AgentToolSelection> SelectedTools { get; set; } = new();

	/// <summary>异常原因分析链路（销售额→同比→环比→区域→客户→产品→渠道）。可为空。</summary>
	public List<AnalysisStep> AnomalyChain { get; set; } = new();
}

/// <summary>工具选择项（P9）。描述一次具体的工具调用意图。</summary>
public sealed class AgentToolSelection
{
	/// <summary>工具类型，取值见 <see cref="AgentTools"/>。</summary>
	public string Tool { get; set; } = string.Empty;

	/// <summary>执行顺序（从 1 开始，小者优先）。</summary>
	public int Order { get; set; }

	/// <summary>选择该工具的理由（可解释性）。</summary>
	public string? Reason { get; set; }

	/// <summary>工具所需的参数（键值对，具体取值由执行器在运行时填充/解释）。</summary>
	public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>异常原因分析的一步（P9）：沿某一维度以某一方向下钻，使用某一工具。</summary>
public sealed class AnalysisStep
{
	/// <summary>步骤顺序（从 1 开始，小者优先）。</summary>
	public int Order { get; set; }

	/// <summary>下钻维度，取值见 <see cref="AnalysisDimensions"/>。</summary>
	public string Dimension { get; set; } = string.Empty;

	/// <summary>分析方向，取值见 <see cref="AnalysisDirections"/>。</summary>
	public string Direction { get; set; } = string.Empty;

	/// <summary>使用的工具，取值见 <see cref="AgentTools"/>。</summary>
	public string Tool { get; set; } = string.Empty;

	/// <summary>该步骤的人类可读说明。</summary>
	public string? Description { get; set; }
}
