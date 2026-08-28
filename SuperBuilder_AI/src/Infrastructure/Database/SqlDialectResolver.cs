using SuperBuilder_AI.Interfaces.Database;


namespace SuperBuilder_AI.Infrastructure.Database;

/// <summary>
/// SQL方言解析器。
///
/// 根据DataSource.DbType
/// 返回对应SQL方言。
/// </summary>
public class SqlDialectResolver :
	ISqlDialectResolver
{


	private readonly Dictionary<string, ISqlDialect>
		_dialects;



	public SqlDialectResolver(
		IEnumerable<ISqlDialect> dialects)
	{

		_dialects =
			dialects
			.ToDictionary(
				x => x.Name.ToUpper());

	}



	/// <summary>
	/// 获取数据库方言。
	/// </summary>
	public ISqlDialect Resolve(
		string dbType)
	{

		if (_dialects.TryGetValue(
			dbType.ToUpper(),
			out var dialect))
		{
			return dialect;
		}



		throw new NotSupportedException(
			$"不支持数据库类型:{dbType}");

	}

}