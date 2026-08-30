using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Models.Audit;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 审计日志端点（P10.3 Enterprise / SaaS 治理面）。
/// <list type="bullet">
/// <item><c>GET /api/audit/logs</c>：租户作用域查询审计日志（按时间倒序，支持 action/entityType/limit 过滤）。</item>
/// <item><c>POST /api/audit/logs</c>：手动记录一条审计事件（默认路径，确定性、不调 LLM），供业务方/测试调用。</item>
/// </list>
/// 自动请求级审计由 <c>AuditMiddleware</c> 写入，本控制器不重复记录。
/// 本控制器属平台治理面，不触碰 BI 查询链路与 Golden 契约数据。
/// </summary>
[ApiController]
[Route("api/audit")]
public sealed class AuditController : ControllerBase
{
    private readonly SuperBIContext _db;
    private readonly IAuditLogService _audit;

    public AuditController(SuperBIContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>在当前请求作用域内开启租户隔离，返回解析出的租户 Id。</summary>
    private long ScopeTo(long requestedTenantId)
    {
        var tenantId = requestedTenantId > 0 ? requestedTenantId : 0;
        _db.ApplyTenantScope(tenantId);
        return tenantId;
    }

    /// <summary>租户作用域查询审计日志（按时间倒序）。</summary>
    [HttpGet("logs")]
    public async Task<IActionResult> ListLogs(
        [FromQuery] long tenantId = 0,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ScopeTo(tenantId);
        var query = new AuditLogQuery(TenantId: tenantId, Action: action, EntityType: entityType, Limit: limit);
        var items = await _audit.QueryAsync(query, cancellationToken);
        return Ok(items.Select(ToSummary).ToList());
    }

    /// <summary>手动记录一条审计事件（默认路径，确定性、不调 LLM）。</summary>
    [HttpPost("logs")]
    public async Task<IActionResult> Record(
        [FromBody] RecordAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null) return BadRequest("请求体不能为空。");
        if (request.TenantId < 0) return BadRequest("tenantId 不能为负。");
        if (string.IsNullOrWhiteSpace(request.Action)) return BadRequest("action 必填。");
        if (string.IsNullOrWhiteSpace(request.EntityType)) return BadRequest("entityType 必填。");

        var entry = new AuditLogEntry(
            TenantId: request.TenantId,
            Action: request.Action,
            EntityType: request.EntityType,
            UserId: request.UserId,
            Actor: request.Actor ?? "system",
            EntityId: request.EntityId,
            BeforeJson: request.BeforeJson,
            AfterJson: request.AfterJson,
            Result: request.Result ?? "success",
            Message: request.Message,
            Timestamp: request.Timestamp);

        var id = await _audit.LogAsync(entry, cancellationToken);
        var ts = request.Timestamp ?? DateTime.UtcNow;
        return CreatedAtAction(
            nameof(ListLogs),
            new { tenantId = request.TenantId },
            new AuditLogSummary(id, request.TenantId, request.Action, request.EntityType, request.Actor ?? "system", request.Result ?? "success", ts, request.Message));
    }

    private static AuditLogSummary ToSummary(AuditLog a) =>
        new(a.Id, a.TenantId, a.Action, a.EntityType, a.Actor, a.Result, a.Timestamp, a.Message);

    #region DTOs
    /// <summary>手动记录审计请求体（默认路径，确定性、不调 LLM）。</summary>
    public sealed record RecordAuditRequest(
        long TenantId,
        string Action,
        string EntityType,
        long? UserId = null,
        string? Actor = null,
        string? EntityId = null,
        string? BeforeJson = null,
        string? AfterJson = null,
        string? Result = null,
        string? Message = null,
        DateTime? Timestamp = null);

    /// <summary>审计日志摘要 DTO。</summary>
    public sealed record AuditLogSummary(
        long Id,
        long TenantId,
        string Action,
        string EntityType,
        string Actor,
        string Result,
        DateTime Timestamp,
        string? Message);
    #endregion
}
