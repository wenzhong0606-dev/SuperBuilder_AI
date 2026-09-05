namespace SuperBuilder_AI.Components.Models;

/// <summary>M3-02 平台语言目录视图（管理员视角，含启用状态与翻译进度）。</summary>
public sealed class AdminLanguageView
{
	/// <summary>目录主键。</summary>
	public long Id { get; set; }
	/// <summary>BCP 47 文化码（经后端归一化，如 en-US）。</summary>
	public string Culture { get; set; } = "";
	/// <summary>平台统一显示名（如 English）。</summary>
	public string DisplayName { get; set; } = "";
	/// <summary>本地名称（如 English / 中文），切换器优先展示。</summary>
	public string NativeName { get; set; } = "";
	/// <summary>是否启用（停用后租户侧不可用）。</summary>
	public bool Enabled { get; set; }
	/// <summary>排序权重（越小越靠前）。</summary>
	public int SortOrder { get; set; }
	/// <summary>已翻译键数（IsTranslated=true 的键数）。</summary>
	public int TranslatedCount { get; set; }
	/// <summary>平台基线键总数（ResourceKeys 注册条目）。</summary>
	public int TotalKeys { get; set; }
	/// <summary>待翻译键数（TotalKeys - TranslatedCount，下限 0）。</summary>
	public int PendingCount => Math.Max(0, TotalKeys - TranslatedCount);
}

/// <summary>M3-02 新建语言请求（BCP 47 文化码 + 名称 + 可选复制源）。</summary>
public sealed class AdminLanguageCreate
{
	/// <summary>BCP 47 文化码（en-US 等）；后端会归一化并校验唯一性与格式。</summary>
	public string? Culture { get; set; }
	/// <summary>平台统一显示名（必填）。</summary>
	public string? DisplayName { get; set; }
	/// <summary>本地名称（必填）。</summary>
	public string? NativeName { get; set; }
	/// <summary>可选复制源文化码；默认复制 en-US 的平台基线键集合并标记「待翻译」。</summary>
	public string? CopyFromCulture { get; set; }
}

/// <summary>M3-02 更新语言请求（仅可更新显示名/本地名/排序）。</summary>
public sealed class AdminLanguageUpdate
{
	public string? DisplayName { get; set; }
	public string? NativeName { get; set; }
	public int? SortOrder { get; set; }
}

/// <summary>公开语言目录视图（含本地名称），供语言切换器展示 NativeName。</summary>
public sealed class PublicLanguageView
{
	/// <summary>BCP 47 文化码（如 en-US）。</summary>
	public string Culture { get; set; } = "";
	/// <summary>平台统一显示名。</summary>
	public string DisplayName { get; set; } = "";
	/// <summary>本地名称。</summary>
	public string NativeName { get; set; } = "";
}
