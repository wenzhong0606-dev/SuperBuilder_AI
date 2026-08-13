using System.Text;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Models.AI;


namespace SuperBulider_AI.Services;

/// <summary>
/// Metadata Prompt生成服务
///
/// 根据用户问题:
///
/// 用户问题
///     ↓
/// MetadataSemanticSearchService
///     ↓
/// Table
/// Column
/// Semantic
///     ↓
/// 生成Text-To-SQL Prompt
///
/// </summary>
public class MetadataPromptBuilder
	: IMetadataPromptBuilder
{


	private readonly IMetadataSemanticSearchService _semanticSearch;



	/// <summary>
	/// 构造函数
	/// </summary>
	public MetadataPromptBuilder(
		IMetadataSemanticSearchService semanticSearch)
	{

		_semanticSearch =
			semanticSearch;

	}





	/// <summary>
	/// 根据用户问题生成SQL Prompt
	/// </summary>
	public async Task<MetadataPromptContext>
		BuildAsync(
			string question)
	{


		/*
		 * Step 1
		 *
		 * Semantic Vector Search
		 *
		 */


		var semanticResults =
			await _semanticSearch
			.SearchAsync(
				question,
				10);






		/*
		 * Step 2
		 *
		 * 构建Metadata上下文
		 *
		 */


		var builder =
			new StringBuilder();



		builder.AppendLine(
			"数据库Metadata:");




		var tables =
			semanticResults

			.Where(x =>
				x.Table != null)

			.Select(x =>
				x.Table!)

			.GroupBy(x =>
				x.Id)

			.Select(x =>
				x.First())

			.ToList();







		foreach (var table in tables)
		{


			builder.AppendLine();


			builder.AppendLine(
				$"表名:{table.TableName}");



			builder.AppendLine(
				$"表说明:{table.TableComment}");



			builder.AppendLine(
				"字段:");




			var columns =
				semanticResults

				.Where(x =>
					x.Table != null
					&&
					x.Table.Id == table.Id
					&&
					x.Column != null)

				.Select(x =>
					x.Column!)

				.GroupBy(x =>
					x.Id)

				.Select(x =>
					x.First())

				.ToList();






			foreach (var column in columns)
			{


				builder.AppendLine();


				builder.AppendLine(
					$"字段:{column.ColumnName}");



				builder.AppendLine(
					$"类型:{column.DataType}");



				builder.AppendLine(
					$"数据库说明:{column.ColumnComment}");





				var semantic =
					semanticResults

					.Where(x =>
						x.Column != null
						&&
						x.Column.Id ==
						column.Id)

					.Select(x =>
						x.Semantic)

					.FirstOrDefault();





				if (semantic != null)
				{

					builder.AppendLine(
						$"业务含义:{semantic.BusinessMeaning}");



					builder.AppendLine(
						$"关键词:{semantic.Keywords}");



					builder.AppendLine(
						$"同义词:{semantic.Synonyms}");



					builder.AppendLine(
						$"示例问题:{semantic.ExampleQuestions}");

				}


			}



			builder.AppendLine();

		}





		var metadata =
			builder.ToString();






		/*
		 * Step 3
		 *
		 * 生成Qwen Prompt
		 *
		 */


		var prompt =
$"""
你是一名企业BI系统SQL生成专家。


你的任务:

根据用户业务问题生成准确SQL。


必须严格遵守:

1. 只能使用下面提供的数据库表。
2. 只能使用下面提供的字段。
3. 禁止虚构字段。
4. 禁止虚构表。
5. 优先根据字段业务语义判断业务含义。
6. 返回标准SQL。


====================

数据库Metadata:

{metadata}


====================


用户问题:

{question}


====================


请生成SQL:

""";






		return new MetadataPromptContext
		{

			Metadata =
				metadata,


			Prompt =
				prompt

		};

	}

}