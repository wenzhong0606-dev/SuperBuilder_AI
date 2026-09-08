using System.Collections.Generic;

using SuperBuilder_AI.Components.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// BI 问数域客户端契约（M9-01）：自然语言问数、原始响应、多轮语义细化。
/// 与 <see cref="IApiClient"/> 中对应方法签名一致，可独立于其他域单独测试/替换。
/// </summary>
public interface IBiApiClient
{
    Task<string?> AskRawAsync(string question, long? dataSourceId, CancellationToken ct = default);

    /// <summary>类型化问数：返回 <see cref="BIResponse"/> 并区分传输错误。</summary>
    Task<AskOutcome> AskAsync(string question, long? dataSourceId, string? conversationId = null, CancellationToken ct = default);

    /// <summary>
    /// 多轮语义调整：在已有问题（+ 历史上下文）上追加细化指令，重新走完整 BI 链路。
    /// 仅在用户显式发起「细化」时调用（<c>POST api/ask/refine</c>）；默认问数路径 <c>api/ask</c> 不变。
    /// </summary>
    Task<AskOutcome> RefineAsync(string? question, string instruction, IEnumerable<RefineTurn>? history, long? dataSourceId, CancellationToken ct = default);
}
