using System;
using Xunit;

namespace SuperBuilder_AI.E2E;

/// <summary>
/// 从环境变量读取 E2E 集成凭据。任一必需项缺失即抛出 <see cref="SkipException"/>，
/// 由 xUnit 标记为跳过而非失败——这是 CI 前的诚实占位，而非"假成功"。
/// </summary>
internal static class E2EConfig
{
    public static string BaseUrl => Environment.GetEnvironmentVariable("SB_E2E_BASE_URL") ?? "http://localhost:5080";
    /// <summary>后端 API 基地址。Playwright 的 <c>evaluate(fetch)</c> 是浏览器同源请求，
    /// 而 Web 宿主（<see cref="BaseUrl"/>）不转发 <c>/api/**</c>，故必须显式指向 API。</summary>
    public static string ApiUrl => Environment.GetEnvironmentVariable("SB_E2E_API_URL") ?? "http://localhost:5032";
    public static string? User => Environment.GetEnvironmentVariable("SB_E2E_USER");
    public static string? Password => Environment.GetEnvironmentVariable("SB_E2E_PASSWORD");
    public static string? Tenant => Environment.GetEnvironmentVariable("SB_E2E_TENANT");
    public static string? ReaderUser => Environment.GetEnvironmentVariable("SB_E2E_READER_USER");
    public static string? ReaderPassword => Environment.GetEnvironmentVariable("SB_E2E_READER_PASSWORD");
    public static string? ReaderTenant => Environment.GetEnvironmentVariable("SB_E2E_READER_TENANT");
    /// <summary>平台管理员账号（属 <c>platform</c> 租户，须与业务管理员分开配置）。</summary>
    public static string? PlatformUser => Environment.GetEnvironmentVariable("SB_E2E_PLATFORM_USER");
    public static string? PlatformPassword => Environment.GetEnvironmentVariable("SB_E2E_PLATFORM_PASSWORD");
    public static string SwitchCulture => Environment.GetEnvironmentVariable("SB_E2E_SWITCH_CULTURE") ?? "en-US";

    public static void Require(params string?[] values)
    {
        foreach (var v in values)
        {
            Skip.If(string.IsNullOrWhiteSpace(v),
                "E2E 集成环境未配置：请设置 SB_E2E_BASE_URL 及对应凭据（见 tests/SuperBuilder_AI.E2E.Tests/README.md）后运行。");
        }
    }
}
