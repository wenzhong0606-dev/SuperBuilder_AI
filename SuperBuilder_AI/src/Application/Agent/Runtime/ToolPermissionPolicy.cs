using System.Collections.Generic;
using SuperBuilder_AI.Interfaces.Agent.Runtime;

namespace SuperBuilder_AI.Services.Agent.Runtime;

/// <summary>
/// 工具权限策略（M7-03）：<strong>deny-by-default</strong>。
/// 仅当工具位于调用方被授予的集合内才放行；空集合时一律拒绝（安全下限）。
/// </summary>
public sealed class ToolPermissionPolicy : IToolPermissionPolicy
{
	/// <inheritdoc />
	public bool IsAllowed(string tool, IReadOnlySet<string> grantedTools)
		=> !string.IsNullOrWhiteSpace(tool) && grantedTools.Contains(tool);
}
