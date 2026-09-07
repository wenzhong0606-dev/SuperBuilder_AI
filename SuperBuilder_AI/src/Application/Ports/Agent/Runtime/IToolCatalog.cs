namespace SuperBuilder_AI.Interfaces.Agent.Runtime;

/// <summary>受控工具目录端口（M7-03）。按工具类型解析已注册的实现。</summary>
public interface IToolCatalog
{
	/// <summary>按工具类型获取实现；未知工具返回 <c>null</c>。</summary>
	ITool? Get(string tool);

	/// <summary>全部已注册工具。</summary>
	IReadOnlyList<ITool> All { get; }
}
