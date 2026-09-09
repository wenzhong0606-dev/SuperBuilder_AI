using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SuperBuilder_AI.Interfaces.Agent;
using SuperBuilder_AI.Models.Agent;

namespace SuperBuilder_AI.Services.Agent;

/// <summary>
/// <see cref="IAgentDslSerializer"/> 的默认实现（P9 AI Agent / Copilot）。
///
/// 实现要点（与 P6 <c>DashboardDslSerializer</c>、P8 <c>AppDslSerializer</c> 同源）：
/// <list type="number">
/// <item>提供一致的 <see cref="JsonSerializerOptions"/>（camelCase、大小写不敏感、忽略 null 以缩小体积）。</item>
/// <item><strong>先校验后信任</strong>：<see cref="TryDeserialize"/> 反序列化成功后必须再跑一遍
///       <see cref="Validate"/>，因为 JSON 能表达的类型合法组合未必是业务合法的（例如工具不在白名单）。</item>
/// <item><strong>红线硬校验</strong>：任何文本字段出现 HTML 标签或脚本片段一律拒绝，从源头保证"底层不存裸 HTML"。</item>
/// </list>
/// </summary>
public sealed class AgentDslSerializer : IAgentDslSerializer
{
	/// <summary>统一的序列化选项：camelCase、大小写不敏感、忽略 null 以缩小体积。</summary>
	private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	/// <summary>HTML 标签或脚本片段的检测模式（P9 红线：DSL 不得承载标记语言）。</summary>
	private static readonly Regex HtmlPattern = new(
		@"<\s*/?\s*(script|iframe|style|div|span|table|img|a|p|br|h[1-6])\b|<\s*!\s*doctype|javascript\s*:",
		RegexOptions.IgnoreCase | RegexOptions.Compiled,
		TimeSpan.FromMilliseconds(100));

	/// <inheritdoc />
	public string Serialize(AgentDsl dsl)
	{
		ArgumentNullException.ThrowIfNull(dsl);
		return JsonSerializer.Serialize(dsl, Options);
	}

	/// <inheritdoc />
	public bool TryDeserialize(string? json, out AgentDsl? dsl, out IReadOnlyList<string> errors)
	{
		dsl = null;

		if (string.IsNullOrWhiteSpace(json))
		{
			errors = new[] { "Agent DSL JSON 不能为空。" };
			return false;
		}

		AgentDsl? parsed;
		try
		{
			parsed = JsonSerializer.Deserialize<AgentDsl>(json, Options);
		}
		catch (JsonException ex)
		{
			errors = new[] { $"Agent DSL JSON 解析失败：{ex.Message}" };
			return false;
		}

		if (parsed is null)
		{
			errors = new[] { "Agent DSL JSON 反序列化结果为空。" };
			return false;
		}

		// 版本白名单：未知版本直接拒绝（M9-10 之前的唯一行为；先拒绝再升级）。
		if (!AgentDslVersions.Supported.Contains(parsed.Version))
		{
			errors = new[] { $"不支持的 Agent DSL 版本：{parsed.Version}（受支持：{string.Join(", ", AgentDslVersions.Supported)}）。" };
			return false;
		}

		// M9-10：受支持版本归一化为 Current（V1 与 Current 同构，结构就绪、前向兼容）。
		Upgrade(parsed);

		var validationErrors = Validate(parsed);
		if (validationErrors.Count > 0)
		{
			errors = validationErrors;
			return false;
		}

		dsl = parsed;
		errors = Array.Empty<string>();
		return true;
	}

	/// <inheritdoc />
	public IReadOnlyList<string> Validate(AgentDsl dsl)
	{
		ArgumentNullException.ThrowIfNull(dsl);

		var errors = new List<string>();

		// 1. 版本
		if (!AgentDslVersions.Supported.Contains(dsl.Version))
			errors.Add($"不支持的 Agent DSL 版本：{dsl.Version}（受支持：{string.Join(", ", AgentDslVersions.Supported)}）。");

		// 2. 名称 + 红线
		if (string.IsNullOrWhiteSpace(dsl.Name))
			errors.Add("Agent 计划名称不能为空。");
		else if (ContainsHtml(dsl.Name))
			errors.Add("Agent 计划名称含 HTML 标记或脚本，DSL 只允许结构化文本。");

		if (ContainsHtml(dsl.Description))
			errors.Add("Agent 计划描述含 HTML 标记或脚本，DSL 只允许结构化文本。");
		if (ContainsHtml(dsl.UserRequest))
			errors.Add("用户请求含 HTML 标记或脚本，DSL 只允许结构化文本。");

		// 3. 至少一个工具（计划必须选定工具）
		if (dsl.SelectedTools.Count == 0)
			errors.Add("Agent 计划至少需要选择一个工具。");

		var toolOrders = new HashSet<int>();
		foreach (var tool in dsl.SelectedTools)
		{
			ValidateToolSelection(tool, toolOrders, errors);
		}

		// 4. 异常分析链路枚举白名单
		var stepOrders = new HashSet<int>();
		foreach (var step in dsl.AnomalyChain)
		{
			ValidateAnalysisStep(step, stepOrders, errors);
		}

		return errors;
	}

	/// <summary>
	/// 将任意<strong>受支持</strong>版本的 DSL 归一化为 <see cref="AgentDslVersions.Current"/>（M9-10）。
	/// 调用方须先通过 <see cref="AgentDslVersions.Supported"/> 白名单。归一化后即可被运行时消费，
	/// 无需手工数据迁移；当前仅 V1（等同 Current），结构同构，归一化仅保证版本号统一为 Current。
	/// </summary>
	/// <remarks>
	/// 显式<strong>不</strong>做「版本 == Current 即跳过」的早返回：因 V1 == Current，须对所有受支持版本
	/// 执行结构归一化，确保未来 V2 演进时旧文档也能统一升级。
	/// </remarks>
	private static void Upgrade(AgentDsl dsl)
	{
		switch (dsl.Version)
		{
			case AgentDslVersions.V1:
				NormalizeV1(dsl);
				break;
			default:
				// 白名单已拦截未知版本，理论上不可达。
				throw new InvalidOperationException($"未注册的 Agent DSL 升级路径：{dsl.Version}");
		}

		dsl.Version = AgentDslVersions.Current;
	}

	/// <summary>V1 → 当前版本的结构归一化。</summary>
	/// <remarks>
	/// 当前 V1 与 Current 同构，且 <see cref="AgentDsl"/> 无可空的结构性布局字段（<c>SelectedTools</c> /
	/// <c>AnomalyChain</c> 均以空集合为默认值）；此处为 V2 演进预留迁移点，不修改任何字段，保证旧文档无损升级。
	/// </remarks>
	private static void NormalizeV1(AgentDsl dsl)
	{
		// V1 无结构性缺口；预留字段迁移位置（如未来 V2 重命名工具类型枚举）。
	}

	private static void ValidateToolSelection(AgentToolSelection tool, HashSet<int> orders, List<string> errors)
	{
		if (string.IsNullOrWhiteSpace(tool.Tool))
			errors.Add("工具选择项的 Tool 不能为空。");
		else if (!AgentTools.Supported.Contains(tool.Tool))
			errors.Add($"不支持的工具类型：{tool.Tool}（受支持：{string.Join(", ", AgentTools.Supported)}）。");

		if (tool.Order <= 0)
			errors.Add($"工具 {tool.Tool} 的执行顺序必须大于 0。");
		else if (!orders.Add(tool.Order))
			errors.Add($"工具执行顺序重复：{tool.Order}。");

		foreach (var kv in tool.Parameters)
			if (ContainsHtml(kv.Value))
				errors.Add($"工具 {tool.Tool} 的参数 {kv.Key} 含 HTML 标记或脚本。");
	}

	private static void ValidateAnalysisStep(AnalysisStep step, HashSet<int> orders, List<string> errors)
	{
		if (!AnalysisDimensions.Supported.Contains(step.Dimension))
			errors.Add($"不支持的分析维度：{step.Dimension}（受支持：{string.Join(", ", AnalysisDimensions.Supported)}）。");

		if (!AnalysisDirections.Supported.Contains(step.Direction))
			errors.Add($"不支持的分析方向：{step.Direction}（受支持：{string.Join(", ", AnalysisDirections.Supported)}）。");

		if (!AgentTools.Supported.Contains(step.Tool))
			errors.Add($"不支持的分析工具：{step.Tool}（受支持：{string.Join(", ", AgentTools.Supported)}）。");

		if (step.Order <= 0)
			errors.Add($"分析步骤 {step.Dimension}/{step.Direction} 的顺序必须大于 0。");
		else if (!orders.Add(step.Order))
			errors.Add($"分析步骤顺序重复：{step.Order}。");

		if (ContainsHtml(step.Description))
			errors.Add($"分析步骤 {step.Dimension}/{step.Direction} 的描述含 HTML 标记或脚本。");
	}

	/// <summary>检测文本中是否混入 HTML 标签或脚本片段（P9 红线）。</summary>
	private static bool ContainsHtml(string? text)
		=> !string.IsNullOrWhiteSpace(text) && HtmlPattern.IsMatch(text);
}
