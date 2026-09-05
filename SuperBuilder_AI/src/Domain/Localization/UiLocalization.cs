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

    /// <summary>
    /// 是否已翻译。平台基线种子与人工保存的译文置 true；
    /// 新建语言从其它语言复制键集合时置 false（标记“待翻译”），便于平台管理员识别待补译项。
    /// </summary>
    public bool IsTranslated { get; set; } = true;
}
