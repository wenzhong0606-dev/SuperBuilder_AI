using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    private static readonly Regex LiteralTranslationKey =
        new("L10n\\.T\\(\\s*\\\"(?<key>[^\\\"]+)\\\"(?=\\s*[,\\)])", RegexOptions.Compiled);

    private static readonly Regex FormatPlaceholder =
        new(@"\{\d+(?::[^{}]+)?\}", RegexOptions.Compiled);

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SuperBulider_AI.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root.");
    }

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

    /// <summary>源码中以字符串字面量调用 L10n.T 的键必须存在于注册表，拼写错误在 CI 阶段直接失败。</summary>
    [Fact]
    public void Literal_TranslationKeys_Used_By_Components_AreRegistered()
    {
        var known = new HashSet<string>(Keys.Defaults.Keys, StringComparer.Ordinal);
        var componentRoot = Path.Combine(FindRepositoryRoot(), "SuperBuilder_AI.Components");
        var unknown = Directory.EnumerateFiles(componentRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(path => LiteralTranslationKey.Matches(File.ReadAllText(path))
                .Select(match => (Path: Path.GetRelativePath(componentRoot, path), Key: match.Groups["key"].Value)))
            .Where(item => !known.Contains(item.Key))
            .Select(item => $"{item.Path}: {item.Key}")
            .OrderBy(item => item)
            .ToList();

        Assert.Empty(unknown);
    }

    /// <summary>中英文基线中的格式化占位符必须完全一致，避免切换语言后 FormatException 或参数错位。</summary>
    [Fact]
    public void RclMirror_FormattingPlaceholders_MatchAcrossCultures()
    {
        var mismatched = Keys.Defaults
            .Where(pair =>
            {
                var zh = FormatPlaceholder.Matches(pair.Value.ZhCn).Select(match => match.Value).OrderBy(x => x);
                var en = FormatPlaceholder.Matches(pair.Value.EnUs).Select(match => match.Value).OrderBy(x => x);
                return !zh.SequenceEqual(en, StringComparer.Ordinal);
            })
            .Select(pair => pair.Key)
            .ToList();

        Assert.Empty(mismatched);
    }

    /// <summary>
    /// 防止页面重新引入最常见的裸中文：文本节点和 placeholder 必须经过 L10n。
    /// PageHead 的 Title/Desc 是显式资源键的离线回退，不属于裸文本。
    /// </summary>
    [Fact]
    public void Razor_Markup_ContainsNo_Unlocalized_Cjk_TextNodes_Or_Placeholders()
    {
        var componentRoot = Path.Combine(FindRepositoryRoot(), "SuperBuilder_AI.Components");
        var visibleCjk = new Regex(
            @"placeholder\s*=\s*\""(?!@)[^\""]*[\u3400-\u9fff]|>\s*[\u3400-\u9fff][^<@]*<",
            RegexOptions.Compiled);
        var violations = new List<string>();

        foreach (var path in Directory.EnumerateFiles(componentRoot, "*.razor", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("@*", StringComparison.Ordinal)
                    || trimmed.StartsWith("//", StringComparison.Ordinal)
                    || trimmed.StartsWith("///", StringComparison.Ordinal)
                    || trimmed.StartsWith("*", StringComparison.Ordinal)
                    || line.Contains("L10n.T(", StringComparison.Ordinal))
                    continue;

                if (visibleCjk.IsMatch(line))
                    violations.Add($"{Path.GetRelativePath(componentRoot, path)}:{index + 1}");
            }
        }

        Assert.Empty(violations);
    }
}
