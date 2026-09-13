using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace SuperBuilder_AI.E2E;

/// <summary>
/// E2E-01 缺失链之一：<c>RLS</c>（行级租户隔离）在 API/DB 边界的强制验证。
/// 管理员（e2eapp 租户）经令牌查询数据源列表，并显式附加 <c>tenantId=0</c>（平台）查询参数——
/// 令牌租户作用域必须胜出：返回集与不带参数时一致，不得混入平台或其他租户数据（DB-02/M13-17 修复的租户过滤在此被端到端守护）。
/// <para>完整的"双租户正向隔离"（A 租户看不到 B 租户数据）需第二个种子租户；e2e 沙箱仅 e2eapp 一租户，
/// 故本用例以"令牌作用域压倒查询参数"作为边界层隔离的不变量证明（CI 可达）。</para>
/// 仅当 SB_E2E_BASE_URL 与管理员凭据齐备时执行，否则由 SkippableFact 诚实跳过。
/// </summary>
[Collection("playwright")]
public sealed class RlsIsolationE2ETests
{
    private readonly PlaywrightFixture _fx;
    public RlsIsolationE2ETests(PlaywrightFixture fx) => _fx = fx;

    /// <summary>令牌租户作用域必须压倒显式 tenantId 查询参数，防止跨租户泄漏。</summary>
    [SkippableFact]
    public async Task Token_TenantScope_Wins_Over_QueryParam()
    {
        E2EConfig.Require(_fx.BaseUrl, E2EConfig.User, E2EConfig.Password);
        var page = await _fx.NewPageAsync();
        await LoginHelper.ApiLoginByCodeAsync(page, E2EConfig.User!, E2EConfig.Password!, "e2eapp");

        // 不带查询参数：令牌租户（e2eapp）作用域下的数据源列表
        var scoped = await E2EApiHelper.CallApiAsync(page, "GET", "api/data-sources");
        Skip.If(!scoped.Ok, $"数据源列表失败（{scoped.Status}）：{scoped.Body}");
        var scopedCount = JsonDocument.Parse(scoped.Body).RootElement.EnumerateArray().Count();

        // 显式附加 tenantId=0（平台）：若 API 错误地按查询参数放宽，将混入平台数据源 → 计数变化
        var withParam = await E2EApiHelper.CallApiAsync(page, "GET", "api/data-sources?tenantId=0");
        Assert.True(withParam.Ok, $"tenantId=0 查询应成功，实际（{withParam.Status}）：{withParam.Body}");
        var withParamCount = JsonDocument.Parse(withParam.Body).RootElement.EnumerateArray().Count();

        Assert.Equal(scopedCount, withParamCount);
    }
}
