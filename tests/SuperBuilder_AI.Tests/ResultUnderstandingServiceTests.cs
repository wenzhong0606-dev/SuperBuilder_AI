using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class ResultUnderstandingServiceTests
{
    private sealed class TrackingQwen : IQwenService
    {
        public int Calls { get; private set; }
        public string Response { get; set; } = "{}";

        public Task<string> GenerateSqlAsync(string prompt)
        {
            Calls++;
            return Task.FromResult(Response);
        }
    }

    [Fact]
    public async Task Detail_list_uses_deterministic_table_answer_without_ai_kpis_or_charts()
    {
        var qwen = new TrackingQwen();
        var service = new ResultUnderstandingService(qwen);
        var result = new QueryResult
        {
            Success = true,
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["code"] = "RK-001", ["status"] = 2, ["come_time"] = new DateTime(2026, 9, 4, 10, 0, 0) }
            }
        };
        var plan = new QueryPlan
        {
            Limit = 10,
            IsRanking = true,
            IsDetailRanking = true,
            Orders = { new QueryOrder { Field = "come_time", Direction = "DESC" } }
        };

        var answer = await service.AnalyzeAsync("最近的十个入库单", result, plan);

        Assert.True(answer.Success);
        Assert.Equal(0, qwen.Calls);
        Assert.Empty(answer.Summary);
        var view = Assert.Single(answer.Visualizations);
        Assert.Equal("table", view.Type);
        Assert.Contains("1 条", answer.Answer);
        Assert.Contains("come_time", answer.Answer);
        Assert.Contains("倒序", answer.Answer);
    }

    [Fact]
    public async Task Aggregate_answer_drops_chart_when_measure_is_datetime()
    {
        var qwen = new TrackingQwen
        {
            Response = """
            {"answer":"分析完成","summary":{},"visualizations":[
              {"type":"line","title":"错误趋势","xAxis":"status","yAxis":["come_time"],"reason":""},
              {"type":"bar","title":"有效统计","xAxis":"status","yAxis":["count"],"reason":""}
            ]}
            """
        };
        var service = new ResultUnderstandingService(qwen);
        var result = new QueryResult
        {
            Success = true,
            Rows = new List<Dictionary<string, object?>>
            {
                new() { ["status"] = "已确认", ["come_time"] = "2026-09-04T10:00:00", ["count"] = 8 },
                new() { ["status"] = "未确认", ["come_time"] = "2026-09-03T10:00:00", ["count"] = 2 }
            }
        };
        var plan = new QueryPlan { IsAggregate = true };

        var answer = await service.AnalyzeAsync("按状态统计入库单", result, plan);

        Assert.Equal(1, qwen.Calls);
        var view = Assert.Single(answer.Visualizations);
        Assert.Equal("有效统计", view.Title);
        Assert.Equal(new[] { "count" }, view.YAxis);
    }
}
