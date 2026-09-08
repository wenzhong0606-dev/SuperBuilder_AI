using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M5-11 AI BI E2E：NL→API→Plan→SQL→Test DB→Result 全链覆盖。
///
/// 设计原则（与 M5 各里程碑一致的零回归/确定性约束）：
/// 1. 不依赖实时 LLM（Qwen）——用确定性桩隔离 NL 理解与结果分析的不确定性；
///    明细列表场景经 ResultUnderstandingService 的确定性分支，对 LLM 完全免疫。
/// 2. 不依赖向量/Metadata 检索——用确定性 IQueryPlanBuilder 桩提供 Plan，
///    注入的 QueryPlanPipeline 仅含 BuildStage，不触发 metadata validator（其已由 M5 各单测覆盖）。
/// 3. Plan→SQL→Test DB→Result 全部为真实组件：真实 SqlQueryBuilder + 真实 QueryExecutionService
///    （自定义 SQLite 内存连接工厂，与 SuperBIContext 共享同一文件库）+ 真实 ResultUnderstandingService。
/// 4. 用文件型 SQLite 临时库（EF 与 Dapper 共享同一数据源），离线可还原、无跨网络依赖。
///
/// 覆盖三档：
///  - E2E-1：Plan→SQL→Test DB→Result 真实执行闭环（核心最后一公里）。
///  - E2E-2：NL→Intent→Plan 确定性映射。
///  - E2E-3：NL→API（BIConversationService）→Plan→SQL→Test DB→Result 全链贯通。
/// </summary>
public class QueryPlanAiBiE2ETests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connStr;
    private readonly SuperBIContext _db;

    private const long TenantId = 1;
    private const long DataSourceId = 1;

    public QueryPlanAiBiE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sb_e2e_{Guid.NewGuid():N}.db");
        // Pooling=false：关闭即释放文件句柄，避免连接池持有的句柄阻止 Dispose 阶段删除临时库。
        _connStr = $"Data Source={_dbPath};Pooling=false";
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(_connStr).Options;
        _db = new SuperBIContext(options);
        _db.Database.EnsureCreated();

        // 种子租户 + 数据源（供 BIConversationService 在 SQL 生成前按 Id/Tenant/Enabled 查询）。
        _db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            TenantCode = "e2e",
            TenantName = "E2E Tenant",
            Enabled = true,
            CreatedTime = DateTime.UtcNow
        });
        _db.DataSources.Add(new DataSource
        {
            Id = DataSourceId,
            TenantId = TenantId,
            Name = "E2E Test DS",
            NormalizedName = "e2e test ds",
            DbType = "POSTGRESQL",
            ConnectionString = "placeholder",
            Enabled = true,
            CreatedTime = DateTime.UtcNow
        });
        _db.SaveChanges();

        // 建业务表并插入 15 行不同 come_time 的数据（Dapper 直接执行，非 EF 实体，不受租户过滤影响）。
        using var conn = new SqliteConnection(_connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE WarehouseReceipts (
                id INTEGER PRIMARY KEY,
                receipt_no TEXT,
                come_time TEXT,
                qty INTEGER,
                supplier TEXT,
                status TEXT
            );";
        cmd.ExecuteNonQuery();

        var rnd = new Random(42);
        for (var i = 1; i <= 15; i++)
        {
            // i 越大时间越新：用于验证 LIMIT 10 后按 come_time 倒序取最新十条。
            var t = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified).AddHours(i);
            cmd.CommandText = "INSERT INTO WarehouseReceipts (id, receipt_no, come_time, qty, supplier, status) " +
                              "VALUES (@id, @no, @t, @q, @s, @st)";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@id", i));
            cmd.Parameters.Add(new SqliteParameter("@no", $"R{1000 + i}"));
            cmd.Parameters.Add(new SqliteParameter("@t", t.ToString("yyyy-MM-dd HH:mm:ss")));
            cmd.Parameters.Add(new SqliteParameter("@q", rnd.Next(1, 50)));
            cmd.Parameters.Add(new SqliteParameter("@s", $"SUP{i % 3}"));
            cmd.Parameters.Add(new SqliteParameter("@st", i % 2 == 0 ? "done" : "pending"));
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        // 强制释放任何仍被池持有的连接句柄（即便 Pooling=false，亦作为防御性兜底）。
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 自定义数据源连接工厂：返回指向同一文件库的 SQLite 连接，
    /// 使 Dapper 执行的 SQL 能访问 SuperBIContext 已建表与种子化数据。
    /// </summary>
    private sealed class FileConnectionFactory : IDataSourceConnectionFactory
    {
        private readonly string _connStr;
        public FileConnectionFactory(string connStr) => _connStr = connStr;
        public Task<DbConnection> CreateAsync(long dataSourceId)
            => Task.FromResult<DbConnection>(new SqliteConnection(_connStr));
    }

    /// <summary>确定性 LLM 桩：仅用于非明细分支兜底，明细列表走确定性分支不会调用。</summary>
    private sealed class FakeQwen : IQwenService
    {
        public Task<string> GenerateSqlAsync(string prompt)
            => Task.FromResult("{\"answer\":\"（确定性占位）已分析数据。\",\"summary\":{},\"visualizations\":[]}");
    }

    /// <summary>确定性 NL 理解桩：将“最近的十张入库凭证”映射为明细 TopN 意图。</summary>
    private sealed class FakeUnderstanding : IQueryUnderstandingService
    {
        public Task<QueryIntent> UnderstandAsync(string question)
            => Task.FromResult(BuildIntent(question));
        public Task<QueryIntent> UnderstandAsync(string question, PlatformContext platformContext)
            => Task.FromResult(BuildIntent(question));

        private static QueryIntent BuildIntent(string question) => new()
        {
            OriginalQuestion = question,
            IntentType = "Detail",
            OrderBy = "come_time",
            OrderDirection = "DESC",
            Limit = 10
        };
    }

    /// <summary>
    /// 确定性 Plan 构建桩：绕过 Metadata 向量检索，直接产出与种子化业务表一致的 QueryPlan。
    /// 供注入的 QueryPlanPipeline（仅 BuildStage）消费，不触发 metadata validator。
    /// </summary>
    private sealed class FakePlanBuilder : IQueryPlanBuilder
    {
        public Task<QueryPlan> BuildAsync(
            QueryIntent intent,
            long? requestedDataSourceId = null,
            IReadOnlyCollection<long>? authorizedDataSourceIds = null)
            => Task.FromResult(BuildPlan(intent, requestedDataSourceId));

        public Task<QueryPlan> BuildAsync(
            QueryIntent intent,
            QueryPlanSemanticResolution? resolution)
            => Task.FromResult(BuildPlan(intent, null));

        public static QueryPlan SamplePlan(long? dsId = null)
        {
            var intent = new QueryIntent
            {
                OriginalQuestion = "最近的十张入库凭证",
                IntentType = "Detail",
                OrderBy = "come_time",
                OrderDirection = "DESC",
                Limit = 10
            };
            return BuildPlan(intent, dsId);
        }

        private static QueryPlan BuildPlan(QueryIntent intent, long? dsId)
        {
            var plan = new QueryPlan
            {
                Intent = intent,
                DataSourceId = dsId ?? DataSourceId,
                Limit = intent.Limit,
                IsAggregate = false
            };
            plan.Tables.Add(new QueryTable
            {
                MetadataTableId = 1,
                DataSourceId = dsId ?? DataSourceId,
                TableName = "WarehouseReceipts"
            });
            plan.Fields.Add(new QueryField { MetadataColumnId = 1, ColumnName = "receipt_no" });
            plan.Fields.Add(new QueryField { MetadataColumnId = 2, ColumnName = "come_time" });
            plan.Fields.Add(new QueryField { MetadataColumnId = 3, ColumnName = "qty" });
            plan.Fields.Add(new QueryField { MetadataColumnId = 4, ColumnName = "supplier" });
            plan.Orders.Add(new QueryOrder { MetadataColumnId = 2, Field = "come_time", Direction = "DESC" });
            return plan;
        }
    }

    private sealed class Components
    {
        public BIConversationService Service = null!;
        public SqlQueryBuilder SqlBuilder = null!;
        public QueryExecutionService Execution = null!;
        public ResultUnderstandingService Result = null!;
    }

    private Components BuildComponents()
    {
        var understanding = new FakeUnderstanding();
        var planBuilder = new FakePlanBuilder();
        var pipeline = new QueryPlanPipeline(
            new IQueryPlanStage[] { new QueryPlanBuildStage(planBuilder) },
            new NoOpDecisionAuditSink());
        var sqlBuilder = new SqlQueryBuilder();
        var dialectResolver = new SqlDialectResolver(new List<ISqlDialect> { new PostgreSqlDialect() });
        var execution = new QueryExecutionService(new FileConnectionFactory(_connStr));
        var result = new ResultUnderstandingService(new FakeQwen());
        var service = new BIConversationService(
            understanding, pipeline, sqlBuilder, dialectResolver, _db, execution, result);
        return new Components
        {
            Service = service,
            SqlBuilder = sqlBuilder,
            Execution = execution,
            Result = result
        };
    }

    [Fact]
    public async Task E2E_Plan_To_Sql_To_TestDb_To_Result_RealExecution()
    {
        var components = BuildComponents();
        var dialect = new PostgreSqlDialect();
        var plan = FakePlanBuilder.SamplePlan();

        // Plan → SQL（真实 SqlQueryBuilder，PostgreSQL 方言生成 "col" + LIMIT n，SQLite 兼容）
        var sqlQuery = await components.SqlBuilder.BuildAsync(plan, dialect);

        // SQL → Test DB（真实 QueryExecutionService + Dapper，SQLite 内存连接）
        var data = await components.Execution.ExecuteAsync(sqlQuery, DataSourceId);

        // Test DB → Result（真实 ResultUnderstandingService，明细列表走确定性分支）
        var answer = await components.Result.AnalyzeAsync("最近的十张入库凭证", data, plan);

        Assert.Contains("\"WarehouseReceipts\"", sqlQuery.Sql);
        Assert.Contains("\"come_time\"", sqlQuery.Sql);
        Assert.Contains("ORDER BY", sqlQuery.Sql);
        Assert.Contains("LIMIT 10", sqlQuery.Sql);

        Assert.True(data.Success, $"DB 执行失败：{data.ErrorMessage}");
        Assert.Equal(10, data.Rows.Count);

        // 验证按 come_time 倒序（ISO 字符串字典序即时间序）
        var times = data.Rows.Select(r => (string)r["come_time"]!).ToList();
        for (var i = 1; i < times.Count; i++)
            Assert.True(string.Compare(times[i - 1], times[i], StringComparison.Ordinal) >= 0,
                $"行 {i - 1} 的时间应 >= 行 {i} 的时间（倒序）");

        Assert.True(answer.Success);
        Assert.False(string.IsNullOrEmpty(answer.Answer));
    }

    [Fact]
    public async Task E2E_NL_To_Plan_Deterministic()
    {
        var understanding = new FakeUnderstanding();
        var planBuilder = new FakePlanBuilder();

        var intent = await understanding.UnderstandAsync("最近的十张入库凭证");
        var plan = await planBuilder.BuildAsync(intent, DataSourceId);

        Assert.Equal("Detail", intent.IntentType);
        Assert.Equal(10, intent.Limit);
        Assert.Equal("come_time", intent.OrderBy);
        Assert.Equal("DESC", intent.OrderDirection);

        Assert.Equal("WarehouseReceipts", plan.Tables[0].TableName);
        Assert.Equal(DataSourceId, plan.DataSourceId);
        Assert.Equal(10, plan.Limit);
        Assert.False(plan.IsAggregate);
        Assert.Equal("come_time", plan.Orders[0].Field);
        Assert.Equal("DESC", plan.Orders[0].Direction);
    }

    [Fact]
    public async Task E2E_NL_To_Api_To_Plan_To_Sql_To_Result_FullChain()
    {
        var components = BuildComponents();

        // NL → API（BIConversationService）→ Plan → SQL → Test DB → Result 全链贯通
        var response = await components.Service.ExecuteAsync(
            "最近的十张入库凭证", TenantId, requestedDataSourceId: DataSourceId);

        Assert.True(response.Success, $"BI 响应失败：{response.Explanation}");

        Assert.Contains("\"WarehouseReceipts\"", response.Sql);
        Assert.Contains("LIMIT 10", response.Sql);
        Assert.Contains("ORDER BY", response.Sql);

        Assert.NotNull(response.Data);
        Assert.True(response.Data.Success, $"DB 执行失败：{response.Data.ErrorMessage}");
        Assert.Equal(10, response.Data.Rows.Count);

        var times = response.Data.Rows.Select(r => (string)r["come_time"]!).ToList();
        for (var i = 1; i < times.Count; i++)
            Assert.True(string.Compare(times[i - 1], times[i], StringComparison.Ordinal) >= 0,
                $"行 {i - 1} 的时间应 >= 行 {i} 的时间（倒序）");

        Assert.NotNull(response.Answer);
        Assert.True(response.Answer.Success);
        Assert.False(string.IsNullOrEmpty(response.Answer.Answer));
    }
}
