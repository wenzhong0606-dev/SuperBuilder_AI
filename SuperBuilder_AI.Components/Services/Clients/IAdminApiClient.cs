using System.Collections.Generic;
using SuperBuilder_AI.Components.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 平台管理域客户端契约（M9-01）：语言目录（含公开目录）与演示数据安装。
/// 与 <see cref="IApiClient"/> 中对应方法签名一致，可独立于其他域单独测试/替换。
/// </summary>
public interface IAdminApiClient
{
    /// <summary>M3-02 平台管理员查看全部语言目录（含已停用与翻译进度）。</summary>
    Task<(IReadOnlyList<AdminLanguageView>? Result, string? Error)> GetAdminLanguagesAsync(CancellationToken ct = default);

    /// <summary>M3-02 平台管理员新建语言（BCP 47 归一化 + 必填名 + 复制键集合待翻译）。</summary>
    Task<(bool Ok, string? Error)> CreateLanguageAsync(AdminLanguageCreate model, CancellationToken ct = default);

    /// <summary>M3-02 平台管理员更新语言显示名/本地名/排序。</summary>
    Task<(bool Ok, string? Error)> UpdateLanguageAsync(long id, AdminLanguageUpdate model, CancellationToken ct = default);

    /// <summary>M3-02 平台管理员启用/停用语言（停用委托租户关系迁移）。</summary>
    Task<(bool Ok, string? Error)> SetLanguageEnabledAsync(long id, bool enabled, CancellationToken ct = default);

    /// <summary>M3-02 平台管理员按 Id 顺序重排语言目录。</summary>
    Task<(bool Ok, string? Error)> ReorderLanguagesAsync(IReadOnlyList<long> orderedIds, CancellationToken ct = default);

    /// <summary>读取平台公开语言目录（含本地名称），供语言切换器展示 NativeName。</summary>
    Task<(IReadOnlyList<PublicLanguageView>? Result, string? Error)> GetPublicLanguagesAsync(CancellationToken ct = default);

    /// <summary>M2-07 平台管理员预览演示数据安装计划（需 platform:admin:manage）。</summary>
    Task<(DemoInstallPlan? Result, string? Error)> GetDemoDataPlanAsync(CancellationToken ct = default);

    /// <summary>M2-07 平台管理员触发演示数据安装（事务原子、重复执行保护，需 platform:admin:manage）。</summary>
    Task<(DemoInstallResult? Result, string? Error, string? Code)> InstallDemoDataAsync(CancellationToken ct = default);
}
