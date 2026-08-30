using SuperBuilder_AI.Models.AppBuilder;

namespace SuperBuilder_AI.Interfaces.AppBuilder;

/// <summary>
/// 应用编排端口（P8.2 AI App Builder）。
///
/// <para>
/// 职责：把<strong>自然语言描述</strong>或<strong>结构化 DSL</strong> 编排为可持久化的
/// <see cref="AppPlan"/> 实体。编排层不负责渲染（那是后续渲染器的职责），也不负责持久化
/// （那是 P8.3 控制器 / 仓储的职责）。
/// </para>
///
/// <para>
/// <strong>零回归门控（与 P5/P6 同款手法）</strong>：
/// <see cref="BuildFromDslAsync"/> 是<em>默认路径</em>，纯确定性、不触碰任何 LLM，
/// 行为逐字节稳定；<see cref="GenerateFromDescriptionAsync"/> 是<em>非默认路径</em>，仅当调用方
/// 显式传入自然语言描述时才启用 LLM。两条路径都不触碰 Golden 依赖文件。
/// </para>
/// </summary>
public interface IAppBuilderAgent
{
	/// <summary>
	/// 默认路径：把已结构化的 <see cref="AppDsl"/> 直接编排为 <see cref="AppPlan"/>。
	/// 不调用 LLM，确定性、可离线、零回归。
	/// </summary>
	/// <param name="tenantId">目标租户（0 表示全局模板）。</param>
	/// <param name="dsl">已结构化的应用 DSL（调用方通常已从编辑器/请求体构造）。</param>
	/// <param name="code">可选业务编码；为空时取 <see cref="AppDsl.Code"/> 或按名称生成 slug。</param>
	/// <returns>编排结果；校验失败则 <see cref="AppBuildResult.Success"/> 为 false。</returns>
	Task<AppBuildResult> BuildFromDslAsync(long tenantId, AppDsl dsl, string? code = null);

	/// <summary>
	/// 非默认路径：把自然语言描述编排为 <see cref="AppPlan"/>。
	/// 通过 <c>IQwenService</c> 生成 DSL JSON，再经 <see cref="IAppDslSerializer"/> 校验后落地。
	/// </summary>
	/// <param name="tenantId">目标租户（0 表示全局模板）。</param>
	/// <param name="description">用户自然语言描述（如"做一个销售看板，首页展示总销售额 KPI 和地区趋势图"）。</param>
	/// <param name="code">可选业务编码；为空时按描述生成 slug。</param>
	/// <returns>编排结果；<see cref="AppBuildResult.UsedAi"/> 为 true，失败时给出 LLM 输出无法解析的原因。</returns>
	Task<AppBuildResult> GenerateFromDescriptionAsync(long tenantId, string description, string? code = null);
}
