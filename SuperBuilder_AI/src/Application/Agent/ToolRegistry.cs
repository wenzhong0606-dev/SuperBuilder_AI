using SuperBuilder_AI.Models.Agent;

namespace SuperBuilder_AI.Services.Agent;

/// <summary>
/// Agent 工具注册表（P9 AI Agent / Copilot）。
///
/// <para>职责：</para>
/// <list type="bullet">
/// <item><see cref="GetAll"/> / <see cref="Get"/>：维护可用工具目录（名称、描述、分类、是否依赖业务实体）。</item>
/// <item><see cref="ResolveFromIntent"/>：<strong>确定性</strong>地把用户意图（自然语言/关键词）映射到一组有序工具选择——不调用 LLM、零回归。</item>
/// <item><see cref="BuildAnomalyChain"/>：构建异常原因分析链路（销售额→同比→环比→区域→客户→产品→渠道），纯模板化、不调 LLM。</item>
/// </list>
///
/// <para>本类为静态目录服务，无状态、可单测；LLM 增强版的工具选择见 P9.2 <c>AgentPlanner</c>。</para>
/// </summary>
public static class ToolRegistry
{
	/// <summary>工具分类。</summary>
	private const string CategoryGovernance = "governance";
	private const string CategorySemantic = "semantic";
	private const string CategoryAnalytics = "analytics";
	private const string CategoryVisual = "visual";
	private const string CategoryPlanning = "planning";
	private const string CategoryMonitor = "monitor";
	private const string CategoryAutomation = "automation";

	/// <summary>后端连接状态：已接真实后端。</summary>
	private const string BackendLive = "live";

	/// <summary>后端连接状态：尚为诚实受控信封（未接真实后端）。</summary>
	private const string BackendPending = "pending";

	/// <summary>工具目录（有序，供编辑器/蓝图展示）。</summary>
	private static readonly IReadOnlyList<AgentToolDescriptor> Catalog = new[]
	{
		new AgentToolDescriptor(AgentTools.Metadata, "元数据探查", "探查业务数据库中的表、字段与语义映射。", CategoryGovernance, false, BackendLive, "M7-04"),
		new AgentToolDescriptor(AgentTools.Semantic, "语义解析", "解析业务术语、同义词与指标口径。", CategorySemantic, true, BackendLive, "M7-04"),
		new AgentToolDescriptor(AgentTools.Query, "指标查询", "计算指标、聚合与取数。", CategoryAnalytics, true, BackendPending, "M7-05~M7-08"),
		new AgentToolDescriptor(AgentTools.Dashboard, "看板编排", "将查询结果编排为可视化看板。", CategoryVisual, true, BackendPending, "M7-05~M7-08"),
		new AgentToolDescriptor(AgentTools.Report, "报表导出", "生成固定格式报表并导出。", CategoryVisual, true, BackendPending, "M7-05~M7-08"),
		new AgentToolDescriptor(AgentTools.Forecast, "趋势预测", "基于历史数据预测趋势与 What-If 模拟。", CategoryPlanning, true, BackendPending, "M7-05~M7-08"),
		new AgentToolDescriptor(AgentTools.Alert, "告警监控", "设置阈值监控与异常通知。", CategoryMonitor, true, BackendPending, "M7-05~M7-08"),
		new AgentToolDescriptor(AgentTools.Workflow, "流程编排", "跨系统编排审批/同步等工作流。", CategoryAutomation, false, BackendPending, "M7-05~M7-08"),
	};

	/// <summary>返回全部工具描述（供蓝图与目录端点）。</summary>
	public static IReadOnlyList<AgentToolDescriptor> GetAll() => Catalog;

	/// <summary>按工具类型获取描述；不存在返回 <c>null</c>。</summary>
	public static AgentToolDescriptor? Get(string tool)
		=> Catalog.FirstOrDefault(c => c.Tool == tool);

	/// <summary>
	/// <strong>确定性</strong>意图解析：把用户请求映射为有序工具选择序列（不调用 LLM）。
	/// 通过关键词命中映射到预置工具集；多个关键词可叠加（去重后按固定优先级排序）。
	/// </summary>
	/// <param name="request">用户原始请求（自然语言或结构化意图）。</param>
	/// <returns>有序工具选择列表（Order 从 1 起）；无命中时返回空列表。</returns>
	public static IReadOnlyList<AgentToolSelection> ResolveFromIntent(string? request)
	{
		if (string.IsNullOrWhiteSpace(request))
			return Array.Empty<AgentToolSelection>();

		var lower = request.ToLowerInvariant();
		var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// 异常原因分析（最复杂，优先纳入相关工具链）
		if (ContainsAny(lower, "异常", "为什么", "为何", "原因", "下降", "下跌", "波动", "下滑", "anomaly", "why", "root cause", "drop"))
			matched.AddAll(AgentTools.Query, AgentTools.Dashboard, AgentTools.Forecast);

		if (ContainsAny(lower, "预测", "趋势", "forecast", "trend", "what-if", "未来"))
			matched.Add(AgentTools.Forecast);
		if (ContainsAny(lower, "报表", "report", "导出", "excel", "明细表"))
			matched.Add(AgentTools.Report);
		if (ContainsAny(lower, "看板", "仪表盘", "dashboard", "可视化", "图表", "大屏"))
			matched.Add(AgentTools.Dashboard);
		if (ContainsAny(lower, "查询", "指标", "多少", "统计", "sql", "query", "计算", "排名"))
			matched.Add(AgentTools.Query);
		if (ContainsAny(lower, "元数据", "有哪些表", "字段", "表结构", "metadata", "schema"))
			matched.Add(AgentTools.Metadata);
		if (ContainsAny(lower, "语义", "业务术语", "口径", "同义词", "semantic"))
			matched.Add(AgentTools.Semantic);
		if (ContainsAny(lower, "告警", "报警", "提醒", "阈值", "alert", "notify"))
			matched.Add(AgentTools.Alert);
		if (ContainsAny(lower, "流程", "审批", "工作流", "workflow", "同步", "自动化"))
			matched.Add(AgentTools.Workflow);

		if (matched.Count == 0)
			return Array.Empty<AgentToolSelection>();

		// 固定优先级排序，保证同输入输出稳定（确定性，便于 Golden 复跑）
		var priority = new[] { AgentTools.Metadata, AgentTools.Semantic, AgentTools.Query, AgentTools.Dashboard, AgentTools.Report, AgentTools.Forecast, AgentTools.Alert, AgentTools.Workflow };
		var ordered = priority.Where(matched.Contains).ToList();

		var selections = new List<AgentToolSelection>();
		for (var i = 0; i < ordered.Count; i++)
		{
			var tool = ordered[i];
			var desc = Get(tool);
			selections.Add(new AgentToolSelection
			{
				Tool = tool,
				Order = i + 1,
				Reason = desc is null ? null : $"根据意图命中工具：{desc.Name}。",
			});
		}

		return selections;
	}

	/// <summary>
	/// 构建异常原因分析链路（销售额→同比→环比→区域→客户→产品→渠道）。
	/// 纯模板化、不调用 LLM，保证结果确定且可解释。
	/// </summary>
	/// <param name="metric">异常指标名（如 销售额），用于生成可读说明。</param>
	/// <param name="entity">关联业务实体语义名（可选，用于说明）。</param>
	/// <returns>有序分析步骤列表（Order 从 1 起）。</returns>
	public static IReadOnlyList<AnalysisStep> BuildAnomalyChain(string? metric, string? entity = null)
	{
		var m = string.IsNullOrWhiteSpace(metric) ? "指标" : metric.Trim();
		var scope = string.IsNullOrWhiteSpace(entity) ? "" : $"（{entity.Trim()}）";

		return new List<AnalysisStep>
		{
			new() { Order = 1, Dimension = AnalysisDimensions.Time, Direction = AnalysisDirections.YoY, Tool = AgentTools.Query, Description = $"{m}{scope} 同比：对比去年同期，判断回落是否季节性。" },
			new() { Order = 2, Dimension = AnalysisDimensions.Time, Direction = AnalysisDirections.MoM, Tool = AgentTools.Query, Description = $"{m}{scope} 环比：对比上一周期，定位突变发生时段。" },
			new() { Order = 3, Dimension = AnalysisDimensions.Region, Direction = AnalysisDirections.Drill, Tool = AgentTools.Dashboard, Description = $"沿区域下钻：识别下跌主要来自哪些地区。" },
			new() { Order = 4, Dimension = AnalysisDimensions.Customer, Direction = AnalysisDirections.Drill, Tool = AgentTools.Query, Description = $"沿客户下钻：识别流失或下滑的关键客户群。" },
			new() { Order = 5, Dimension = AnalysisDimensions.Product, Direction = AnalysisDirections.Drill, Tool = AgentTools.Query, Description = $"沿产品下钻：定位拖累整体的具体品类/SKU。" },
			new() { Order = 6, Dimension = AnalysisDimensions.Channel, Direction = AnalysisDirections.Drill, Tool = AgentTools.Dashboard, Description = $"沿渠道下钻：对比线上线下等渠道贡献变化。" },
		};
	}

	private static bool ContainsAny(string source, params string[] keywords)
	{
		foreach (var kw in keywords)
			if (source.Contains(kw, StringComparison.OrdinalIgnoreCase))
				return true;
		return false;
	}
}

/// <summary>工具描述（P9）。目录中的一项元数据。</summary>
/// <param name="Tool">工具类型，取值见 <see cref="AgentTools"/>。</param>
/// <param name="Name">展示名。</param>
/// <param name="Description">能力说明。</param>
/// <param name="Category">分类。</param>
/// <param name="RequiresEntity">是否需要关联业务实体（取数类为 true，治理/自动化类可为 false）。</param>
/// <param name="BackendStatus">后端连接状态：<c>live</c>=已接真实后端；<c>pending</c>=尚为诚实受控信封（未接真实后端，绝不伪造成功）。</param>
/// <param name="BackendMilestone">真实后端计划接入的里程碑（<c>pending</c> 时有效）。</param>
public sealed record AgentToolDescriptor(
	string Tool,
	string Name,
	string Description,
	string Category,
	bool RequiresEntity,
	string BackendStatus = "pending",
	string? BackendMilestone = null);

/// <summary>HashSet 批量添加扩展（内部使用）。</summary>
internal static class ToolRegistryHashSetExtensions
{
	public static void AddAll(this HashSet<string> set, params string[] values)
	{
		foreach (var v in values)
			set.Add(v);
	}
}
