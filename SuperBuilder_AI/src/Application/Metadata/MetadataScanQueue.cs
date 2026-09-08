using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SuperBuilder_AI.Services;

/// <summary>
/// 基于 <see cref="Channel{T}"/> 的扫描任务队列实现（M4-05）。
/// 单消费者（后台处理器）读取，支持多生产者入队。
/// </summary>
public sealed class MetadataScanQueue : IMetadataScanQueue
{
    private readonly Channel<long> _channel;

    public MetadataScanQueue()
    {
        _channel = Channel.CreateBounded<long>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public ValueTask EnqueueAsync(long jobId, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(jobId, ct);

    public ValueTask<long> DequeueAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAsync(ct);
}
