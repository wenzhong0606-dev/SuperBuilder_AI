using System;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 默认列级安全上下文解析器（M5-05）。
///
/// 从执行身份读取租户 / 用户；授权白名单留作治理扩展点（默认空）。
/// 生产环境可在此接入列级 ACL / 角色权限，填充
/// <see cref="ColumnSecurityContext.AuthorizedColumnIds"/>。
/// </summary>
public sealed class DefaultColumnSecurityContextResolver : IColumnSecurityContextResolver
{
	private readonly IDataSourceExecutionIdentityAccessor _identity;

	/// <summary>创建默认上下文解析器。</summary>
	public DefaultColumnSecurityContextResolver(IDataSourceExecutionIdentityAccessor identity)
	{
		_identity = identity ?? throw new ArgumentNullException(nameof(identity));
	}

	/// <inheritdoc />
	public Task<ColumnSecurityContext> ResolveAsync(QueryPlan plan, CancellationToken ct = default)
	{
		var caller = _identity.Current;
		var ctx = new ColumnSecurityContext(caller?.TenantId ?? 0, caller?.UserId ?? 0);
		// 授权白名单留作治理扩展点（默认空 = deny-by-default 由 classifier 控制）。
		return Task.FromResult(ctx);
	}
}
