namespace SuperBuilder_AI.Interfaces.Agent.Runtime;

/// <summary>
/// 工具权限策略端口（M7-03）：<strong>deny-by-default</strong>。
///
/// <para>调用方仅能执行其被授予（<paramref name="grantedTools"/>）的工具；
/// 任何不在授予集中的工具一律拒绝。授予集为空时仅放行 Safe 级工具（由运行时默认基线决定）。</para>
/// </summary>
public interface IToolPermissionPolicy
{
	/// <summary>调用方是否被授予执行该工具的权限。</summary>
	/// <param name="tool">工具类型。</param>
	/// <param name="grantedTools">本次调用方被授予的工具集合（deny-by-default 基线）。</param>
	bool IsAllowed(string tool, IReadOnlySet<string> grantedTools);
}
