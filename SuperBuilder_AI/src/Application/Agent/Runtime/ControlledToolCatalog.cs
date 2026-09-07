using System.Collections.Generic;
using System.Linq;
using SuperBuilder_AI.Interfaces.Agent.Runtime;

namespace SuperBuilder_AI.Services.Agent.Runtime;

/// <summary>受控工具目录（M7-03）：聚合已注册 <see cref="ITool"/>，按工具类型解析。</summary>
public sealed class ControlledToolCatalog : IToolCatalog
{
	private readonly Dictionary<string, ITool> _map;

	/// <summary>从已注册工具集合构建目录（按 <see cref="ITool.Tool"/> 建立索引）。</summary>
	public ControlledToolCatalog(IEnumerable<ITool> tools)
	{
		_map = new Dictionary<string, ITool>(System.StringComparer.OrdinalIgnoreCase);
		foreach (var t in tools)
			_map[t.Tool] = t;
	}

	/// <inheritdoc />
	public ITool? Get(string tool)
		=> _map.TryGetValue(tool, out var t) ? t : null;

	/// <inheritdoc />
	public IReadOnlyList<ITool> All => _map.Values.ToList();
}
