using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Services.Platform;

/// <summary>
/// <see cref="IPlatformContextAccessor"/> 的默认实现：以 scoped 字段保存当前上下文。
///
/// 由 Runtime 入口（如 <c>BIConversationService</c>）在每个请求作用域内写入，
/// 供下游服务（含 P4.3 的 <c>SuperBIContext</c> 全局租户过滤）读取。
/// </summary>
public sealed class PlatformContextAccessor : IPlatformContextAccessor
{
	private PlatformContext? _current;

	/// <inheritdoc />
	public PlatformContext? Current
	{
		get => _current;
		set => _current = value;
	}
}
