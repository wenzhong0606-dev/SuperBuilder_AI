using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 默认列敏感性分类器（M5-05）：任何列都不视为受限。
///
/// 零影响、向后兼容：在未接入治理策略（按业务键 / 分类 / 脱敏标记判定受限列）前，
/// 列级安全阶段不产生任何拦截行为。生产环境应替换为治理驱动的实现。
/// </summary>
public sealed class DenyNothingColumnClassifier : IColumnSensitivityClassifier
{
	/// <inheritdoc />
	public Task<bool> IsRestrictedAsync(
		long columnId,
		string? columnName,
		long? tableId,
		ColumnSecurityContext ctx,
		CancellationToken ct = default)
		=> Task.FromResult(false);
}
