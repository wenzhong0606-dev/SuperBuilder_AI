using System;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.Agent.Runtime;

namespace SuperBuilder_AI.Services.Agent.Runtime;

/// <summary>
/// 重试策略（M7-03）：仅对 <see cref="TransientToolException"/> 重试，最多 <see cref="MaxAttempts"/> 次；
/// 其余异常视为不可重试，直接向上抛出。测试环境下退化为零退避。
/// </summary>
public sealed class RetryPolicy : IRetryPolicy
{
	private static readonly TimeSpan ZeroBackoff = TimeSpan.Zero;

	/// <inheritdoc />
	public int MaxAttempts { get; }

	/// <summary>构造重试策略。</summary>
	/// <param name="maxAttempts">最大尝试次数（含首次），须 ≥ 1。</param>
	public RetryPolicy(int maxAttempts = 3) => MaxAttempts = maxAttempts;

	/// <inheritdoc />
	public async Task<RetryOutcome<T>> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
	{
		if (MaxAttempts < 1)
			throw new InvalidOperationException("MaxAttempts 必须 >= 1。");

		var attempts = 0;
		while (true)
		{
			attempts++;
			try
			{
				return new RetryOutcome<T>(await action(cancellationToken), attempts);
			}
			catch (TransientToolException) when (attempts < MaxAttempts)
			{
				await Task.Delay(ZeroBackoff, cancellationToken);
			}
		}
	}
}
