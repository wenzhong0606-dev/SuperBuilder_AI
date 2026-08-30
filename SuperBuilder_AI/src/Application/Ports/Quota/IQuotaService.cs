using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.Quota;

namespace SuperBuilder_AI.Interfaces.Quota;

/// <summary>单资源配额视图。</summary>
public record QuotaItemView(long Limit, long Used, long Remaining, QuotaWindow Window, string PeriodKey);

/// <summary>租户配额概览。</summary>
public record QuotaOverviewResponse(long TenantId, IReadOnlyList<QuotaItemView> Items);

/// <summary>配额校验请求。</summary>
public record QuotaCheckRequest(string ResourceType, long Requested = 1);

/// <summary>配额校验结果。</summary>
public record QuotaCheckResponse(bool Allowed, long Limit, long Used, long Remaining);

/// <summary>配额扣减请求。</summary>
public record QuotaConsumeRequest(string ResourceType, long Amount = 1);

/// <summary>
/// 租户配额服务（确定性，不调 LLM）。
/// 负责平台默认配额种子、按租户回退解析上限、用量校验与扣减 enforcement。
/// </summary>
public interface IQuotaService
{
    /// <summary>幂等种子平台默认配额（TenantId=0）。</summary>
    Task EnsureSeededAsync(CancellationToken ct = default);

    /// <summary>返回租户全部受管资源的配额概览（含使用量与剩余）。</summary>
    Task<QuotaOverviewResponse> GetQuotaAsync(long tenantId, CancellationToken ct = default);

    /// <summary>校验指定资源是否还有足够配额容纳 requested。确定性，不调 LLM。</summary>
    Task<QuotaCheckResponse> CheckAsync(long tenantId, QuotaResourceType resourceType, long requested = 1, CancellationToken ct = default);

    /// <summary>扣减配额。不足则返回 false（不抛）；成功扣减返回 true。确定性，不调 LLM。</summary>
    Task<bool> ConsumeAsync(long tenantId, QuotaResourceType resourceType, long amount = 1, CancellationToken ct = default);
}
