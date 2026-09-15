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

    /// <summary>
    /// 记录一条结构化失败项（表/字段级），同时追加一条 Error 级业务事件，
    /// 供前端"失败数据清单"精确展示哪张表/哪个字段未成功处理。
    /// </summary>
    public void AddFailure(string scope, string name, string? errorCode, string? message)
    {
        Details.FailedItems.Add(new ScanFailedItem
        {
            Scope = scope,
            Name = name,
            ErrorCode = errorCode,
            Message = message
        });
        Details.WarningsCount++;
        AddEvent(
            "Error",
            "ItemFailed:" + scope,
            $"{scope}「{name}」失败" + (string.IsNullOrWhiteSpace(message) ? "" : $"：{message}"));
    }

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

        const int maxEvents = 60;
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

    /// <summary>结构化失败项清单（表/字段级），供前端展示未成功处理的数据。</summary>
    public List<ScanFailedItem> FailedItems { get; set; } = new();

    public long ElapsedMs { get; set; }
    public long? EstimatedRemainingMs { get; set; }

    public List<ScanProgressEvent> Events { get; set; } = new();
}

/// <summary>一条结构化失败项：标识哪类对象（表/字段）的哪一项处理未成功。</summary>
public sealed class ScanFailedItem
{
    /// <summary>失败对象类别：Table / Column。</summary>
    public string Scope { get; set; } = string.Empty;
    /// <summary>失败对象名称（表名 / 字段名）。</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>脱敏后的错误码（异常类型名或阶段标识）。</summary>
    public string? ErrorCode { get; set; }
    /// <summary>可读的失败原因（已脱敏，绝不含有连接串）。</summary>
    public string? Message { get; set; }
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
