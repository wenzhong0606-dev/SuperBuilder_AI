namespace SuperBulider_AI.Interfaces;


/// <summary>
/// Metadata搜索文本生成器
/// </summary>
public interface IMetadataSearchTextBuilder
{


	string BuildTableText(
		string? tableName,
		string? tableComment,
		string? businessDomain);



	string BuildColumnText(
		string? tableName,
		string? columnName,
		string? columnComment,
		string? dataType);



	/// <summary>
	/// 创建完整表Metadata文本
	///
	/// 用于Embedding
	/// </summary>
	string BuildMetadataText(
		string? tableName,
		string? tableComment,
		IEnumerable<string> columnTexts);

}