using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.Audit;

namespace SuperBuilder_AI.Interfaces.Audit;

/// <summary>审计日志服务端口（确定性，不调 LLM）。</summary>
public interface IAuditLogService
{
    /// <summary>记录一条审计事件；tenantId 必须 ≥ 0（0 表示平台级）。返回新行 Id。</summary>
    Task<long> LogAsync(AuditLogEntry entry, CancellationToken ct = default);

    /// <summary>
    /// 按条件查询审计日志。租户作用域内可见本租户 + 全局 TenantId=0；
    /// 若 query.TenantId 为空，则返回全部（平台级查看）。
    /// </summary>
    Task<IReadOnlyList<AuditLog>> QueryAsync(AuditLogQuery query, CancellationToken ct = default);
}

/// <summary>审计写入输入 DTO。</summary>
public sealed record AuditLogEntry(
    long TenantId,
    string Action,
    string EntityType,
    long? UserId = null,
    string Actor = "system",
    string? EntityId = null,
    string? BeforeJson = null,
    string? AfterJson = null,
    string Result = "success",
    string? Message = null,
    DateTime? Timestamp = null);

/// <summary>审计查询条件（按用户/动作/实体类型/时间窗过滤，时间倒序取前 Limit 条）。</summary>
public sealed record AuditLogQuery(
    long? TenantId = null,
    long? UserId = null,
    string? Action = null,
    string? EntityType = null,
    DateTime? From = null,
    DateTime? To = null,
    int Limit = 100);
