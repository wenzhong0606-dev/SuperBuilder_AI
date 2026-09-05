using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Theme;

namespace SuperBuilder_AI.Services.Seed;

/// <summary>
/// 默认主题种子（M2-07）。幂等播种平台内置默认主题（TenantId=0, Key="default"），
/// 供 <see cref="IThemeResolver"/> 在租户未指定主题时兜底。
/// </summary>
public interface IThemeSeedService
{
    /// <summary>确保内置默认主题存在（幂等）。</summary>
    Task EnsureSeededAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IThemeSeedService"/>
public sealed class ThemeSeedService : IThemeSeedService
{
    private readonly SuperBIContext _ctx;

    public ThemeSeedService(SuperBIContext ctx) => _ctx = ctx;

    public async Task EnsureSeededAsync(CancellationToken ct = default)
    {
        var exists = await _ctx.Themes
            .AnyAsync(t => t.TenantId == 0 && t.Key == BuiltInThemeKeys.Default, ct);
        if (exists) return;

        _ctx.Themes.Add(new Theme
        {
            TenantId = 0,
            Key = BuiltInThemeKeys.Default,
            Name = "默认主题",
            IsBuiltIn = true,
            DslVersion = ThemeDslVersions.Current,
            DslJson = JsonSerializer.Serialize(new ThemeDsl()),
        });
        await _ctx.SaveChangesAsync(ct);
    }
}
