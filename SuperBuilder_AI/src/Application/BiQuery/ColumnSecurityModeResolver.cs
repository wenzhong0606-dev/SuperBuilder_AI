using SuperBuilder_AI.Application.Common.Options;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 列级安全分类模式的解析（M5-08）：全局模式与逐租户覆盖的单一事实来源。
/// </summary>
public static class ColumnSecurityModeResolver
{
	/// <summary>
	/// 解析给定租户的有效分类模式：优先使用 <see cref="ColumnSecurityOptions.TenantOverrides"/> 中的
	/// 显式覆盖，否则回退全局 <see cref="ColumnSecurityOptions.ClassifierMode"/>。
	/// </summary>
	public static ColumnClassifierMode Resolve(ColumnSecurityOptions? options, long tenantId)
	{
		if (options?.TenantOverrides is { } overrides
			&& overrides.TryGetValue(tenantId, out var ov)
			&& ov?.Mode is { } m)
			return m;
		return options?.ClassifierMode ?? ColumnClassifierMode.DenyNothing;
	}
}
