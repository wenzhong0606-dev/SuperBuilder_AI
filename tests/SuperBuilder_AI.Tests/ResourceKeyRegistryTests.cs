using System.Collections.Generic;
using System.Linq;
using SuperBuilder_AI.Components.Localization;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Services.Platform;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M3-05 批2 防漂移护栏：资源键「注册表 / 元数据目录 / zh-CN 种子基线 / 前端 RCL 镜像」四处必须严格一致。
/// <para>
/// 背景：M3-05 批1 曾出现 ResourceKeys 扩到 ~110 键而 LocalizationSeedService 未同步，导致种子覆盖测试失败；
/// 本组测试把该类漂移变成编译期/测试期硬失败，防止再次发生。
/// </para>
/// </summary>
public sealed class ResourceKeyRegistryTests
{
    /// <summary>每个已登记键都必须有 Catalog 元数据（模块/页面归属 + en-US 默认值），否则无法产出英文基线。</summary>
    [Fact]
    public void All_RegisteredKeys_HaveCatalogMetadata_WithDefaultValue()
    {
        var missing = ResourceKeys.All()
            .Where(k => !ResourceKeys.Catalog.TryGetValue(k, out var meta) || string.IsNullOrWhiteSpace(meta.DefaultValue))
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// 每个已登记键都必须有 zh-CN 基线文案。缺失时种子会静默降级为英文（或键名），
    /// 而种子覆盖测试只校验「行存在」，无法发现这种静默降级——故在此显式断言。
    /// </summary>
    [Fact]
    public void All_RegisteredKeys_HaveZhCnBaseline()
    {
        var missing = ResourceKeys.All()
            .Where(k => !LocalizationSeedService.ZhCnDefaults.ContainsKey(k))
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>Catalog 与 ZhCnDefaults 均不得登记未声明的孤儿键（避免注册表与基线双向脱节）。</summary>
    [Fact]
    public void Catalog_And_ZhCnBaseline_ContainNoOrphanKeys()
    {
        var registered = new HashSet<string>(ResourceKeys.All());

        var orphanCatalog = ResourceKeys.Catalog.Keys.Where(k => !registered.Contains(k)).ToList();
        var orphanZhCn = LocalizationSeedService.ZhCnDefaults.Keys.Where(k => !registered.Contains(k)).ToList();

        Assert.Empty(orphanCatalog);
        Assert.Empty(orphanZhCn);
    }

    /// <summary>
    /// RCL 镜像 Keys.Defaults 与后端 ResourceKeys 的键集合必须完全一致（双向）。
    /// RCL 不能引用 EF 重型后端程序集，故自持一份常量副本；本测试是该副本的唯一正确性保证。
    /// </summary>
    [Fact]
    public void RclMirror_KeySet_MatchesBackendRegistry()
    {
        var backend = new HashSet<string>(ResourceKeys.All());
        var rcl = new HashSet<string>(Keys.Defaults.Keys);

        var missingInRcl = backend.Except(rcl).ToList();
        var extraInRcl = rcl.Except(backend).ToList();

        Assert.Empty(missingInRcl);
        Assert.Empty(extraInRcl);
        Assert.Equal(backend.Count, rcl.Count);
    }

    /// <summary>RCL 镜像的 zh-CN/en-US 默认值必须与后端基线一致，避免离线回退与服务端取值出现文案分歧。</summary>
    [Fact]
    public void RclMirror_DefaultValues_MatchBackendBaseline()
    {
        var mismatched = new List<string>();

        foreach (var (key, l10n) in Keys.Defaults)
        {
            if (!LocalizationSeedService.ZhCnDefaults.TryGetValue(key, out var zhCn) || zhCn != l10n.ZhCn)
                mismatched.Add($"{key}#zh-CN");

            if (!ResourceKeys.Catalog.TryGetValue(key, out var meta) || meta.DefaultValue != l10n.EnUs)
                mismatched.Add($"{key}#en-US");
        }

        Assert.Empty(mismatched);
    }

    /// <summary>注册表内不得有重复键（字典初始化器重复键只在运行期抛异常，编译期不可见）。</summary>
    [Fact]
    public void RegisteredKeys_AreUnique()
    {
        var all = ResourceKeys.All();
        var duplicates = all.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.Empty(duplicates);
    }
}
