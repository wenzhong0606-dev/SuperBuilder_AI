using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 默认零行为网关（M5-10）。不触及既有 Golden Baseline 服务，保证对 Golden 免疫；
/// 闭环的 Baselined/RegressionVerified 步骤在此返回占位标识而不触发真实回归。
/// </summary>
public sealed class NoOpFeedbackBaselineGateway : IFeedbackBaselineGateway
{
    public Task<string> RegisterBaselineAsync(FeedbackDrivenCandidate candidate, CancellationToken ct = default)
        => Task.FromResult("noop-baseline");

    public Task<string> VerifyRegressionAsync(string baselineVersion, CancellationToken ct = default)
        => Task.FromResult("noop-regression");
}
