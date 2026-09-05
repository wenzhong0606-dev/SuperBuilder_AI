namespace SuperBuilder_AI.Models.Localization;

/// <summary>平台语言目录领域规则违例（M3-02）。控制器捕获后映射为 400 BadRequest。</summary>
public sealed class PlatformLanguageException : Exception
{
    public PlatformLanguageException(string message) : base(message) { }
}
