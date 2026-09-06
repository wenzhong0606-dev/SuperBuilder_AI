using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 默认内存存储桩（M5-10）。零落盘、进程内有效，对 Golden 免疫，不引入任何持久化依赖。
/// </summary>
public sealed class InMemoryProductionFeedbackStore : IProductionFeedbackStore
{
    private readonly ConcurrentDictionary<Guid, ProductionFeedback> _items = new();

    public Task<ProductionFeedback> SaveAsync(ProductionFeedback feedback, CancellationToken ct = default)
    {
        if (feedback is null) throw new System.ArgumentNullException(nameof(feedback));
        _items[feedback.FeedbackId] = feedback;
        return Task.FromResult(feedback);
    }

    public Task<ProductionFeedback?> GetAsync(Guid feedbackId, CancellationToken ct = default)
    {
        _items.TryGetValue(feedbackId, out var f);
        return Task.FromResult(f);
    }

    public Task<IReadOnlyList<ProductionFeedback>> ListAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ProductionFeedback> list = _items.Values.OrderByDescending(x => x.SubmittedAt).ToList();
        return Task.FromResult(list);
    }
}
