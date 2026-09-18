namespace SuperBuilder_AI.Application.Metadata;

public sealed record ScanTableFailure(
	string? CatalogName,
	string? SchemaName,
	string TableName,
	string Stage,
	string ErrorType,
	string ErrorMessage,
	int Attempts);
