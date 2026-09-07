using System.Collections.Generic;
using System.Text.Json;
using SuperBuilder_AI.Interfaces.Agent.Runtime;
using SuperBuilder_AI.Models.Agent;

namespace SuperBuilder_AI.Services.Agent.Runtime;

/// <summary>
/// 受控工具基类（M7-03，沿用至 M7-04~M7-08）：统一产出<strong>诚实</strong>的执行信封（<c>Mode = controlled</c>）。
///
/// <para>关键红线：不触碰任何真实后端、不声称业务结果、无外部副作用。
/// 输出明确声明 <c>connected:false</c> 与待接入里程碑（M7-05~M7-08），杜绝"假成功按钮"。
/// M7-04 已把确定性、只读、无 LLM 的 Safe 工具（metadata/semantic）翻为 <c>live</c> 模式（见
/// <see cref="LiveMetadataTool"/>/<see cref="LiveSemanticTool"/>）；其余 Read/Write 工具在对应后端
/// 于后续里程碑接入前，继续走本诚实信封。运行时框架无需改动。</para>
/// </summary>
public abstract class ControlledToolBase : ITool
{
	/// <inheritdoc />
	public abstract string Tool { get; }

	/// <inheritdoc />
	public abstract ToolRisk Risk { get; }

	/// <inheritdoc />
	public virtual bool RequiresApproval => Risk == ToolRisk.Write;

	/// <summary>展示名（用于执行信封可读说明）。</summary>
	public abstract string DisplayName { get; }

	/// <inheritdoc />
	public Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken cancellationToken = default)
	{
		var output = JsonSerializer.Serialize(new Dictionary<string, object?>
		{
			["tool"] = Tool,
			["displayName"] = DisplayName,
			["mode"] = "controlled",
			["connected"] = false,
			["backendMilestone"] = "M7-05~M7-08",
			["note"] = "真实后端尚未接入（计划 M7-05~M7-08 逐工具接入）：此处仅校验参数并回执，不触碰外部系统、不声称业务结果。",
			["parameters"] = context.Parameters,
			["correlationId"] = context.CorrelationId,
		});
		return Task.FromResult(ToolResult.Ok(output, "controlled", RequiresApproval));
	}
}

/// <summary>元数据探查（Safe：只读自省）。</summary>
public sealed class MetadataTool : ControlledToolBase
{
	public override string Tool => AgentTools.Metadata;
	public override ToolRisk Risk => ToolRisk.Safe;
	public override string DisplayName => "元数据探查";
}

/// <summary>语义解析（Safe：只读自省）。</summary>
public sealed class SemanticTool : ControlledToolBase
{
	public override string Tool => AgentTools.Semantic;
	public override ToolRisk Risk => ToolRisk.Safe;
	public override string DisplayName => "语义解析";
}

/// <summary>指标查询（Read：需权限授权）。</summary>
public sealed class QueryTool : ControlledToolBase
{
	public override string Tool => AgentTools.Query;
	public override ToolRisk Risk => ToolRisk.Read;
	public override string DisplayName => "指标查询";
}

/// <summary>看板编排（Read：需权限授权）。</summary>
public sealed class DashboardTool : ControlledToolBase
{
	public override string Tool => AgentTools.Dashboard;
	public override ToolRisk Risk => ToolRisk.Read;
	public override string DisplayName => "看板编排";
}

/// <summary>趋势预测（Read：需权限授权）。</summary>
public sealed class ForecastTool : ControlledToolBase
{
	public override string Tool => AgentTools.Forecast;
	public override ToolRisk Risk => ToolRisk.Read;
	public override string DisplayName => "趋势预测";
}

/// <summary>报表导出（Write：需权限授权 + 人工审批）。</summary>
public sealed class ReportTool : ControlledToolBase
{
	public override string Tool => AgentTools.Report;
	public override ToolRisk Risk => ToolRisk.Write;
	public override string DisplayName => "报表导出";
}

/// <summary>告警监控（Write：需权限授权 + 人工审批）。</summary>
public sealed class AlertTool : ControlledToolBase
{
	public override string Tool => AgentTools.Alert;
	public override ToolRisk Risk => ToolRisk.Write;
	public override string DisplayName => "告警监控";
}

/// <summary>流程编排（Write：需权限授权 + 人工审批）。</summary>
public sealed class WorkflowTool : ControlledToolBase
{
	public override string Tool => AgentTools.Workflow;
	public override ToolRisk Risk => ToolRisk.Write;
	public override string DisplayName => "流程编排";
}
