using SuperBulider_AI.Models.BI;


namespace SuperBulider_AI.Interfaces.BI;

/// <summary>
/// 查询结果理解服务。
///
/// 负责:
///
/// QueryResult
///        ↓
/// AI分析
///        ↓
/// QueryAnswer
///
/// </summary>
public interface IResultUnderstandingService
{


	/// <summary>
	/// 根据查询结果生成AI回答。
	/// </summary>
	/// <param name="question">
	/// 用户原始问题。
	/// </param>
	/// <param name="result">
	/// 数据库查询结果。
	/// </param>
	Task<QueryAnswer>
		AnalyzeAsync(
			string question,
			QueryResult result);


}