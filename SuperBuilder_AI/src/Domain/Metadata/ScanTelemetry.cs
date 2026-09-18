using System.Text.Json;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 扫描可观测遥测（M4-05 rich progress）。
/// 后台扫描过程只维护这一份内存快照；HostedService 将其序列化到 MetadataScanJob.ProgressDetailsJson，
/// 供前端轮询恢复阶段、计数、ETA、差异摘要与最近事件。
/// </summary>
public class ScanTelemetry
{
    public string Stage { get; private set; } = "Queued";
    public ScanProgressDetails Details { get; } = new();

    // 兼容既有调用点。
    public int TablesScanned
    {
        get => Details.TablesProcessed;
        set => Details.TablesProcessed = value;
    }

    public int ColumnsScanned
    {
        get => Details.ColumnsProcessed;
        set => Details.ColumnsProcessed = value;
    }

    public int OrphansDetected
    {
        get => Details.OrphansRemoved;
        set => Details.OrphansRemoved = value;
    }

    public void SetStage(string stage, string message, string? eventCode = null)
    {
        var changed = !string.Equals(Stage, stage, StringComparison.Ordinal);
        Stage = stage;
        Details.StageMessage = message;
        if (changed || !string.IsNullOrWhiteSpace(eventCode))
            AddEvent("Info", eventCode ?? stage, message);
    }

    public void SetCurrent(string? objectType, string? objectName)
    {
        Details.CurrentObjectType = objectType;
        Details.CurrentObjectName = objectName;
    }

	public void AddWarning(string eventCode, string message)
	{
		Details.WarningsCount++;
		AddEvent("Warning", eventCode, message);
	}

	/// <summary>C10：成功级别事件（绿），用于阶段完成/整体完成摘要。</summary>
	public void AddSuccess(string eventCode, string message, int? current = null, int? total = null)
		=> AddEvent("Success", eventCode, message, current, total);

	/// <summary>C10：调试级别事件（灰），用于不影响结论的细粒度进度。</summary>
	public void AddDebug(string eventCode, string message, int? current = null, int? total = null)
		=> AddEvent("Debug", eventCode, message, current, total);

    public void AddEvent(string level, string eventCode, string message, int? current = null, int? total = null)
    {
        Details.Events.Add(new ScanProgressEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Level = level,
            EventCode = eventCode,
            Message = message,
            Current = current,
            Total = total
        });

        const int maxEvents = 20;
        if (Details.Events.Count > maxEvents)
            Details.Events.RemoveRange(0, Details.Events.Count - maxEvents);
    }

    public void UpdateTiming(DateTime? startedAt, int progressPercent)
    {
        if (startedAt is null) return;

        var elapsed = Math.Max(0, (long)(DateTime.UtcNow - startedAt.Value).TotalMilliseconds);
        Details.ElapsedMs = elapsed;

        if (progressPercent <= 0 || progressPercent >= 100)
        {
            Details.EstimatedRemainingMs = progressPercent >= 100 ? 0 : null;
            return;
        }

        var estimate = (long)(elapsed * ((100d - progressPercent) / progressPercent));
        // ETA 仅作为用户提示，避免异常早期样本得到超大值。
        Details.EstimatedRemainingMs = Math.Clamp(estimate, 0, 24L * 60 * 60 * 1000);
    }

    public string ToJson() => JsonSerializer.Serialize(Details);

    public static ScanProgressDetails FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ScanProgressDetails();
        try
        {
            return JsonSerializer.Deserialize<ScanProgressDetails>(
                       json,
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new ScanProgressDetails();
        }
        catch
        {
            return new ScanProgressDetails();
        }
    }
}

/// <summary>一次扫描任务的富进度快照。</summary>
public sealed class ScanProgressDetails
{
    public string? StageMessage { get; set; }

    public int TablesDiscovered { get; set; }
    public int TablesProcessed { get; set; }
    public int ColumnsDiscovered { get; set; }
    public int ColumnsProcessed { get; set; }

    public int SemanticsTotal { get; set; }
    public int SemanticsProcessed { get; set; }
    public int VectorsTotal { get; set; }
    public int VectorsProcessed { get; set; }

    public string? CurrentObjectType { get; set; }
    public string? CurrentObjectName { get; set; }

    public int AddedTables { get; set; }
    public int UpdatedTables { get; set; }
    public int AddedColumns { get; set; }
    public int UpdatedColumns { get; set; }
    public int SemanticsGenerated { get; set; }
    public int VectorsIndexed { get; set; }
    public int OrphansRemoved { get; set; }
    public int WarningsCount { get; set; }

    public long ElapsedMs { get; set; }
    public long? EstimatedRemainingMs { get; set; }

    public List<ScanProgressEvent> Events { get; set; } = new();
}

/// <summary>面向用户的扫描业务事件，不包含连接串、SQL 或密钥。</summary>
public sealed class ScanProgressEvent
{
    public DateTime TimestampUtc { get; set; }
    public string Level { get; set; } = "Info";
    public string EventCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? Current { get; set; }
    public int? Total { get; set; }
}
