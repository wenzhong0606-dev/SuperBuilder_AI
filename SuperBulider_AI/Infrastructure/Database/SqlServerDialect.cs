namespace SuperBuilder_AI.Infrastructure.Database;

/// <summary>
/// SQL Server数据库方言。
/// </summary>
public class SqlServerDialect
	:
	ISqlDialect
{


	public string Name
	{
		get
		{
			return "SQLSERVER";
		}
	}




	public string EscapeIdentifier(
		string identifier)
	{

		return $"[{identifier}]";

	}





	public string ApplyLimit(
		string sql,
		int limit)
	{


		/*
		 * SQL Server:
		 *
		 * SELECT TOP 10
		 *
		 * 需要插入SELECT后。
		 *
		 */


		return sql.Replace(
			"SELECT ",
			$"SELECT TOP {limit} ",
			StringComparison.OrdinalIgnoreCase);

	}





	public string GetParameterName(
		int index)
	{

		return $"@p{index}";

	}


}