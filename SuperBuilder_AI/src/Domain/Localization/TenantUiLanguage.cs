using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.Localization;

/// <summary>
/// 租户界面语言关系（M3-01 语言关系模型）。
/// <para>将原先存放在 <see cref="TenantSetting"/> 的 <c>localization:availableCultures/defaultCulture</c> JSON
/// 提升为结构化关系：每租户对平台 <see cref="UiLanguage"/> 目录的一行授权，含启用、默认与排序。
/// 约束（由 <c>ITenantLanguageService</c> 在事务内强制）：每租户至少一种启用语言、恰有一个默认语言、默认语言必须处于启用状态。</para>
/// </summary>
public sealed class TenantUiLanguage : BaseEntity
{
    public long TenantId { get; set; }
    public long UiLanguageId { get; set; }
    public bool Enabled { get; set; } = true;
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}
