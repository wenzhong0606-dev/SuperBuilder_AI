using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Services.Audit;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P10.3 AuditController 单测（SQLite 内存库 + 真实 AuditLogService，不依赖 Moq）。
/// 覆盖：手动记录返回 201、租户作用域查询隔离、按 action 过滤。
/// </summary>
public class AuditControllerTests
{
    private const long Tenant7 = 7;
    private const long Tenant8 = 8;

    private static SuperBIContext CreateContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        var ctx = new SuperBIContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static AuditController Build(SuperBIContext db)
    {
        var svc = new AuditLogService(db);
        return new AuditController(db, svc);
    }

    [Fact]
    public async Task Record_Returns_201_With_Summary()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;

        var ctrl = Build(ctx);
        var result = await ctrl.Record(
            new AuditController.RecordAuditRequest(Tenant7, "user.create", "User", Actor: "alice"),
            CancellationToken.None) as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;

        Assert.NotNull(result);
        Assert.Equal((int)HttpStatusCode.Created, result!.StatusCode);
        var summary = Assert.IsType<AuditController.AuditLogSummary>(result.Value);
        Assert.Equal(Tenant7, summary.TenantId);
        Assert.Equal("user.create", summary.Action);
    }

    [Fact]
    public async Task Record_Requires_Action()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;

        var ctrl = Build(ctx);
        var result = await ctrl.Record(
            new AuditController.RecordAuditRequest(Tenant7, "", "User"),
            CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
        Assert.NotNull(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, result!.StatusCode);
    }

    [Fact]
    public async Task ListLogs_TenantScoped_OnlyOwn_NotPlatformGlobal()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;

        var ctrl = Build(ctx);
        await ctrl.Record(new AuditController.RecordAuditRequest(Tenant7, "user.create", "User"), CancellationToken.None);
        await ctrl.Record(new AuditController.RecordAuditRequest(Tenant8, "user.create", "User"), CancellationToken.None);
        await ctrl.Record(new AuditController.RecordAuditRequest(0, "system.boot", "System"), CancellationToken.None);

        var result = await ctrl.ListLogs(Tenant7, cancellationToken: CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(result);
        var logs = Assert.IsAssignableFrom<IEnumerable<AuditController.AuditLogSummary>>(result!.Value).ToList();
		Assert.Single(logs);
		Assert.Equal(Tenant7, logs[0].TenantId);
    }

    [Fact]
    public async Task ListLogs_Filter_By_Action()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;

        var ctrl = Build(ctx);
        await ctrl.Record(new AuditController.RecordAuditRequest(Tenant7, "user.create", "User"), CancellationToken.None);
        await ctrl.Record(new AuditController.RecordAuditRequest(Tenant7, "role.assign", "Role"), CancellationToken.None);

        var result = await ctrl.ListLogs(Tenant7, action: "role.assign", cancellationToken: CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(result);
        var logs = Assert.IsAssignableFrom<IEnumerable<AuditController.AuditLogSummary>>(result!.Value).ToList();
        Assert.Single(logs);
        Assert.Equal("role.assign", logs[0].Action);
    }
}
