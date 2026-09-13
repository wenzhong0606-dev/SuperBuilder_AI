using System;

namespace SuperBuilder_AI.Api.Diagnostics;

/// <summary>
/// 扫描积压量规（OBS-01）。单例、有界内存，记录「待处理扫描任务数」——
/// 即已入队但尚未进入终态（Succeeded/Failed）的任务，覆盖 Queued + Running。
/// <para>由 <see cref="Controllers.MetadataController"/> 入队时 <see cref="Increment"/>，
/// 由 <see cref="Background.MetadataScanHostedService"/> 进入终态时 <see cref="Decrement"/>。</para>
/// </summary>
public sealed class ScanBacklogGauge
{
	private readonly object _gate = new();
	private int _pending;
	private int _peak;

	/// <summary>当前待处理扫描任务数（Queued + Running）。</summary>
	public int Pending
	{
		get { lock (_gate) return _pending; }
	}

	/// <summary>观测到的最大待处理数（进程生命周期内）。</summary>
	public int Peak
	{
		get { lock (_gate) return _peak; }
	}

	/// <summary>任务入队：待处理 +1，并刷新峰值。</summary>
	public void Increment()
	{
		lock (_gate)
		{
			_pending++;
			if (_pending > _peak) _peak = _pending;
		}
	}

	/// <summary>任务进入终态：待处理 −1（下限为 0，防御重复/错位 Decrement）。</summary>
	public void Decrement()
	{
		lock (_gate)
		{
			if (_pending > 0) _pending--;
		}
	}

	/// <summary>重置（运维/测试用）。</summary>
	public void Reset()
	{
		lock (_gate)
		{
			_pending = 0;
			_peak = 0;
		}
	}
}
