namespace SuperBulider_AI.Infrastructure.Database;

/// <summary>
/// MySQL数据库方言。
/// </summary>
public class MySqlDialect
	:
	ISqlDialect
{


	public string Name
	{
		get
		{
			return "MYSQL";
		}
	}




	public string EscapeIdentifier(
		string identifier)
	{

		return $"`{identifier}`";

	}





	public string ApplyLimit(
		string sql,
		int limit)
	{

		return
			$"{sql} LIMIT {limit}";

	}





	public string GetParameterName(
		int index)
	{

		return $"@p{index}";

	}


}