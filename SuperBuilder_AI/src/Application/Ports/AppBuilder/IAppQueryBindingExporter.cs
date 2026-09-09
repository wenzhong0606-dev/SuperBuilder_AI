using SuperBuilder_AI.Models.AppBuilder;

namespace SuperBuilder_AI.Interfaces.AppBuilder;

/// <summary>
/// M7-11：查询快照 → 应用取数绑定导出端口（C1）。
/// 校验归属并按 §9 支持矩阵拒绝不可无损转换的计划。
/// </summary>
public interface IAppQueryBindingExporter
{
	/// <summary>
	/// 凭 turnId 导出绑定。仅快照创建者本人可调用（跨用户/跨租户抛 403；无效/过期抛 404）。
	/// 不支持的查询模式抛 422（<see cref="ErrorCodes.AppBindingNotSupported"/>）。
	/// </summary>
	Task<AppDataSourceBinding> ExportAsync(
		string turnId, long tenantId, long userId, CancellationToken cancellationToken = default);
}
