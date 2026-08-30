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
            var (limit, window) = await ResolveLimitAsync(tenantId, rt, ct);
            var (used, periodKey) = await ResolveUsageAsync(tenantId, rt, window, now, ct);
            items.Add(new QuotaItemView(limit, used, Math.Max(0, limit - used), window, periodKey));
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
        var defaultPolicy = await _ctx.QuotaPolicies
            .Where(p => p.TenantId == 0 && p.ResourceType == rt)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
        if (defaultPolicy != null) return (defaultPolicy.Limit, defaultPolicy.Window);

        // 极端兜底：连种子都没有，用常量
        var def = QuotaDefaults.PlatformDefaults.TryGetValue(rt, out var d) ? d : (0, QuotaWindow.Total);
        return (def.Limit, def.Window);
    }

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
