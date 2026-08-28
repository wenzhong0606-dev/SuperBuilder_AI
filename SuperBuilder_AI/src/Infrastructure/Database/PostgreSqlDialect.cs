namespace SuperBuilder_AI.Infrastructure.Database;

/// <summary>
/// PostgreSQL数据库方言。
/// </summary>
public class PostgreSqlDialect
	:
	ISqlDialect
{


	public string Name
	{
		get
		{
			return "POSTGRESQL";
		}
	}




	public string EscapeIdentifier(
		string identifier)
	{

		return $"\"{identifier}\"";

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