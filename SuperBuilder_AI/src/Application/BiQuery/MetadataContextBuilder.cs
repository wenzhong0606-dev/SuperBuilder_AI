using System.Text;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;


namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// Metadata上下文构建器。
///
/// 负责:
///
/// 用户问题
///      ↓
/// Metadata语义搜索
///      ↓
/// 构造AI理解上下文
///
/// </summary>
public class MetadataContextBuilder
	: IMetadataContextBuilder
{


	private readonly IMetadataSemanticSearchService
		_metadataSearch;




	public MetadataContextBuilder(
		IMetadataSemanticSearchService metadataSearch)
	{

		_metadataSearch =
			metadataSearch;

	}





	/// <summary>
	/// 上下文默认召回条数（保持既有值不变）。
	/// </summary>
	private const int TopK = 10;


	/// <summary>
	/// 构建Metadata AI上下文。
	/// </summary>
	public Task<string> BuildAsync(
		string question)
		=> BuildAsync(
			question,
			null);




	/// <summary>
	/// 在指定数据源作用域内构建Metadata AI上下文。
	///
	/// <paramref name="dataSourceIds"/> 为 <c>null</c> 时走原三参检索调用，
	/// 提示词与新增作用域之前完全一致（Golden / 内部兼容路径零回归）。
	/// </summary>
	public async Task<string> BuildAsync(
		string question,
		IReadOnlyCollection<long>? dataSourceIds)
	{


		var results =
			dataSourceIds is null
				? await _metadataSearch
					.SearchAsync(
						question,
						TopK)
				: await _metadataSearch
					.SearchAsync(
						question,
						TopK,
						null,
						dataSourceIds);




		var builder =
			new StringBuilder();




		builder.AppendLine(
			"数据库Metadata知识:");





		foreach (var item in results)
		{


			/*
             * 表信息
             */

			if (item.Table != null)
			{

				builder.AppendLine(
					$"""
                    表:
                    {item.Table.TableName}

                    """);

			}





			/*
             * 字段信息
             */

			if (item.Column != null)
			{

				builder.AppendLine(
					$"""
                    字段:
                    {item.Column.ColumnName}


                    数据类型:
                    {item.Column.DataType}


                    字段说明:
                    {item.Column.ColumnComment}


                    """);

			}





			/*
             * 字段业务语义
             */

			if (item.Semantic != null)
			{

				builder.AppendLine(
					$"""
                    业务含义:
                    {item.Semantic.BusinessMeaning}


                    关键词:
                    {item.Semantic.Keywords}


                    同义词:
                    {item.Semantic.Synonyms}


                    示例问题:
                    {item.Semantic.ExampleQuestions}


                    业务领域:
                    {item.Semantic.BusinessDomain}

                    """);

			}



			builder.AppendLine(
				"======================");

		}



		return builder.ToString();

	}

}
