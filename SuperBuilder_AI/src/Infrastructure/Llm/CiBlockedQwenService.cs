using SuperBuilder_AI.Interfaces;

namespace SuperBuilder_AI.Services;

/// <summary>
/// CI 环境的 Qwen 阻断器。
/// 普通 CI 必须是确定性、零真实 LLM Token 消耗；若测试意外走到真实 Qwen 路径，
/// 立即失败并提示将该场景改为 Fake/Stub，或移入独立的 Live AI Regression。
/// </summary>
public sealed class CiBlockedQwenService : IQwenService
{
    private static InvalidOperationException Blocked()
        => new(
            "CI 已禁止真实 Qwen 调用，以避免普通 Build/E2E 消耗 AI Token。"
            + " 请为该测试注入 Fake/Stub IQwenService，或在独立 Live AI Regression 中执行。");

    public Task<string> GenerateSqlAsync(string prompt)
        => Task.FromException<string>(Blocked());

    public Task<string> GenerateSqlAsync(string prompt, CancellationToken ct)
        => Task.FromException<string>(Blocked());
}
