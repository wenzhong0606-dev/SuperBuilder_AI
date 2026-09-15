using System.Linq;
using SuperBuilder_AI.Models.Metadata;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// 扫描遥测结构化失败项与事件上限的纯逻辑测试（无 IO 依赖）。
/// </summary>
public class MetadataScanTelemetryTests
{
    [Fact]
    public void AddFailure_RecordsStructuredItem_And_ErrorEvent()
    {
        var telemetry = new ScanTelemetry();

        telemetry.AddFailure("Table", "dbo.Orders", "VectorIndex", "向量索引未成功");

        Assert.Single(telemetry.Details.FailedItems);
        var item = telemetry.Details.FailedItems[0];
        Assert.Equal("Table", item.Scope);
        Assert.Equal("dbo.Orders", item.Name);
        Assert.Equal("VectorIndex", item.ErrorCode);
        Assert.Equal("向量索引未成功", item.Message);

        var errorEvent = telemetry.Details.Events.FirstOrDefault(e => e.Level == "Error");
        Assert.NotNull(errorEvent);
        Assert.Contains("dbo.Orders", errorEvent!.Message);
        Assert.Equal(1, telemetry.Details.WarningsCount);
    }

    [Fact]
    public void AddFailure_ColumnScope_IsDistinguishable()
    {
        var telemetry = new ScanTelemetry();

        telemetry.AddFailure("Column", "Amount", "Semantic", "业务语义未生成成功");

        var item = Assert.Single(telemetry.Details.FailedItems);
        Assert.Equal("Column", item.Scope);
        Assert.Equal("Amount", item.Name);
    }

    [Fact]
    public void AddEvent_CapsAtSixtyEntries()
    {
        var telemetry = new ScanTelemetry();
        for (var i = 0; i < 80; i++)
            telemetry.AddEvent("Info", "T" + i, "m");

        Assert.Equal(60, telemetry.Details.Events.Count);
    }
}
