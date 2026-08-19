using SuperBuilder_AI.Interfaces;

namespace SuperBuilder_AI.Services;

/// <summary>
/// Metadata搜索文本生成器
/// </summary>
public class MetadataSearchTextBuilder
	: IMetadataSearchTextBuilder
{


	/// <summary>
	/// 创建表级搜索文本
	/// </summary>
	public string BuildTableText(
		string? tableName,
		string? tableComment,
		string? businessDomain)
	{

		var values = new List<string?>
		{
			tableName,
			tableComment,
			businessDomain
		};


		return string.Join(
			" ",
			values
			.Where(x => !string.IsNullOrWhiteSpace(x)));

	}



	/// <summary>
	/// 创建字段搜索文本
	/// </summary>
	public string BuildColumnText(
		string? tableName,
		string? columnName,
		string? columnComment,
		string? dataType)
	{

		var values = new List<string?>
		{
			tableName,
			columnName,
			columnComment,
			dataType
		};


		return string.Join(
			" ",
			values
			.Where(x => !string.IsNullOrWhiteSpace(x)));

	}


	/// <summary>
	/// 创建完整Metadata搜索文本
	///
	/// 示例:
	///
	/// SalesOrder
	/// 销售订单
	///
	/// 字段:
	/// NetAmount
	/// 销售净金额
	/// decimal
	///
	/// </summary>
	public string BuildMetadataText(
		string? tableName,
		string? tableComment,
		IEnumerable<string> columnTexts)
	{

		var values =
			new List<string?>
			{
			tableName,
			tableComment
			};


		values.AddRange(
			columnTexts);



		return string.Join(
			" ",
			values
			.Where(x =>
				!string.IsNullOrWhiteSpace(x)));

	}
}