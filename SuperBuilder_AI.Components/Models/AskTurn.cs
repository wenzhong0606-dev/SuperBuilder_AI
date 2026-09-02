namespace SuperBuilder_AI.Components.Models;

/// <summary>一次问数对话轮次（用户问题 + 后端响应 + 视图层多轮调整状态）。</summary>
public sealed class AskTurn
{
    public string Question = "";
    public BIResponse? Response;
    public string? Error;
    public string? Code;
    public string? TraceId;
    public Dictionary<int, VizOverride> Overrides { get; } = new();
    public string RefineCmd { get; set; } = "";
    public string? RefineMsg { get; set; }
    public string RefineInstruction { get; set; } = "";
    public bool RefineBusy { get; set; }
    public string? RefineInfo { get; set; }
    public string? RefineError { get; set; }
    public bool IsRefine { get; set; }
    /// <summary>该轮提问所用的数据源 ID 提示（仅用于会话恢复时回填选择，不影响后端调用）。</summary>
    public long DataSourceIdHint { get; set; }
    public bool PublishBusy { get; set; }
    public string? PublishInfo { get; set; }
    public string? PublishError { get; set; }

    public VizOverride GetOverride(int vi)
    {
        if (!Overrides.TryGetValue(vi, out var o)) { o = new VizOverride(); Overrides[vi] = o; }
        return o;
    }
}

/// <summary>单个图表的视图层覆盖（纯前端，不触发后端请求）。</summary>
public sealed class VizOverride
{
    public string? Type;
    public bool? ShowLegend;
    public string? Palette;
    /// <summary>堆叠（多序列叠加，仅 bar/line 有意义）。</summary>
    public bool? Stacked;
    /// <summary>面积图（序列下方填充，仅 line 有意义）。</summary>
    public bool? Area;
    /// <summary>多轴（第 2+ 度量走右侧次级 Y 轴，解决量纲差异）。</summary>
    public bool? MultiAxis;
}
