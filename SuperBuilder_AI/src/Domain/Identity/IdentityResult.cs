using System.Collections.Generic;

namespace SuperBuilder_AI.Models.Identity;

/// <summary>Identity 操作结果（镜像 AgentResult，承载成功/失败与可选实体 Id）。</summary>
public class IdentityResult
{
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
    public long? Id { get; set; }

    public static IdentityResult Ok(long? id = null) => new() { Success = true, Id = id };
    public static IdentityResult Fail(params string[] errors) => new() { Success = false, Errors = errors.ToList() };
}
