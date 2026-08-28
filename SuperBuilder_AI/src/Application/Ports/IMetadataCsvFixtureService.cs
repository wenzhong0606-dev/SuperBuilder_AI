namespace SuperBuilder_AI.Interfaces;

/// <summary>
/// C.13.3 Controller 回归使用的 Metadata CSV Fixture 装载接口。
///
/// 仅用于 Development / Evaluation Fixture 环境。
/// 正式运行仍然从数据库读取 Metadata。
/// </summary>
public interface IMetadataCsvFixtureService
{
    Task<MetadataCsvFixtureResult> ImportAsync();
}

/// <summary>
/// Metadata CSV Fixture 导入结果。
/// </summary>
public sealed record MetadataCsvFixtureResult
{
    public int TableCount { get; init; }
    public int ColumnCount { get; init; }
    public int SemanticCount { get; init; }
}