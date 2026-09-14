using System;
using SuperBuilder_AI.Models.Metadata;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class ScanTelemetryTests
{
    [Fact]
    public void RichProgress_RoundTrips_StageCountsTimingAndEvents()
    {
        var telemetry = new ScanTelemetry();
        telemetry.SetStage("SyncingMetadata", "syncing", "MetadataSyncStarted");
        telemetry.Details.TablesDiscovered = 12;
        telemetry.Details.TablesProcessed = 5;
        telemetry.Details.ColumnsDiscovered = 120;
        telemetry.Details.ColumnsProcessed = 48;
        telemetry.Details.AddedTables = 2;
        telemetry.Details.UpdatedTables = 3;
        telemetry.SetCurrent("Table", "wms_receipt");
        telemetry.UpdateTiming(DateTime.UtcNow.AddSeconds(-10), 50);

        var restored = ScanTelemetry.FromJson(telemetry.ToJson());

        Assert.Equal("syncing", restored.StageMessage);
        Assert.Equal(12, restored.TablesDiscovered);
        Assert.Equal(5, restored.TablesProcessed);
        Assert.Equal(120, restored.ColumnsDiscovered);
        Assert.Equal(48, restored.ColumnsProcessed);
        Assert.Equal(2, restored.AddedTables);
        Assert.Equal(3, restored.UpdatedTables);
        Assert.Equal("Table", restored.CurrentObjectType);
        Assert.Equal("wms_receipt", restored.CurrentObjectName);
        Assert.True(restored.ElapsedMs >= 9000);
        Assert.True(restored.EstimatedRemainingMs >= 0);
        Assert.Contains(restored.Events, e => e.EventCode == "MetadataSyncStarted");
    }

    [Fact]
    public void RichProgress_KeepsOnlyLatestTwentyEvents()
    {
        var telemetry = new ScanTelemetry();

        for (var i = 0; i < 25; i++)
            telemetry.AddEvent("Info", "E" + i, "event " + i);

        Assert.Equal(20, telemetry.Details.Events.Count);
        Assert.Equal("E5", telemetry.Details.Events[0].EventCode);
        Assert.Equal("E24", telemetry.Details.Events[^1].EventCode);
    }

    [Fact]
    public void RichProgress_InvalidHistoricalJson_FallsBackToEmptySnapshot()
    {
        var restored = ScanTelemetry.FromJson("{not-json");

        Assert.Equal(0, restored.TablesDiscovered);
        Assert.Empty(restored.Events);
    }
}
