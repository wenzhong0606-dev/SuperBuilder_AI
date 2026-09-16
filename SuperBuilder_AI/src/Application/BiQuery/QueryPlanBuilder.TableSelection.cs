using System;
using System.Collections.Generic;
using System.Linq;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services.BI;

public partial class QueryPlanBuilder : IQueryPlanBuilder
{
	/// <summary>
	/// A4 重构：候选表选择转发给 TableSelector，评分公式与输出保持原样。
	/// </summary>
	private MetadataTable?
		SelectBestTable(
			List<MetadataSemanticSearchResult> results,
			List<BusinessTerm> businessTerms,
			QueryIntent? intent = null,
			IReadOnlyCollection<long>? authorizedDataSourceIds = null)
		=> _tableSelector.SelectBestTable(
			results,
			businessTerms,
			intent,
			tableResolver: name =>
			{
				// 在“全部已授权数据源”范围内按表名找回，守住跨数据源安全。
				// 同步解析单次目录查询即可（ASP.NET Core 无 SynchronizationContext，无死锁风险）。
				var resolved =
					_metadataSearch
						.ResolveTableByNameAsync(
							name,
							authorizedDataSourceIds)
						.GetAwaiter()
						.GetResult();

				return resolved;
			});
}
