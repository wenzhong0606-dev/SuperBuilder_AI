using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SuperBuilder_AI.Data;
using Xunit;
using Xunit.Abstractions;

namespace SuperBuilder_AI.Tests.Persistence;

/// <summary>
/// DB-02 子步 A：在模型构建/首次使用时捕获 EF Core 诊断告警，
/// 重点确认是否存在「required-navigation + global query filter」类的 EF 10622 风险告警。
/// 同时作为永久回归护栏：一旦模型配置引入该告警即失败。
/// </summary>
public sealed class SuperBIContextModelWarningTests
{
    private readonly ITestOutputHelper _output;
    public SuperBIContextModelWarningTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ModelBuild_NoRequiredNavigationQueryFilterWarnings()
    {
        var warnings = new ConcurrentBag<string>();
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>()
            .UseSqlite(connection)
            .UseLoggerFactory(new CaptureLoggerFactory(warnings))
            .Options;

        using (var ctx = new SuperBIContext(options))
        {
            // 仅强制模型构建与最终化（不执行 DDL/查询，规避 SQLite 对 varchar(max) 列类型的语法限制），
            // 捕获模型期诊断告警（EF 10622 族在模型最终化阶段产生）。
            var model = ctx.Model;
            _ = model.GetEntityTypes().Count();
        }

        var ordered = warnings.OrderBy(x => x).ToList();
        foreach (var w in ordered)
            _output.WriteLine("EF-WARN: " + w);

        // 目标断言：required-navigation + global query filter (EF 10622 族)
        var offending = ordered
            .Where(w => w.Contains("query filter", StringComparison.OrdinalIgnoreCase)
                     && (w.Contains("navigation", StringComparison.OrdinalIgnoreCase)
                         || w.Contains("required", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(offending);
    }

    private sealed class CaptureLoggerFactory : ILoggerFactory
    {
        private readonly ConcurrentBag<string> _sink;
        public CaptureLoggerFactory(ConcurrentBag<string> sink) => _sink = sink;
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, _sink);
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
    }

    private sealed class CaptureLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentBag<string> _sink;
        public CaptureLogger(string category, ConcurrentBag<string> sink)
        {
            _category = category;
            _sink = sink;
        }
        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                _sink.Add($"[{eventId.Id}] {_category}: {formatter(state, exception)}");
        }
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
