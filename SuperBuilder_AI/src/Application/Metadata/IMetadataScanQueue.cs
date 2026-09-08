using System.Threading;
using System.Threading.Tasks;

namespace SuperBuilder_AI.Services;

/// <summary>
/// 元数据扫描任务队列（M4-05）。在通道中传递扫描任务 Id，由后台处理器消费。
/// </summary>
public interface IMetadataScanQueue
{
    /// <summary>入队一个扫描任务 Id。</summary>
    ValueTask EnqueueAsync(long jobId, CancellationToken ct = default);

    /// <summary>阻塞直至取出一个扫描任务 Id。</summary>
    ValueTask<long> DequeueAsync(CancellationToken ct = default);
}
