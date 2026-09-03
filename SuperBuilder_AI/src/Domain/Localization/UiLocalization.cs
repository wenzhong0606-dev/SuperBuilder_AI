using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.Localization;

/// <summary>平台可供租户选择的界面语言。</summary>
public sealed class UiLanguage : BaseEntity
{
    public string Culture { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>系统界面文本。TenantId=0 为平台基线，大于 0 为租户覆盖。</summary>
public sealed class UiTextResource : BaseEntity
{
    public long TenantId { get; set; }
    public string Culture { get; set; } = string.Empty;
    public string ResourceKey { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}
