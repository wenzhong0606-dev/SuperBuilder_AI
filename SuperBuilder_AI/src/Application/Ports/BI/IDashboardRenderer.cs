using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Dashboard.Rendering;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Interfaces.BI.Dashboard;

/// <summary>
/// LowcodeRenderer 渲染引擎端口（P6.3）。
///
/// 把 <see cref="DashboardDsl"/> 转换为纯结构化的 <see cref="DashboardRenderModel"/>，
/// 不产出也不存储任何 HTML（与 P6.2 红线一致）。
/// </summary>
public interface IDashboardRenderer
{
	/// <summary>渲染仪表盘。</summary>
	/// <param name="dsl">已反序列化并校验通过的仪表盘 DSL。</param>
	/// <param name="context">平台运行时上下文（租户/语言等）。</param>
	/// <param name="cancellationToken">取消令牌。</param>
	Task<DashboardRenderModel> RenderAsync(
		DashboardDsl dsl,
		PlatformContext context,
		CancellationToken cancellationToken = default);
}
