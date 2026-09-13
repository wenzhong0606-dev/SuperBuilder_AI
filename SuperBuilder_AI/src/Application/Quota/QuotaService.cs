using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Quota;
using SuperBuilder_AI.Models.Quota;

namespace SuperBuilder_AI.Services.Quota;

/// <summary>
/// 配额服务实现（确定性，不调 LLM）。
/// - 平台默认配额种子（TenantId=0），幂等。
/// - 解析上限：租户覆盖行优先，否则回退平台默认。
/// - 用量：按周期键（Total/Monthly/Daily）滚动归零。
/// - 查询/校验为只读（不创建用量行）；扣减为写路径（按需创建/滚动并持久化）。
/// </summary>
public class QuotaService : IQuotaService
{
    private readonly SuperBIContext _ctx;

    public QuotaService(SuperBIContext ctx) => _ctx = ctx;

    public async Task EnsureSeededAsync(CancellationToken ct = default)
    {
        foreach (var kv in QuotaDefaults.PlatformDefaults)
        {
            var rt = kv.Key;
            var exists = await _ctx.QuotaPolicies.AnyAsync(p => p.TenantId == 0 && p.ResourceType == rt, ct);
            if (exists) continue;
            _ctx.QuotaPolicies.Add(new QuotaPolicy
            {
                TenantId = 0,
                ResourceType = rt,
                Limit = kv.Value.Limit,
                Window = kv.Value.Window,
                CreatedTime = DateTime.UtcNow,
            });
        }
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<QuotaOverviewResponse> GetQuotaAsync(long tenantId, CancellationToken ct = default)
    {
        var items = new List<QuotaItemView>();
        var now = DateTime.UtcNow;
        foreach (var rt in QuotaDefaults.PlatformDefaults.Keys)
        {
            var platform = await ResolvePlatformLimitAsync(rt, ct);
            var tenantOverride = tenantId > 0
                ? await _ctx.QuotaPolicies.AsNoTracking()
                    .Where(p => p.TenantId == tenantId && p.ResourceType == rt)
                    .OrderByDescending(p => p.Id)
                    .FirstOrDefaultAsync(ct)
                : null;
            var limit = tenantOverride?.Limit ?? platform.Limit;
            var window = tenantOverride?.Window ?? platform.Window;
            var (used, periodKey) = tenantId > 0
                ? await ResolveUsageAsync(tenantId, rt, window, now, ct)
                : (0L, PeriodKeyFor(window, now));
            items.Add(ToView(rt, limit, used, window, periodKey, tenantOverride is not null, platform));
        }
        return new QuotaOverviewResponse(tenantId, items);
    }

    public async Task<QuotaCheckResponse> CheckAsync(long tenantId, QuotaResourceType resourceType, long requested = 1, CancellationToken ct = default)
    {
        if (requested < 0) throw new ArgumentException("requested 不能为负。", nameof(requested));
        var now = DateTime.UtcNow;
        var (limit, window) = await ResolveLimitAsync(tenantId, resourceType, ct);
        var (used, _) = await ResolveUsageAsync(tenantId, resourceType, window, now, ct);
        var remaining = Math.Max(0, limit - used);
        var allowed = used + requested <= limit;
        return new QuotaCheckResponse(allowed, limit, used, remaining);
    }

    public async Task<bool> ConsumeAsync(long tenantId, QuotaResourceType resourceType, long amount = 1, CancellationToken ct = default)
    {
        if (amount < 0) throw new ArgumentException("amount 不能为负。", nameof(amount));
        var now = DateTime.UtcNow;
        var (limit, window) = await ResolveLimitAsync(tenantId, resourceType, ct);
        var key = PeriodKeyFor(window, now);

        // 原子条件自增：单语句 UPDATE，仅当「当前周期且自增后仍不超额」时 +amount。
        // 数据库层判定，彻底消除 read-modify-write 竞态导致的超额消费（M13-18 / QUOTA-01）。
        // ExecuteUpdate 绕过变更跟踪器，成功后需 Reload 跟踪中的用量实体，避免后续读取看到陈旧快照。
        if (await AtomicIncrementAsync(tenantId, resourceType, key, amount, limit, ct) > 0)
        {
            await ReloadUsageTrackerAsync(tenantId, resourceType, ct);
            return true;
        }

        // 未命中：无行 / 周期已滚动 / 已超额。先确保当前周期行就绪（去重+滚动，幂等）。
        if (!await EnsureUsageRowAsync(tenantId, resourceType, window, now, ct))
            return false; // 行无法就绪或已超额

        // 行就绪后重试原子自增（此时 PeriodKey 必为当前周期，且门禁未超额时才成功）。
        if (await AtomicIncrementAsync(tenantId, resourceType, key, amount, limit, ct) > 0)
        {
            await ReloadUsageTrackerAsync(tenantId, resourceType, ct);
            return true;
        }
        return false;
    }

    public async Task<QuotaItemView> UpsertPolicyAsync(
        long tenantId,
        QuotaResourceType resourceType,
        long limit,
        QuotaWindow window,
        CancellationToken ct = default)
    {
        if (tenantId < 0) throw new ArgumentException("tenantId 不能为负。", nameof(tenantId));
        if (limit < 0) throw new ArgumentException("limit 不能为负。", nameof(limit));
        if (!Enum.IsDefined(resourceType)) throw new ArgumentException("未知资源类型。", nameof(resourceType));
        if (!Enum.IsDefined(window)) throw new ArgumentException("未知周期窗口。", nameof(window));
        await EnsureTenantExistsAsync(tenantId, ct);

        var policy = await _ctx.QuotaPolicies
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ResourceType == resourceType, ct);
        if (policy is null)
        {
            policy = new QuotaPolicy
            {
                TenantId = tenantId,
                ResourceType = resourceType,
                CreatedTime = DateTime.UtcNow,
            };
            _ctx.QuotaPolicies.Add(policy);
        }
        policy.Limit = limit;
        policy.Window = window;
        await _ctx.SaveChangesAsync(ct);
        return await GetItemAsync(tenantId, resourceType, ct);
    }

    public async Task<bool> RemoveTenantOverrideAsync(
        long tenantId,
        QuotaResourceType resourceType,
        CancellationToken ct = default)
    {
        if (tenantId <= 0) throw new ArgumentException("只能删除实际租户的覆盖策略。", nameof(tenantId));
        var policy = await _ctx.QuotaPolicies
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ResourceType == resourceType, ct);
        if (policy is null) return false;
        _ctx.QuotaPolicies.Remove(policy);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    public async Task<QuotaItemView> SetUsageAsync(
        long tenantId,
        QuotaResourceType resourceType,
        long used,
        CancellationToken ct = default)
    {
        if (tenantId <= 0) throw new ArgumentException("用量只能维护到实际租户。", nameof(tenantId));
        if (used < 0) throw new ArgumentException("used 不能为负。", nameof(used));
        await EnsureTenantExistsAsync(tenantId, ct);

        var now = DateTime.UtcNow;
        var (_, window) = await ResolveLimitAsync(tenantId, resourceType, ct);
        var usage = await GetOrCreateUsageAsync(tenantId, resourceType, window, now, ct);
        usage.Used = used;
        usage.PeriodKey = PeriodKeyFor(window, now);
        usage.LastReset = now;
        await _ctx.SaveChangesAsync(ct);
        return await GetItemAsync(tenantId, resourceType, ct);
    }

    #region helpers
    private async Task<(long Limit, QuotaWindow Window)> ResolveLimitAsync(long tenantId, QuotaResourceType rt, CancellationToken ct)
    {
        // 租户覆盖优先
        var overridePolicy = await _ctx.QuotaPolicies
            .Where(p => p.TenantId == tenantId && p.ResourceType == rt)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
        if (overridePolicy != null) return (overridePolicy.Limit, overridePolicy.Window);

        // 回退平台默认
        return await ResolvePlatformLimitAsync(rt, ct);
    }

    private async Task<(long Limit, QuotaWindow Window)> ResolvePlatformLimitAsync(QuotaResourceType rt, CancellationToken ct)
    {
        var defaultPolicy = await _ctx.QuotaPolicies.AsNoTracking()
            .Where(p => p.TenantId == 0 && p.ResourceType == rt)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
        if (defaultPolicy != null) return (defaultPolicy.Limit, defaultPolicy.Window);

        // 极端兜底：连种子都没有，用常量
        return QuotaDefaults.PlatformDefaults.TryGetValue(rt, out var d)
            ? d
            : (0, QuotaWindow.Total);
    }

    private async Task<QuotaItemView> GetItemAsync(long tenantId, QuotaResourceType resourceType, CancellationToken ct)
    {
        var overview = await GetQuotaAsync(tenantId, ct);
        return overview.Items.Single(x => string.Equals(x.ResourceType, resourceType.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureTenantExistsAsync(long tenantId, CancellationToken ct)
    {
        if (tenantId == 0) return;
        if (!await _ctx.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId, ct))
            throw new InvalidOperationException($"租户不存在：{tenantId}。");
    }

    private static QuotaItemView ToView(
        QuotaResourceType resourceType,
        long limit,
        long used,
        QuotaWindow window,
        string periodKey,
        bool isOverride,
        (long Limit, QuotaWindow Window) platform) =>
        new(
            resourceType.ToString(),
            limit,
            used,
            Math.Max(0, limit - used),
            window.ToString(),
            periodKey,
            isOverride,
            platform.Limit,
            platform.Window.ToString());

    // 只读解析：不创建用量行（视图上跨周期视为已归零）
    private async Task<(long Used, string PeriodKey)> ResolveUsageAsync(long tenantId, QuotaResourceType rt, QuotaWindow window, DateTime now, CancellationToken ct)
    {
        var key = PeriodKeyFor(window, now);
        var usage = await _ctx.QuotaUsages
            .Where(u => u.TenantId == tenantId && u.ResourceType == rt)
            .OrderByDescending(u => u.Id)
            .FirstOrDefaultAsync(ct);
        if (usage == null) return (0, key);
        if (usage.PeriodKey != key) return (0, key); // 周期已滚动
        return (usage.Used, usage.PeriodKey);
    }

    // 写路径：创建或滚动并持久化（插入竞态下幂等重试；供 SetUsageAsync 使用）。
    private async Task<QuotaUsage> GetOrCreateUsageAsync(long tenantId, QuotaResourceType rt, QuotaWindow window, DateTime now, CancellationToken ct)
    {
        var key = PeriodKeyFor(window, now);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var usage = await _ctx.QuotaUsages
                .Where(u => u.TenantId == tenantId && u.ResourceType == rt)
                .OrderByDescending(u => u.Id)
                .FirstOrDefaultAsync(ct);

            if (usage is null)
            {
                var created = new QuotaUsage
                {
                    TenantId = tenantId,
                    ResourceType = rt,
                    Used = 0,
                    PeriodKey = key,
                    LastReset = now,
                    CreatedTime = now,
                };
                _ctx.QuotaUsages.Add(created);
                try
                {
                    await _ctx.SaveChangesAsync(ct);
                    return created;
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    // 并发线程已先行插入：清理本次失败实体，下一轮接管既有行。
                    DetachAddedUsages();
                    await Task.Delay(20 * (attempt + 1), ct);
                    continue;
                }
            }

            if (usage.PeriodKey != key)
            {
                usage.Used = 0;
                usage.PeriodKey = key;
                usage.LastReset = now;
                await _ctx.SaveChangesAsync(ct);
            }
            return usage;
        }
        throw new InvalidOperationException("无法确保配额用量行（并发唯一冲突重试耗尽）。");
    }

    // 原子条件自增：单语句 UPDATE，仅当当前周期且自增后仍不超额时 +amount。
    private Task<int> AtomicIncrementAsync(long tenantId, QuotaResourceType rt, string key, long amount, long limit, CancellationToken ct) =>
        _ctx.QuotaUsages
            .Where(u => u.TenantId == tenantId && u.ResourceType == rt && u.PeriodKey == key && u.Used + amount <= limit)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Used, x => x.Used + amount), ct);

    // 写路径：确保当前周期用量行就绪（无则插入、跨周期则滚动），插入竞态下幂等重试。
    private async Task<bool> EnsureUsageRowAsync(long tenantId, QuotaResourceType rt, QuotaWindow window, DateTime now, CancellationToken ct)
    {
        var key = PeriodKeyFor(window, now);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var usage = await _ctx.QuotaUsages
                .Where(u => u.TenantId == tenantId && u.ResourceType == rt)
                .OrderByDescending(u => u.Id)
                .FirstOrDefaultAsync(ct);

            if (usage is null)
            {
                var created = new QuotaUsage
                {
                    TenantId = tenantId,
                    ResourceType = rt,
                    Used = 0,
                    PeriodKey = key,
                    LastReset = now,
                    CreatedTime = now,
                };
                _ctx.QuotaUsages.Add(created);
                try
                {
                    await _ctx.SaveChangesAsync(ct);
                    return true;
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    DetachAddedUsages();
                    await Task.Delay(20 * (attempt + 1), ct);
                    continue;
                }
            }

            if (usage.PeriodKey != key)
            {
                usage.Used = 0;
                usage.PeriodKey = key;
                usage.LastReset = now;
                await _ctx.SaveChangesAsync(ct);
            }
            return true;
        }
        return false;
    }

    private void DetachAddedUsages()
    {
        foreach (var e in _ctx.ChangeTracker.Entries<QuotaUsage>().Where(e => e.State == EntityState.Added).ToList())
            e.State = EntityState.Detached;
    }

    // ExecuteUpdate 不更新变更跟踪器中的实体；Reload 拉取 DB 最新 Used，避免同一上下文后续查询读到陈旧值。
    private async Task ReloadUsageTrackerAsync(long tenantId, QuotaResourceType rt, CancellationToken ct)
    {
        var tracked = _ctx.ChangeTracker.Entries<QuotaUsage>()
            .FirstOrDefault(e => e.Entity.TenantId == tenantId && e.Entity.ResourceType == rt);
        if (tracked is not null)
            await tracked.ReloadAsync(ct);
    }

    // SQLite: Microsoft.Data.Sqlite.SqliteException（SqliteErrorCode 2067 唯一约束 / 1555 约束）
    // SQL Server: Microsoft.Data.SqlClient.SqlException（Number 2601 / 2627）
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null) return false;
        var t = inner.GetType();
        var name = t.Name;
        if (name == "SqliteException")
        {
            var code = (int?)t.GetProperty("SqliteErrorCode")?.GetValue(inner);
            return code == 2067 || code == 1555;
        }
        if (name == "SqlException")
        {
            var number = (int?)t.GetProperty("Number")?.GetValue(inner);
            return number == 2601 || number == 2627;
        }
        return false;
    }

    private static string PeriodKeyFor(QuotaWindow window, DateTime now) => window switch
    {
        QuotaWindow.Daily => now.ToString("yyyy-MM-dd"),
        QuotaWindow.Monthly => now.ToString("yyyy-MM"),
        _ => "total",
    };
    #endregion
}
