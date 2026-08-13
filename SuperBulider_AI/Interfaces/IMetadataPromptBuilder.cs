using SuperBulider_AI.Models.AI;


namespace SuperBulider_AI.Interfaces;

/// <summary>
/// Metadata Prompt构建器
/// </summary>
public interface IMetadataPromptBuilder
{


	/// <summary>
	/// 根据用户问题生成AI上下文
	/// </summary>
	Task<MetadataPromptContext> BuildAsync(
		string question);


}