using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.AppBuilder;
using SuperBuilder_AI.Models.Components;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.Components;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// M7-09 租户自定义组件资产库。所有 DSL 在落库前通过结构白名单与 HTML/脚本红线校验；
/// Render 只返回已发布的强类型结构，不返回可执行 HTML。
/// </summary>
[ApiController]
[Route("api/components")]
public sealed class ComponentsController : ControllerBase
{
    private readonly SuperBIContext _db;
    private readonly CustomComponentDslSerializer _serializer;

    public ComponentsController(SuperBIContext db, CustomComponentDslSerializer serializer)
    {
        _db = db;
        _serializer = serializer;
    }

    private long ScopeTo(long requestedTenantId)
    {
        var resolution = TenantDataPlanePolicy.ResolvePlatformScope(User, requestedTenantId);
        TenantDataPlanePolicy.StorePlatformScope(HttpContext, resolution, "CustomComponent");
        if (!resolution.Authorized || resolution.EffectiveTenantId <= 0)
            throw new SuperBuilderException(ErrorCodes.TenantIsolated, "禁止：自定义组件只能访问认证租户的数据。", 403);
        _db.ApplyTenantScope(resolution.EffectiveTenantId);
        return resolution.EffectiveTenantId;
    }

    private IActionResult? Require(string permission) =>
        User.Identity?.IsAuthenticated == true && !User.HasClaim("perm", permission)
            ? StatusCode(403, new ApiError { Code = ErrorCodes.Forbidden, Message = $"禁止：缺少 {permission} 权限。" })
            : null;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (Require(IdentityPermissions.AppView) is { } denied) return denied;
        ScopeTo(tenantId);
        var rows = await _db.CustomComponents.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => ToSummary(x))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, [FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (Require(IdentityPermissions.AppView) is { } denied) return denied;
        ScopeTo(tenantId);
        var entity = await _db.CustomComponents.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct);
        return entity is null ? NotFound() : Ok(ToDetail(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveComponentRequest request, CancellationToken ct = default)
    {
        if (Require(IdentityPermissions.AppCreate) is { } denied) return denied;
        if (!_serializer.TryDeserialize(request.DslJson, out var dsl, out var errors) || dsl is null)
            return BadRequest(new { errors });
        var tenantId = ScopeTo(request.TenantId);
        if (await _db.CustomComponents.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.Key == dsl.Key, ct))
            return Conflict(new { errors = new[] { $"组件 Key 已存在：{dsl.Key}。" } });

        var entity = new CustomComponentDefinition
        {
            TenantId = tenantId,
            Key = dsl.Key,
            Name = dsl.Name.Trim(),
            Description = dsl.Description,
            ComponentType = dsl.Root.Type,
            DslVersion = dsl.Version,
            DslJson = _serializer.Serialize(dsl),
        };
        _db.CustomComponents.Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { key = entity.Key, tenantId }, ToDetail(entity));
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] SaveComponentRequest request, [FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (Require(IdentityPermissions.AppEdit) is { } denied) return denied;
        if (!_serializer.TryDeserialize(request.DslJson, out var dsl, out var errors) || dsl is null)
            return BadRequest(new { errors });
        ScopeTo(tenantId);
        if (!string.Equals(key, dsl.Key, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { errors = new[] { "组件 Key 创建后不可修改，DSL Key 必须与路由一致。" } });
        var entity = await _db.CustomComponents.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (entity is null) return NotFound();

        entity.Name = dsl.Name.Trim();
        entity.Description = dsl.Description;
        entity.ComponentType = dsl.Root.Type;
        entity.DslVersion = dsl.Version;
        entity.DslJson = _serializer.Serialize(dsl);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDetail(entity));
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key, [FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (Require(IdentityPermissions.AppDelete) is { } denied) return denied;
        ScopeTo(tenantId);
        var entity = await _db.CustomComponents.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (entity is null) return NotFound();
        _db.CustomComponents.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{key}/publish")]
    public async Task<IActionResult> Publish(string key, [FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (Require(IdentityPermissions.AppPublish) is { } denied) return denied;
        var tid = ScopeTo(tenantId);
        var entity = await _db.CustomComponents.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (entity is null) return NotFound();
        if (!_serializer.TryDeserialize(entity.DslJson, out _, out var errors))
            return BadRequest(new { errors });

        entity.PublishedDslJson = entity.DslJson;
        entity.PublishedVersion += 1;
        entity.PublishedAt = DateTime.UtcNow;
        entity.PublishedBy = Actor();
        _db.CustomComponentVersions.Add(Snapshot(entity, tid));
        await _db.SaveChangesAsync(ct);
        return Ok(new ComponentPublishResult(entity.Id, entity.PublishedVersion, entity.PublishedAt.Value, entity.PublishedBy));
    }

    [HttpPost("{key}/rollback/{version:int}")]
    public async Task<IActionResult> Rollback(string key, int version, [FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (Require(IdentityPermissions.AppPublish) is { } denied) return denied;
        var tid = ScopeTo(tenantId);
        var entity = await _db.CustomComponents.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (entity is null) return NotFound();
        var target = await _db.CustomComponentVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ComponentId == entity.Id && x.Version == version, ct);
        if (target is null) return NotFound(new { errors = new[] { $"组件版本 {version} 不存在。" } });

        entity.PublishedDslJson = target.DslJson;
        entity.PublishedVersion += 1;
        entity.PublishedAt = DateTime.UtcNow;
        entity.PublishedBy = Actor();
        var snapshot = Snapshot(entity, tid);
        snapshot.Name = target.Name;
        snapshot.Description = target.Description;
        snapshot.ComponentType = target.ComponentType;
        snapshot.DslVersion = target.DslVersion;
        snapshot.DslJson = target.DslJson;
        snapshot.RolledBackFromVersion = target.Version;
        _db.CustomComponentVersions.Add(snapshot);
        await _db.SaveChangesAsync(ct);
        return Ok(new ComponentPublishResult(entity.Id, entity.PublishedVersion, entity.PublishedAt.Value, entity.PublishedBy, target.Version));
    }

    [HttpGet("{key}/versions")]
    public async Task<IActionResult> Versions(string key, [FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (Require(IdentityPermissions.AppView) is { } denied) return denied;
        ScopeTo(tenantId);
        var entity = await _db.CustomComponents.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct);
        if (entity is null) return NotFound();
        var rows = await _db.CustomComponentVersions.AsNoTracking()
            .Where(x => x.ComponentId == entity.Id)
            .OrderByDescending(x => x.Version)
            .Select(x => new ComponentVersionSummary(x.Version, x.PublishedAt, x.PublishedBy, x.RolledBackFromVersion, x.Version == entity.PublishedVersion))
            .ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>安全渲染契约：只返回已发布且重新校验通过的强类型组件结构，绝不返回 HTML。</summary>
    [HttpGet("{key}/render")]
    public async Task<IActionResult> Render(string key, [FromQuery] long tenantId = 0, CancellationToken ct = default)
    {
        if (Require(IdentityPermissions.AppView) is { } denied) return denied;
        ScopeTo(tenantId);
        var entity = await _db.CustomComponents.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct);
        if (entity is null) return NotFound();
        if (string.IsNullOrWhiteSpace(entity.PublishedDslJson))
            return Conflict(new { errors = new[] { "组件尚未发布，不能用于运行时渲染。" } });
        if (!_serializer.TryDeserialize(entity.PublishedDslJson, out var dsl, out var errors) || dsl is null)
            return StatusCode(422, new { errors });
        return Ok(new ComponentRenderView(entity.Key, entity.PublishedVersion, dsl.Root));
    }

    [HttpGet("editor/blueprint")]
    public IActionResult Blueprint() => Ok(new ComponentBlueprint(
        CustomComponentDslVersions.Current,
        AppComponentTypes.Supported,
        _serializer.Blueprint()));

    private string Actor() => User.Identity?.Name ?? "system";

    private static CustomComponentVersion Snapshot(CustomComponentDefinition x, long tenantId) => new()
    {
        ComponentId = x.Id,
        TenantId = tenantId,
        Version = x.PublishedVersion,
        Key = x.Key,
        Name = x.Name,
        Description = x.Description,
        ComponentType = x.ComponentType,
        DslVersion = x.DslVersion,
        DslJson = x.PublishedDslJson ?? x.DslJson,
        PublishedAt = x.PublishedAt ?? DateTime.UtcNow,
        PublishedBy = x.PublishedBy,
    };

    private static ComponentSummary ToSummary(CustomComponentDefinition x) =>
        new(x.Id, x.TenantId, x.Key, x.Name, x.Description, x.ComponentType, x.DslVersion, x.PublishedVersion, x.PublishedAt);

    private static ComponentDetail ToDetail(CustomComponentDefinition x) =>
        new(x.Id, x.TenantId, x.Key, x.Name, x.Description, x.ComponentType, x.DslVersion, x.DslJson, x.PublishedVersion, x.PublishedAt);

    public sealed record SaveComponentRequest(long TenantId, string DslJson);
    public sealed record ComponentSummary(long Id, long TenantId, string Key, string Name, string? Description, string ComponentType, string DslVersion, int PublishedVersion, DateTime? PublishedAt);
    public sealed record ComponentDetail(long Id, long TenantId, string Key, string Name, string? Description, string ComponentType, string DslVersion, string DslJson, int PublishedVersion, DateTime? PublishedAt);
    public sealed record ComponentPublishResult(long ComponentId, int Version, DateTime PublishedAt, string? PublishedBy, int? RolledBackFromVersion = null);
    public sealed record ComponentVersionSummary(int Version, DateTime PublishedAt, string? PublishedBy, int? RolledBackFromVersion, bool IsCurrent);
    public sealed record ComponentRenderView(string Key, int Version, ComponentPlan Root);
    public sealed record ComponentBlueprint(string DslVersion, IReadOnlyList<string> ComponentTypes, string Skeleton);
}
