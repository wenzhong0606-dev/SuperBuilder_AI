using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.Agent;
using SuperBuilder_AI.Models.Agent;

namespace SuperBuilder_AI.Services.Agent;

/// <summary>
/// <see cref="IAgentPlanner"/> 的默认实现（P9.2 AI Agent / Copilot）。
///
/// <para>编排两条路径：</para>
/// <list type="bullet">
/// <item><strong>默认路径</strong> <see cref="PlanFromIntentAsync"/>：意图 → <see cref="ToolRegistry"/> 确定性工具选择
///       + 异常原因分析链路，纯确定性、不调用 LLM。这是 P9.3 控制器生成 Agent 计划的主路径，行为与历史版本逐字节一致。</item>
/// <item><strong>非默认路径</strong> <see cref="GenerateFromDescriptionAsync"/>：自然语言描述 → 调 Qwen
///       生成 DSL JSON → 校验 → <see cref="AgentPlan"/>。仅当调用方显式传入描述时才启用 LLM。</item>
/// </list>
///
/// <para>两条路径都复用 P9.1 的 <see cref="IAgentDslSerializer"/> 做"先校验后信任"，并保证 DSL 红线
/// （绝不承载裸 HTML）。本服务不触碰任何 Golden 依赖文件，零回归风险。</para>
/// </summary>
public sealed class AgentPlanner : IAgentPlanner
{
	private readonly IAgentDslSerializer _dslSerializer;
	private readonly IQwenService _qwenService;

	/// <summary>创建 Agent 编排服务。</summary>
	public AgentPlanner(IAgentDslSerializer dslSerializer, IQwenService qwenService)
	{
		_dslSerializer = dslSerializer;
		_qwenService = qwenService;
	}

	/// <inheritdoc />
	public Task<AgentResult> PlanFromIntentAsync(long tenantId, string intent, string? code = null)
	{
		if (string.IsNullOrWhiteSpace(intent))
			return Task.FromResult(AgentResult.Fail(new[] { "Agent 意图不能为空。" }));

		// 默认路径：确定性工具选择，不调用 LLM。
		var selectedTools = ToolRegistry.ResolveFromIntent(intent);
		if (selectedTools.Count == 0)
			return Task.FromResult(AgentResult.Fail(new[] { "无法从意图中识别任何可用工具，请补充分析目标（如查询、看板、预测、异常等）。" }));

		// 意图含异常分析信号时，附加异常原因分析链路（确定性模板）。
		var anomalyChain = IsAnomalyIntent(intent)
			? ToolRegistry.BuildAnomalyChain(ExtractMetric(intent))
			: new List<AnalysisStep>();

		var dsl = new AgentDsl
		{
			Version = AgentDslVersions.Current,
			Code = code,
			Name = DeriveName(intent),
			Description = "由意图自动生成的 Agent 分析计划。",
			UserRequest = intent,
			SelectedTools = selectedTools.ToList(),
			AnomalyChain = anomalyChain.ToList(),
		};

		// 默认路径也走统一校验（红线、枚举白名单、顺序唯一），失败给出全部错误。
		var errors = _dslSerializer.Validate(dsl);
		if (errors.Count > 0)
			return Task.FromResult(AgentResult.Fail(errors));

		var dslJson = _dslSerializer.Serialize(dsl);
		var plan = ToPlan(tenantId, dsl, dslJson, code);
		return Task.FromResult(AgentResult.Ok(plan, dslJson));
	}

	/// <inheritdoc />
	public async Task<AgentResult> GenerateFromDescriptionAsync(long tenantId, string description, string? code = null)
	{
		if (string.IsNullOrWhiteSpace(description))
			return AgentResult.Fail(new[] { "Agent 描述不能为空。" });

		// 非默认路径：调用 LLM 生成 DSL JSON。
		var prompt = BuildPrompt(description);
		var response = await _qwenService.GenerateSqlAsync(prompt);
		var json = CleanJson(response);

		// 先校验后信任：LLM 可能产出非法 DSL（HTML、未知枚举、缺工具等），统一在此拦截。
		if (!_dslSerializer.TryDeserialize(json, out var dsl, out var errors))
			return AgentResult.Fail(errors, usedAi: true);

		var dslJson = _dslSerializer.Serialize(dsl!);
		var plan = ToPlan(tenantId, dsl!, dslJson, code);
		return AgentResult.Ok(plan, dslJson, usedAi: true);
	}

	/// <summary>把校验通过的 DSL 与租户信息组装为可持久化的 <see cref="AgentPlan"/>。</summary>
	private static AgentPlan ToPlan(long tenantId, AgentDsl dsl, string dslJson, string? code)
		=> new()
		{
			TenantId = tenantId,
			Code = !string.IsNullOrWhiteSpace(code)
				? code
				: (!string.IsNullOrWhiteSpace(dsl.Code) ? dsl.Code : Slugify(dsl.Name)),
			Name = dsl.Name,
			Description = dsl.Description,
			Status = AgentStatuses.Draft,
			DslVersion = dsl.Version,
			DslJson = dslJson,
		};

	/// <summary>从意图派生计划名称（截断过长文本，保留可读前缀）。</summary>
	private static string DeriveName(string intent)
	{
		var trimmed = intent.Trim().Replace("\n", " ").Replace("\r", " ");
		if (trimmed.Length <= 40)
			return trimmed;
		return trimmed.Substring(0, 40).TrimEnd() + "…";
	}

	/// <summary>尝试从意图中提取异常分析的指标名（取已知指标关键词，否则返回 null 由注册表兜底为"指标"）。</summary>
	private static string? ExtractMetric(string intent)
	{
		var candidates = new[]
		{
			"销售额", "销量", "利润", "毛利", "收入", "成本", "库存", "客单价", "转化率", "退货率",
			"sales", "revenue", "profit", "cost", "inventory", "conversion",
		};
		var lower = intent.ToLowerInvariant();
		foreach (var c in candidates)
			if (lower.Contains(c.ToLowerInvariant()))
				return c;
		return null;
	}

	/// <summary>判断意图是否包含异常/归因分析信号。</summary>
	private static bool IsAnomalyIntent(string intent)
	{
		var keywords = new[]
		{
			"异常", "为什么", "为何", "原因", "下降", "下跌", "波动", "下滑", "回落", "减少", "亏损",
			"anomaly", "why", "root cause", "drop", "decline", "fall", "decrease", "reason",
		};
		var lower = intent.ToLowerInvariant();
		return keywords.Any(k => lower.Contains(k.ToLowerInvariant()));
	}

	/// <summary>把名称转换为可用作业务编码的 slug（小写、连字符分隔、去噪）。</summary>
	private static string Slugify(string name)
	{
		var src = string.IsNullOrWhiteSpace(name) ? "agent" : name;
		var sb = new System.Text.StringBuilder(src.Length);
		foreach (var ch in src.ToLowerInvariant())
		{
			if (char.IsLetterOrDigit(ch))
				sb.Append(ch);
			else if (char.IsWhiteSpace(ch) || ch is '-' or '_')
			{
				if (sb.Length > 0 && sb[^1] != '-')
					sb.Append('-');
			}
		}

		var slug = sb.ToString().Trim('-');
		return string.IsNullOrEmpty(slug) ? "agent" : slug;
	}

	/// <summary>清理 LLM 可能包裹的 Markdown 代码块标记。</summary>
	private static string CleanJson(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			throw new InvalidOperationException("AI 没有返回任何内容。");

		text = text.Trim();
		if (text.StartsWith("```"))
		{
			var firstLineEnd = text.IndexOf('\n');
			if (firstLineEnd >= 0)
				text = text.Substring(firstLineEnd + 1);

			var last = text.LastIndexOf("```");
			if (last >= 0)
				text = text.Substring(0, last);
		}

		return text.Trim();
	}

	/// <summary>构造 AgentDsl 生成提示词（描述 AgentDsl 的 JSON 结构约束，要求只返回合法 JSON）。</summary>
	private static string BuildPrompt(string description)
	{
		var tools = string.Join(" / ", AgentTools.Supported);
		var dimensions = string.Join(" / ", AnalysisDimensions.Supported);
		var directions = string.Join(" / ", AnalysisDirections.Supported);

		return $$"""
            你是企业级 AI 数据分析 Agent 的编排 AI。

            你的任务是：将用户的自然语言描述，转换为一份严格的 AgentDsl JSON（Agent 执行计划）。

            必须遵守的约束：

            1. 只能返回合法 JSON，禁止返回 Markdown、禁止 ```json、禁止 JSON 之外的解释。
            2. 计划至少需要选择一个工具（selectedTools 数组非空），工具类型（tool）只能是：{{tools}}。
            3. 每个工具选择须有唯一正序 order（从 1 开始）。
            4. 异常原因分析链路（anomalyChain）可选；若用户提供归因类描述，请输出 1~6 步分析链，
               维度（dimension）只能是：{{dimensions}}，方向（direction）只能是：{{directions}}，工具（tool）只能是：{{tools}}。
            5. 所有文本字段（name / description / reason / 各 description）不得包含 HTML 标签或脚本片段。
            6. 字段一律使用业务语义名，不要臆造不存在的字段。

            AgentDsl JSON 结构示例：

            {
              "version": "1.0",
              "code": "sales-anomaly-agent",
              "name": "销售额异常分析 Agent",
              "description": "分析销售额异常原因",
              "userRequest": "为什么本月销售额下降",
              "selectedTools": [
                { "tool": "query", "order": 1, "reason": "计算销售额指标" },
                { "tool": "dashboard", "order": 2, "reason": "可视化下钻结果" }
              ],
              "anomalyChain": [
                { "order": 1, "dimension": "time", "direction": "yoy", "tool": "query", "description": "销售额同比对比" },
                { "order": 2, "dimension": "region", "direction": "drill", "tool": "dashboard", "description": "沿区域下钻" }
              ]
            }

            ============================================================
            用户描述
            ============================================================

            {{description}}

            ============================================================
            最终只返回 JSON
            ============================================================
            """;
	}
}
