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


	public string QualifyTable(
		string? catalog,
		string? schema,
		string table)
	{
		// PostgreSQL 限定名仅 "schema"."table"；catalog(=数据库) 不得进入限定名，
		// 跨 catalog 由 AssertCatalogResolvable 在执行层拒绝（§10.5 #5）。
		if (!string.IsNullOrWhiteSpace(schema))
			return EscapeIdentifier(schema) + "." + EscapeIdentifier(table);
		return EscapeIdentifier(table);
	}


	public void AssertCatalogResolvable(
		string? catalog,
		string? currentCatalog)
	{
		if (!string.IsNullOrWhiteSpace(catalog)
			&& !string.Equals(catalog, currentCatalog, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException(
				$"PostgreSQL 不支持跨 catalog 查询：表指向数据库『{catalog}』，" +
				$"当前执行连接数据库为『{currentCatalog}』。");
		}
	}


}