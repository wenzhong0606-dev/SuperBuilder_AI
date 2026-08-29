using SuperBuilder_AI.Models.BI;


namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// 用户问题理解服务。
///
/// 负责:
///
/// 用户语言
/// ↓
/// AI分析
/// ↓
/// QueryIntent
///
/// </summary>
public interface IQueryUnderstandingService
{


	/// <summary>
	/// 理解用户查询
	/// </summary>
	Task<QueryIntent> UnderstandAsync(
		string question);

	/// <summary>
	/// 理解用户查询并注入业务实体感知（P3）。
	/// 提供租户上下文后，归一化阶段会识别候选业务实体并写入 QueryIntent.BusinessEntityHints。
	/// </summary>
	Task<QueryIntent> UnderstandAsync(
		string question,
		long tenantId);


}
