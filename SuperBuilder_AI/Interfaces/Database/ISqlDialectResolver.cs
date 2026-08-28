using SuperBuilder_AI.Infrastructure.Database;


namespace SuperBuilder_AI.Interfaces.Database;


public interface ISqlDialectResolver
{

	ISqlDialect Resolve(
		string dbType);

}