using SuperBuilder_AI.Models.BI;


namespace SuperBuilder_AI.Interfaces.BI;

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

	/// <summary>
	/// 根据已经通过校验的 QueryPlan 分析结果。默认实现保持旧调用方兼容；
	/// 生产链路应传入 plan，使明细、聚合和趋势使用不同的展示策略。
	/// </summary>
	Task<QueryAnswer> AnalyzeAsync(
		string question,
		QueryResult result,
		QueryPlan plan)
		=> AnalyzeAsync(question, result);


}
