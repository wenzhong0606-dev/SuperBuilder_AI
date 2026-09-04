using System.Linq;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;

namespace SuperBuilder_AI.Api.Diagnostics;

/// <summary>
/// 启动期 Schema 探测：区分「数据库不可达 / Schema 未创建 / 就绪」三种可诊断状态，
/// 可选在启动时应用 EF 迁移（默认由 CI/部署工具迁移，见 <see cref="Startup:MigrateOnStartup"/>）。
/// 各步骤均使用宽松异常捕获，避免特定数据库提供程序的异常类型导致探测本身崩溃。
/// </summary>
public static class SchemaProbe
{
    public static async Task<(BootstrapState State, string? Reason)> ProbeAsync(
        SuperBIContext db, bool migrateOnStartup, CancellationToken ct = default)
    {
        // 1) 数据库可达性：连接失败或 CanConnect 返回 false 均视为不可达
        try
        {
            if (!await db.Database.CanConnectAsync(ct))
                return (BootstrapState.DatabaseUnreachable, "无法连接到数据库（CanConnect 返回 false）。");
        }
        catch (Exception ex)
        {
            return (BootstrapState.DatabaseUnreachable, Format(ex));
        }

        // 2) 可选：启动期迁移
        if (migrateOnStartup)
        {
            try
            {
                await db.Database.MigrateAsync(ct);
            }
            catch (Exception ex)
            {
                return (BootstrapState.SchemaNotCreated, $"启动迁移失败：{Format(ex)}");
            }
        }

        // 3) 迁移历史是否存在/非空（读取失败即视为架构未创建）
        try
        {
            var applied = await db.Database.GetAppliedMigrationsAsync(ct);
            if (applied is null || !applied.Any())
                return (BootstrapState.SchemaNotCreated, "尚未应用任何数据库迁移（__EFMigrationsHistory 为空或不存在）。");
        }
        catch (Exception ex)
        {
            return (BootstrapState.SchemaNotCreated, $"无法读取迁移历史，架构可能未创建：{Format(ex)}");
        }

        return (BootstrapState.Ready, null);
    }

    private static string Format(Exception ex) =>
        ex is System.Data.Common.DbException dbEx
            ? $"{dbEx.GetType().Name}: {dbEx.Message}"
            : $"{ex.GetType().Name}: {ex.Message}";
}
