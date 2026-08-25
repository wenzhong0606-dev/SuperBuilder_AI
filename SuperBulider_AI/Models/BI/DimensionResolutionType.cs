namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// Dimension 在当前 Metadata Snapshot 下的执行路径。
/// </summary>
public enum DimensionResolutionType
{
    NotResolved = 0,
    MasterJoin = 1,
    DirectKey = 2,
    Ambiguous = 3
}

/// <summary>
/// Dimension Resolution 的执行能力。
/// </summary>
public enum DimensionExecutionCapability
{
    NotExecutable = 0,
    Executable = 1
}
