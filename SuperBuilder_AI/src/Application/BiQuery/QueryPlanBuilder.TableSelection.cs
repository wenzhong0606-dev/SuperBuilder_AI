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
			QueryIntent? intent = null)
		=> _tableSelector.SelectBestTable(
			results,
			businessTerms,
			intent);
}
