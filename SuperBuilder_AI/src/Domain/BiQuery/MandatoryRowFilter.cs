namespace SuperBuilder_AI.Models.BI;

/// <summary>Security-owned filter. It is never merged into or replaced by user filters.</summary>
public sealed class MandatoryRowFilter
{
	public long PolicyId { get; set; }
	public long MetadataTableId { get; set; }
	public long MetadataColumnId { get; set; }
	public string TableName { get; set; } = string.Empty;
	public string Field { get; set; } = string.Empty;
	public string? DataType { get; set; }
	public string Operator { get; set; } = "=";
	public string Value { get; set; } = string.Empty;
	public bool Deny { get; set; }
}
