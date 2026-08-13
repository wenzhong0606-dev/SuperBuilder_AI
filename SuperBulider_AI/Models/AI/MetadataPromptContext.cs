namespace SuperBulider_AI.Models.AI;

/// <summary>
/// AI生成SQL上下文
/// </summary>
public class MetadataPromptContext
{

	/// <summary>
	/// Metadata内容
	/// </summary>
	public string Metadata { get; set; } = string.Empty;


	/// <summary>
	/// 完整Prompt
	/// </summary>
	public string Prompt { get; set; } = string.Empty;

}