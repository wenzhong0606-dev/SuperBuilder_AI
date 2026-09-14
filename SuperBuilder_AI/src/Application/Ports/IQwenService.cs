namespace SuperBuilder_AI.Interfaces;


/// <summary>
/// 千问AI服务
/// </summary>
public interface IQwenService
{


	/// <summary>
	/// 根据构建好的 prompt 调用千问模型并返回结果文本（通常为 SQL 或 JSON）。
	/// </summary>
	/// <param name="prompt">发送给模型的完整 prompt 文本。</param>
	Task<string> GenerateSqlAsync(string prompt);

	Task<string> GenerateSqlAsync(string prompt, CancellationToken ct)
		=> GenerateSqlAsync(prompt);


}
