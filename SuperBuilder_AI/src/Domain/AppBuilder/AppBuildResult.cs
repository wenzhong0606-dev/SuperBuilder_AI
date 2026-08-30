namespace SuperBuilder_AI.Models.AppBuilder;

/// <summary>
/// 应用编排结果（P8.2 AppBuilderAgent）。
///
/// <para>统一承载两条编排路径的产物：</para>
/// <list type="bullet">
/// <item><see cref="BuildFromDslAsync"/>（默认路径，确定性、不调用 LLM）—— <see cref="UsedAi"/> 为 false。</item>
/// <item><see cref="GenerateFromDescriptionAsync"/>（非默认路径，调用 LLM 生成 DSL）—— <see cref="UsedAi"/> 为 true。</item>
/// </list>
///
/// 无论哪条路径，只要校验失败 <see cref="Success"/> 即为 false，并给出全部错误（便于编辑器一次性展示）。
/// </summary>
public sealed class AppBuildResult
{
	/// <summary>是否编排成功（结构化校验通过且已生成 <see cref="Plan"/>）。</summary>
	public bool Success { get; init; }

	/// <summary>成功时生成的 <see cref="AppPlan"/> 实体（已填充 TenantId / Code / Name / DslJson 等）。</summary>
	public AppPlan? Plan { get; init; }

	/// <summary>失败时的全部校验错误。</summary>
	public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

	/// <summary>成功时对应的 DSL JSON（缩进，便于版本库可读与人工审阅）。</summary>
	public string? DslJson { get; init; }

	/// <summary>本次编排是否启用了 LLM 增强（区分默认路径与非默认路径）。</summary>
	public bool UsedAi { get; init; }

	/// <summary>成功工厂。</summary>
	public static AppBuildResult Ok(AppPlan plan, string dslJson, bool usedAi = false)
		=> new() { Success = true, Plan = plan, DslJson = dslJson, UsedAi = usedAi };

	/// <summary>失败工厂。</summary>
	/// <param name="errors">全部校验/解析错误。</param>
	/// <param name="usedAi">本次失败是否发生在 LLM 增强路径（用于区分默认路径与非默认路径）。</param>
	public static AppBuildResult Fail(IReadOnlyList<string> errors, bool usedAi = false)
		=> new() { Success = false, Errors = errors, UsedAi = usedAi };
}
