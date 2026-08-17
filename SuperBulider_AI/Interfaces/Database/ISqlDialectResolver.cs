using SuperBulider_AI.Infrastructure.Database;


namespace SuperBulider_AI.Interfaces.Database;


public interface ISqlDialectResolver
{

	ISqlDialect Resolve(
		string dbType);

}