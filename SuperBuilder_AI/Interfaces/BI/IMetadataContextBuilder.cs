using SuperBuilder_AI.Models.Metadata;


namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// Metadata上下文构建器。
///
/// 将数据库字段知识转换为AI Prompt上下文。
///
/// </summary>
public interface IMetadataContextBuilder
{

	/// <summary>
	/// 创建AI理解上下文
	/// </summary>
	Task<string> BuildAsync(
		string question);

}
