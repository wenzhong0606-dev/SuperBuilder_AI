using System.Data.Common;
using Dapper;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Models.BI;


namespace SuperBuilder_AI.Services.Database;

/// <summary>
/// 查询执行引擎。
///
/// 负责执行AI生成的动态SQL。
///
/// </summary>
public class QueryExecutionService
	:
	IQueryExecutionService
{


	private readonly IDataSourceConnectionFactory
		_connectionFactory;



	public QueryExecutionService(
		IDataSourceConnectionFactory connectionFactory)
	{

		_connectionFactory =
			connectionFactory;

	}





	/// <summary>
	/// 执行查询。
	/// </summary>
	public async Task<QueryResult>
		ExecuteAsync(
			SqlQuery query,
			long dataSourceId)
	{


		var result =
			new QueryResult();




		try
		{


			await using var connection =
				await _connectionFactory
				.CreateAsync(
					dataSourceId);





			if (connection.State
				!=
				System.Data.ConnectionState.Open)
			{
				await connection.OpenAsync();
			}






			var rows =
				await connection
				.QueryAsync(
					query.Sql,
					query.Parameters);






			foreach (var row in rows)
			{

				var dictionary =
					new Dictionary<string, object?>();




				var item =
					(IDictionary<string, object>)row;




				foreach (var column in item)
				{

					dictionary.Add(
						column.Key,
						column.Value);

				}



				result.Rows.Add(
					dictionary);

			}





			result.Success =
				true;



		}
		catch (Exception ex)
		{

			result.Success =
				false;


			result.ErrorMessage =
				ex.Message;

		}




		return result;

	}


}