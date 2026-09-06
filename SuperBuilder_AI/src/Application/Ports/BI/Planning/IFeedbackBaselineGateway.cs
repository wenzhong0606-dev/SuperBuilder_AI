using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// 反馈闭环与基线/回归体系的网关（M5-10）。
/// 默认实现 NoOp，不触及既有 Golden Baseline 服务，保证对 Golden 免疫；
/// 生产环境可注入真实实现，将候选纳入基线并触发回归校验。
/// </summary>
public interface IFeedbackBaselineGateway
{
    /// <summary>将候选改进项注册进基线，返回基线版本标识。</summary>
    Task<string> RegisterBaselineAsync(FeedbackDrivenCandidate candidate, CancellationToken ct = default);

    /// <summary>对指定基线版本触发并校验回归，返回回归结果引用。</summary>
    Task<string> VerifyRegressionAsync(string baselineVersion, CancellationToken ct = default);
}
