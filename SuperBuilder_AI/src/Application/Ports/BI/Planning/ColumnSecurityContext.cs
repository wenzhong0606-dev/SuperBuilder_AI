using System.Collections.Generic;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// 列级安全的运行时上下文（M5-05）。
///
/// 由 <see cref="IColumnSecurityContextResolver"/> 从当前执行身份 + 列授权策略解析，
/// 供 <see cref="IColumnSecurityPolicy"/> 判定哪些受限列需要拦截。
/// </summary>
public sealed class ColumnSecurityContext
{
	/// <summary>构造列级安全上下文。</summary>
	public ColumnSecurityContext(long tenantId, long userId)
	{
		TenantId = tenantId;
		UserId = userId;
	}

	/// <summary>有效租户。</summary>
	public long TenantId { get; }

	/// <summary>当前用户。</summary>
	public long UserId { get; }

	/// <summary>
	/// 已授权（白名单）的受限列 MetadataColumnId 集合。
	/// 命中白名单的受限列不会被拦截。
	/// 默认空 = 任何受限列均拦截（deny-by-default；某列是否"受限"由
	/// <see cref="IColumnSensitivityClassifier"/> 决定）。
	/// </summary>
	public ISet<long> AuthorizedColumnIds { get; set; } = new HashSet<long>();
}
