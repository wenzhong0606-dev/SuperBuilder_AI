namespace SuperBuilder_AI.Interfaces.Agent.Runtime;

/// <summary>重试结果（携带实际尝试次数，供运行状态记录）。</summary>
public sealed record RetryOutcome<T>(T Result, int Attempts);

/// <summary>
/// 重试策略端口（M7-03）。
///
/// <para>仅对 <see cref="TransientToolException"/> 重试，最多 <see cref="MaxAttempts"/> 次；
/// 其余异常视为不可重试，直接向上抛出。幂等工具适用。</para>
/// </summary>
public interface IRetryPolicy
{
	/// <summary>最大尝试次数（含首次）。</summary>
	int MaxAttempts { get; }

	/// <summary>执行动作并在瞬态失败时重试，返回结果与尝试次数。</summary>
	Task<RetryOutcome<T>> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
