using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 可配置模型成本遥测（M5-08 注册到 DI 的 <see cref="IModelCostTelemetry"/> 实现）：
/// 依据 <see cref="CostGovernanceOptions.TelemetryMode"/> 在
/// <see cref="NoOpModelCostTelemetry"/>（默认，零行为）与 <see cref="LogModelCostTelemetry"/> 间切换。
/// </summary>
public sealed class ConfigurableModelCostTelemetry : IModelCostTelemetry
{
	private readonly CostGovernanceOptions _options;
	private readonly NoOpModelCostTelemetry _noop;
	private readonly LogModelCostTelemetry _log;

	/// <summary>创建可配置遥测。</summary>
	public ConfigurableModelCostTelemetry(
		IOptions<CostGovernanceOptions> options,
		NoOpModelCostTelemetry noop,
		LogModelCostTelemetry log)
	{
		_options = options?.Value ?? new CostGovernanceOptions();
		_noop = noop ?? throw new ArgumentNullException(nameof(noop));
		_log = log ?? throw new ArgumentNullException(nameof(log));
	}

	/// <inheritdoc />
	public Task RecordAsync(
		QueryCostAssessment assessment,
		CostGovernanceVerdict verdict,
		CostGovernanceContext context,
		CancellationToken ct = default)
		=> _options.TelemetryMode == CostTelemetryMode.Log
			? _log.RecordAsync(assessment, verdict, context, ct)
			: _noop.RecordAsync(assessment, verdict, context, ct);
}
