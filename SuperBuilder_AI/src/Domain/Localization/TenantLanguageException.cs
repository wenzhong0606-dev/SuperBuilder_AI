namespace SuperBuilder_AI.Models.Localization;

/// <summary>租户语言配置领域规则违例（M3-01）。控制器捕获后映射为 400 BadRequest。</summary>
public sealed class TenantLanguageException : Exception
{
    public TenantLanguageException(string message) : base(message) { }
}
