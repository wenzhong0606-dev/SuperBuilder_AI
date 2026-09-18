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


	public string QualifyTable(
		string? catalog,
		string? schema,
		string table)
	{
		var parts = new List<string>();
		if (!string.IsNullOrWhiteSpace(catalog))
			parts.Add(EscapeIdentifier(catalog));
		if (!string.IsNullOrWhiteSpace(schema))
			parts.Add(EscapeIdentifier(schema));
		parts.Add(EscapeIdentifier(table));
		return string.Join(".", parts);
	}


	public void AssertCatalogResolvable(
		string? catalog,
		string? currentCatalog)
	{
		// SQL Server 支持跨 catalog 限定名（同一实例且具备权限），无需拒绝。
	}


}