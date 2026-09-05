using System.Collections.Generic;

namespace SuperBuilder_AI.Interfaces.Seed;

/// <summary>
/// 演示数据安装器端口（M2-07 / DEC-05：独立安装器，不进生产默认种子）。
/// 安装器创建独立的演示租户及示例业务数据，支持预览、事务回滚与重复执行保护。
/// </summary>
public interface IDemoDataInstaller
{
    /// <summary>预览将创建的内容（只读，不写入）。</summary>
    Task<DemoInstallPlan> PreviewAsync(CancellationToken ct = default);

    /// <summary>
    /// 安装演示数据。已安装时幂等返回（不重复创建）。
    /// 任何失败均回滚整个事务并返回 Status="failed"。
    /// </summary>
    Task<DemoInstallResult> InstallAsync(CancellationToken ct = default);
}

/// <summary>预览项：一类将创建的实体及其数量/说明。</summary>
public sealed record DemoPlanItem(string EntityType, int Count, string? Description = null);

/// <summary>演示数据安装预览计划。</summary>
public sealed record DemoInstallPlan(
    bool AlreadyInstalled,
    string DemoTenantCode,
    string DemoTenantName,
    string AdminUsername,
    string AdminEmail,
    IReadOnlyList<DemoPlanItem> Items);

/// <summary>演示数据安装结果。</summary>
public sealed record DemoInstallResult(
    bool Success,
    string Status,
    long TenantId = 0,
    long UserId = 0,
    string? Username = null,
    string? AdminPassword = null,
    string? Error = null);
