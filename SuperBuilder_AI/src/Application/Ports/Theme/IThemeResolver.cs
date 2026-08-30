using SuperBuilder_AI.Models.Theme;

namespace SuperBuilder_AI.Interfaces.Theme;

/// <summary>
/// 主题级联解析端口（P7.2 Multi-Theme / Style Engine）。
///
/// <para>
/// 解析优先级（从高到低）：
/// <list type="number">
///   <item>仪表盘显式主题键 <c>dashboardThemeKey</c>（命中则来源 <see cref="ThemeSource.Dashboard"/>）；</item>
///   <item>租户默认主题（TenantSetting 键 <c>theme:defaultKey</c>，来源 <see cref="ThemeSource.Tenant"/>）；</item>
///   <item>内置默认浅色主题（<see cref="ThemeContext.Default"/>，来源 <see cref="ThemeSource.BuiltIn"/>）。</item>
/// </list>
/// </para>
///
/// <para>
/// 解析时主题检索范围为「当前租户自有主题 ∪ 内置全局主题（TenantId=0）」；租户自有主题优先于内置。
/// 任何层级缺失或反序列化失败时一律兜底到内置默认，确保渲染永不因主题缺失而失败。
/// </para>
/// </summary>
public interface IThemeResolver
{
	/// <summary>
	/// 解析出运行时可用的主题上下文。
	/// </summary>
	/// <param name="tenantId">当前租户 Id；为 0（系统/全局）时直接取内置默认。</param>
	/// <param name="dashboardThemeKey">仪表盘显式指定的主题键；为空时回退到租户默认或内置默认。</param>
	/// <param name="ct">取消令牌。</param>
	/// <returns>已解析的 <see cref="ThemeContext"/>（含结构化 <see cref="ThemeDsl"/> 与来源）。</returns>
	Task<ThemeContext> ResolveAsync(long tenantId, string? dashboardThemeKey = null, CancellationToken ct = default);
}
