using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// 判定某个元数据列是否属于"受限列"（敏感 / 需保护）。
///
/// 默认实现 <c>DenyNothingColumnClassifier</c> 返回 false（零影响，向后兼容）；
/// 生产环境应接入治理策略（按业务键 / 分类 / 脱敏标记判定是否受限）。
/// </summary>
public interface IColumnSensitivityClassifier
{
	/// <summary>
	/// 判定列是否受限。优先依据 <paramref name="columnId"/>（稳定标识），
	/// 当仅有列名（如 Filter / Metric）时退化为依据 <paramref name="columnName"/>。
	/// </summary>
	Task<bool> IsRestrictedAsync(
		long columnId,
		string? columnName,
		long? tableId,
		ColumnSecurityContext ctx,
		CancellationToken ct = default);
}
