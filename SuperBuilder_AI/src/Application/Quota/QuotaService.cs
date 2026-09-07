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
        var usage = await GetOrCreateUsageAsync(tenantId, resourceType, window, now, ct);
        if (usage.Used + amount > limit) return false;
        usage.Used += amount;
        usage.PeriodKey = PeriodKeyFor(window, now);
        await _ctx.SaveChangesAsync(ct);
        return true;
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

    // 写路径：创建或滚动并持久化
    private async Task<QuotaUsage> GetOrCreateUsageAsync(long tenantId, QuotaResourceType rt, QuotaWindow window, DateTime now, CancellationToken ct)
    {
        var key = PeriodKeyFor(window, now);
        var usage = await _ctx.QuotaUsages
            .Where(u => u.TenantId == tenantId && u.ResourceType == rt)
            .OrderByDescending(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (usage == null)
        {
            usage = new QuotaUsage
            {
                TenantId = tenantId,
                ResourceType = rt,
                Used = 0,
                PeriodKey = key,
                LastReset = now,
                CreatedTime = now,
            };
            _ctx.QuotaUsages.Add(usage);
            await _ctx.SaveChangesAsync(ct);
            return usage;
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

    private static string PeriodKeyFor(QuotaWindow window, DateTime now) => window switch
    {
        QuotaWindow.Daily => now.ToString("yyyy-MM-dd"),
        QuotaWindow.Monthly => now.ToString("yyyy-MM"),
        _ => "total",
    };
    #endregion
}
