namespace SuperBuilder_AI.Infrastructure.Database;

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


	public string QualifyTable(
		string? catalog,
		string? schema,
		string table)
	{
		// MySQL 的 schema 与 catalog(=数据库) 同义；DTO 的 schema 必须为空或等于 catalog，
		// 否则物理键无效（不生成三级 catalog.schema.table）。
		if (!string.IsNullOrWhiteSpace(schema)
			&& !string.Equals(schema, catalog, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException(
				$"MySQL 物理键不一致：Schema『{schema}』与 Catalog(=数据库)『{catalog}』不匹配。");
		}

		if (!string.IsNullOrWhiteSpace(catalog))
			return EscapeIdentifier(catalog) + "." + EscapeIdentifier(table);
		return EscapeIdentifier(table);
	}


	public void AssertCatalogResolvable(
		string? catalog,
		string? currentCatalog)
	{
		// MySQL 支持跨 catalog 限定名（同一实例），无需拒绝。
	}


}