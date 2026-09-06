using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 策略驱动列敏感性分类器（M5-08 真实治理实现）：
/// 按内置 PII 启发 + 配置受限列名 + 逐租户覆盖，依据物理列名（来自 M5-01/02 规范化语义模型）
/// 判定列是否受限。仅在 <see cref="ColumnSecurityModeResolver.Resolve"/> 解析为 PolicyDriven 时生效；
/// 否则（含 DenyNothing 与无列名情形）保守返回 false（与 M5-05 零拦截一致）。
/// </summary>
public sealed class PolicyDrivenColumnClassifier : IColumnSensitivityClassifier
{
	private readonly ColumnSecurityOptions _options;

	/// <summary>创建策略驱动分类器。</summary>
	public PolicyDrivenColumnClassifier(IOptions<ColumnSecurityOptions> options)
	{
		_options = options?.Value ?? new ColumnSecurityOptions();
	}

	/// <inheritdoc />
	public Task<bool> IsRestrictedAsync(
		long columnId,
		string? columnName,
		long? tableId,
		ColumnSecurityContext ctx,
		CancellationToken ct = default)
	{
		if (ColumnSecurityModeResolver.Resolve(_options, ctx.TenantId) != ColumnClassifierMode.PolicyDriven)
			return Task.FromResult(false);

		if (string.IsNullOrWhiteSpace(columnName))
			return Task.FromResult(false);

		var lower = columnName!.Trim().ToLowerInvariant();

		if (_options.IncludeBuiltInPiiHeuristics && BuiltInPiiCatalog.Contains(lower))
			return Task.FromResult(true);

		if (ContainsName(_options.RestrictedColumnNames, lower))
			return Task.FromResult(true);

		if (_options.TenantOverrides.TryGetValue(ctx.TenantId, out var ov)
			&& ov?.RestrictedColumnNames is { } extra
			&& ContainsName(extra, lower))
			return Task.FromResult(true);

		return Task.FromResult(false);
	}

	private static bool ContainsName(List<string>? names, string lower) =>
		names is { Count: > 0 }
		&& names.Any(x => !string.IsNullOrWhiteSpace(x)
			&& string.Equals(x.Trim(), lower, StringComparison.OrdinalIgnoreCase));
}
