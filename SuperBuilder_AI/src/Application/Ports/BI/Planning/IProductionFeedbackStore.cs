using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// 生产反馈存储端口（M5-10）。默认实现为内存桩，零落盘；
/// 生产环境可替换为持久化实现，但默认路径不依赖任何数据库。
/// </summary>
public interface IProductionFeedbackStore
{
    /// <summary>保存（新增或更新）一条反馈。</summary>
    Task<ProductionFeedback> SaveAsync(ProductionFeedback feedback, CancellationToken ct = default);

    /// <summary>按标识获取反馈。</summary>
    Task<ProductionFeedback?> GetAsync(Guid feedbackId, CancellationToken ct = default);

    /// <summary>列出全部反馈（按提交时间倒序）。</summary>
    Task<IReadOnlyList<ProductionFeedback>> ListAsync(CancellationToken ct = default);
}
