namespace SuperBuilder_AI.Controllers;

/// <summary>
/// M7-05：业务实体新建/编辑请求体（仅标量字段）。
/// 聚合子项（Keys/Attributes/Metrics/Relationships/PhysicalBindings）的编辑留待后续里程碑，
/// 本请求只承载实体级元数据，保持闭环边界清晰、风险可控。
/// </summary>
public sealed class BusinessEntityUpsertRequest
{
    /// <summary>业务唯一键（租户内稳定标识，必填）。</summary>
    public string BusinessKey { get; set; } = string.Empty;

    /// <summary>实体名称（业务表名映射，必填）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>展示名（可选）。</summary>
    public string? DisplayName { get; set; }

    /// <summary>描述（可选）。</summary>
    public string? Description { get; set; }

    /// <summary>所属业务域（可选）。</summary>
    public string? BusinessDomain { get; set; }

    /// <summary>语义文本/口径说明（可选）。</summary>
    public string? SemanticText { get; set; }

    /// <summary>状态（可选，默认 Active）。</summary>
    public string? Status { get; set; }
}
