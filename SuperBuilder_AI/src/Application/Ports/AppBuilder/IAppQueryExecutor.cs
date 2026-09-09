using SuperBuilder_AI.Models.AppBuilder;

namespace SuperBuilder_AI.Interfaces.AppBuilder;

/// <summary>
/// M7-11：应用组件确定性取数执行器端口（C2）。
/// 由 binding 构造 QueryIntent 绕过 NLU，复用当前访问者安全链路，并做一致性断言。
/// </summary>
public interface IAppQueryExecutor
{
	/// <summary>
	/// 执行单个组件取数。访问者无数据源授权 / 绑定源未授权 → 403；
	/// 一致性不符 / 不支持 → 422；执行失败 → 组件 Succeeded=false（脱敏错误）。
	/// </summary>
	Task<AppComponentRender> ExecuteComponentAsync(
		AppDataSourceBinding binding, long tenantId, long userId, CancellationToken cancellationToken = default);
}
