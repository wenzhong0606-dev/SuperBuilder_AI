using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 可配置列敏感性分类器（M5-08 注册到 DI 的 <see cref="IColumnSensitivityClassifier"/> 实现）：
/// 依据全局 / 逐租户模式在 <see cref="DenyNothingColumnClassifier"/>（零拦截，默认）与
/// <see cref="PolicyDrivenColumnClassifier"/>（真实治理）之间切换。
/// 默认配置下完全等价于 DenyNothing（零默认行为变更，对 Golden 契约免疫）。
/// </summary>
public sealed class ConfigurableColumnClassifier : IColumnSensitivityClassifier
{
	private readonly ColumnSecurityOptions _options;
	private readonly DenyNothingColumnClassifier _denyNothing;
	private readonly PolicyDrivenColumnClassifier _policyDriven;

	/// <summary>创建可配置分类器。</summary>
	public ConfigurableColumnClassifier(
		IOptions<ColumnSecurityOptions> options,
		DenyNothingColumnClassifier denyNothing,
		PolicyDrivenColumnClassifier policyDriven)
	{
		_options = options?.Value ?? new ColumnSecurityOptions();
		_denyNothing = denyNothing ?? throw new ArgumentNullException(nameof(denyNothing));
		_policyDriven = policyDriven ?? throw new ArgumentNullException(nameof(policyDriven));
	}

	/// <inheritdoc />
	public Task<bool> IsRestrictedAsync(
		long columnId,
		string? columnName,
		long? tableId,
		ColumnSecurityContext ctx,
		CancellationToken ct = default)
		=> ColumnSecurityModeResolver.Resolve(_options, ctx.TenantId) == ColumnClassifierMode.PolicyDriven
			? _policyDriven.IsRestrictedAsync(columnId, columnName, tableId, ctx, ct)
			: _denyNothing.IsRestrictedAsync(columnId, columnName, tableId, ctx, ct);
}
