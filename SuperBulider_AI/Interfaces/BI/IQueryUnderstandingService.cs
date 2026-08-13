using SuperBulider_AI.Models.AI;


namespace SuperBulider_AI.Interfaces;

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


}
