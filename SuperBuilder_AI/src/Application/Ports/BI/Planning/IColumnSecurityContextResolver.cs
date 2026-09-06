using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// 解析当前执行身份下的列级安全上下文（M5-05）。
///
/// 默认实现从执行身份读取租户 / 用户，授权白名单留作治理扩展点（默认空）。
/// 生产环境可在此接入列级 ACL / 角色权限，填充
/// <see cref="ColumnSecurityContext.AuthorizedColumnIds"/>。
/// </summary>
public interface IColumnSecurityContextResolver
{
	/// <summary>基于当前执行身份与计划解析 <see cref="ColumnSecurityContext"/>。</summary>
	Task<ColumnSecurityContext> ResolveAsync(QueryPlan plan, CancellationToken ct = default);
}
