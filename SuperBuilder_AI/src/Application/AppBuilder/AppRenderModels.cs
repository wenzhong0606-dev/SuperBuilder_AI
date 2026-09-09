namespace SuperBuilder_AI.Models.AppBuilder;

/// <summary>
/// M7-11：应用运行响应（render / preview）。仅暴露白名单过滤后的呈现配置与数据，不返回完整内部 DSL。
/// </summary>
public sealed class AppRenderModel
{
	/// <summary>应用编码。</summary>
	public string Code { get; set; } = string.Empty;

	/// <summary>应用名称。</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>主题键（可选）。</summary>
	public string? ThemeKey { get; set; }

	/// <summary>发布版本号（render 为发布快照；preview 为草稿修订）。</summary>
	public int? PublishedVersion { get; set; }

	/// <summary>
	/// 整体是否成功。<c>false</c> 表示至少有一个数据组件失败（含绑定缺失/不支持/安全拒绝），
	/// 即使部分组件成功也不得报告为完整成功。
	/// </summary>
	public bool Succeeded { get; set; } = true;

	/// <summary>组件渲染结果列表（顺序与 DSL 一致）。</summary>
	public List<AppComponentRender> Components { get; set; } = new();
}

/// <summary>M7-11：单个组件渲染结果。</summary>
public sealed class AppComponentRender
{
	/// <summary>组件 Id。</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>组件类型。</summary>
	public string Type { get; set; } = string.Empty;

	/// <summary>组件标题。</summary>
	public string? Title { get; set; }

	/// <summary>图表类型（chart 组件）。</summary>
	public string? ChartType { get; set; }

	/// <summary>分类轴字段（chart 组件）。</summary>
	public List<string> AxisFields { get; set; } = new();

	/// <summary>度量序列（chart 组件）。</summary>
	public List<AppSeriesSpec> Series { get; set; } = new();

	/// <summary>静态文本内容（text 组件）。</summary>
	public string? Text { get; set; }

	/// <summary>结果列元数据（数据组件）。</summary>
	public List<AppColumnSpec> Columns { get; set; } = new();

	/// <summary>结果数据行（数据组件）。</summary>
	public List<Dictionary<string, object?>> Data { get; set; } = new();

	/// <summary>本组件是否成功取数；失败时为 false。</summary>
	public bool Succeeded { get; set; } = true;

	/// <summary>失败时的错误码（结构化；如 SB_APP_002 / SB_AUTHZ_001）。</summary>
	public string? ErrorCode { get; set; }

	/// <summary>失败时的友好提示。</summary>
	public string? ErrorMessage { get; set; }
}

/// <summary>结果列元数据。</summary>
public sealed class AppColumnSpec
{
	/// <summary>物理/结果列名。</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>展示名（缺省等于 Name）。</summary>
	public string? DisplayName { get; set; }

	/// <summary>数据类型（如 string/int/decimal/datetime）。</summary>
	public string? Type { get; set; }
}

/// <summary>图表度量序列。</summary>
public sealed class AppSeriesSpec
{
	public string Name { get; set; } = string.Empty;
	public string? DisplayName { get; set; }
}
