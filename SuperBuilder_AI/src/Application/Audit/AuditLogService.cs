using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Models.Audit;

namespace SuperBuilder_AI.Services.Audit;

/// <summary>
/// 审计日志服务实现（确定性，不调 LLM）。
/// 写入：结构化记录「谁/什么/何时/结果」。
/// 查询：按租户作用域隔离（本租户 + 全局 TenantId=0 可见），并按用户/动作/实体类型/时间窗过滤。
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly SuperBIContext _ctx;

    public AuditLogService(SuperBIContext ctx) => _ctx = ctx;

    public async Task<long> LogAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        if (entry.TenantId < 0) throw new ArgumentException("tenantId 不能为负。", nameof(entry));
        if (string.IsNullOrWhiteSpace(entry.Action)) throw new ArgumentException("Action 必填。", nameof(entry));
        if (string.IsNullOrWhiteSpace(entry.EntityType)) throw new ArgumentException("EntityType 必填。", nameof(entry));

        var now = entry.Timestamp ?? DateTime.UtcNow;
        var log = new AuditLog
        {
            TenantId = entry.TenantId,
            UserId = entry.UserId,
            Actor = entry.Actor ?? "system",
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            BeforeJson = entry.BeforeJson,
            AfterJson = entry.AfterJson,
            Result = entry.Result ?? "success",
            Message = entry.Message,
            Timestamp = now,
            CreatedTime = now,
        };
        _ctx.AuditLogs.Add(log);
        await _ctx.SaveChangesAsync(ct);
        return log.Id;
    }

    public async Task<IReadOnlyList<AuditLog>> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var q = _ctx.AuditLogs.AsNoTracking().AsQueryable();

        if (query.TenantId.HasValue)
        {
            // 本租户 + 全局(0) 可见（与全局角色/权限一致）
            q = q.Where(a => a.TenantId == query.TenantId.Value || a.TenantId == 0);
        }
        if (query.UserId.HasValue)
            q = q.Where(a => a.UserId == query.UserId.Value);
        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(a => a.Action == query.Action);
        if (!string.IsNullOrWhiteSpace(query.EntityType))
            q = q.Where(a => a.EntityType == query.EntityType);
        if (query.From.HasValue)
            q = q.Where(a => a.Timestamp >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(a => a.Timestamp <= query.To.Value);

        q = q.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id);
        if (query.Limit > 0)
            q = q.Take(query.Limit);

        return await q.ToListAsync(ct);
    }
}
