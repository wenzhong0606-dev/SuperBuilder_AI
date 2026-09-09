using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using SuperBuilder_AI.Data;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P10.5 架构依赖方向校验（NetArchTest 等价能力，零外部 NuGet 依赖的反射实现）。
/// 说明：构建环境的 NuGet 镜像（nuget.azure.cn）未收录 NetArchTest 包，故以内建反射实现同等校验，
/// 校验「依赖只能向内」的核心不变量：
///   领域(Domain=SuperBuilder_AI.Models) 仅可依赖端口(Ports=SuperBuilder_AI.Interfaces)，不得反向依赖应用/API/基础设施；
///   端口(Ports) 不得反向依赖外层(应用/API/Middleware)；其对 SuperBuilder_AI.Data/SuperBuilder_AI.Infrastructure 的引用属单项目既有基础设施契约，不在强制范围；
///   应用(Application=SuperBuilder_AI.Services) 不得依赖 API(Controllers)/Middleware；
///   API(Controllers) 不得依赖 Middleware。
/// 注：Controllers 直接引用 SuperBuilder_AI.Data(SuperBIContext) 是单项目现状（A3 拆独立项目暂缓）的已知事实，
///     故「Api 不依赖 Infrastructure」在当前阶段不强制，待 A3 落地后再收紧。
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Target = typeof(SuperBIContext).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_Application_Or_Api_Or_Infrastructure()
    {
        var violations = FindViolations("SuperBuilder_AI.Models",
            "SuperBuilder_AI.Services", "SuperBuilder_AI.Controllers", "SuperBuilder_AI.Middleware",
            "SuperBuilder_AI.Data", "SuperBuilder_AI.Infrastructure");
        Assert.Empty(violations);
    }

    [Fact]
    public void Ports_Should_Not_Depend_On_Application_Or_Api_Or_Middleware()
    {
        // 注：端口层对 SuperBuilder_AI.Data / SuperBuilder_AI.Infrastructure 的引用（如 ISqlDialectResolver ↔ SqlDialect）
        // 属单项目现状的既有基础设施契约，不在本断言强制范围内；此处仅校验「端口不反向依赖外层(应用/API/中间件)」。
        var violations = FindViolations("SuperBuilder_AI.Interfaces",
            "SuperBuilder_AI.Services", "SuperBuilder_AI.Controllers", "SuperBuilder_AI.Middleware");
        Assert.Empty(violations);
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Api_Or_Middleware()
    {
        var violations = FindViolations("SuperBuilder_AI.Services",
            "SuperBuilder_AI.Controllers", "SuperBuilder_AI.Middleware");
        Assert.Empty(violations);
    }

    [Fact]
    public void Api_Should_Not_Depend_On_Middleware()
    {
        var violations = FindViolations("SuperBuilder_AI.Controllers",
            "SuperBuilder_AI.Middleware");
        Assert.Empty(violations);
    }

    // ---- M9-11：限界上下文解耦——消除 Metadata↔Organization 循环依赖 ----
    // 验收口径：无 Metadata↔Organization 循环。允许 Metadata 引用 Organization
    // （DataSource.Tenant / MetadataLearningRecord.Tenant 等 FK 导航属正常方向），
    // 但 Organization 不得反向引用 Metadata（此前 Tenant.DataSources 集合导航造成环）。
    // 以下不变量固化该方向约束，防止后续编辑重新引入环。

    [Fact]
    public void Organization_Should_Not_Depend_On_Metadata()
    {
        var violations = FindViolations("SuperBuilder_AI.Models.Organization",
            "SuperBuilder_AI.Models.Metadata");
        Assert.Empty(violations); // M9-11：Organization 上下文不得依赖 Metadata 上下文
    }

    /// <summary>扫描某层全部类型，返回所有「指向 forbidden 命名空间」的非法依赖。</summary>
    private static List<string> FindViolations(string layer, params string[] forbidden)
    {
        var violations = new List<string>();
        foreach (var t in Target.GetTypes())
        {
            if (t.Namespace == null) continue;
            if (!(t.Namespace == layer || t.Namespace.StartsWith(layer + "."))) continue;

            foreach (var refType in CollectReferencedTypes(t))
            {
                var ns = refType.Namespace;
                if (ns == null) continue;
                foreach (var f in forbidden)
                {
                    if (ns == f || ns.StartsWith(f + "."))
                        violations.Add($"{t.FullName} -> {refType.FullName} (forbidden namespace: {f})");
                }
            }
        }
        return violations;
    }

    /// <summary>收集一个类型直接/间接引用的全部类型（基类、接口、字段、属性、方法签名、特性、泛型/嵌套）。</summary>
    private static ISet<Type> CollectReferencedTypes(Type root)
    {
        var result = new HashSet<Type>();
        var toExpand = new Queue<Type>();

        void Consider(Type? x)
        {
            if (x == null) return;
            var elem = x;
            while (elem.HasElementType) elem = elem.GetElementType()!;
            if (elem.IsGenericParameter) return;
            result.Add(elem);
            toExpand.Enqueue(elem);
        }

        Consider(root.BaseType);
        foreach (var i in root.GetInterfaces()) Consider(i);
        foreach (var f in root.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            Consider(f.FieldType);
        foreach (var p in root.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            Consider(p.PropertyType);
        foreach (var m in root.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            Consider(m.ReturnType);
            foreach (var prm in m.GetParameters()) Consider(prm.ParameterType);
        }
        foreach (var a in root.CustomAttributes) Consider(a.AttributeType);
        foreach (var mem in root.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            foreach (var a in mem.CustomAttributes) Consider(a.AttributeType);

        while (toExpand.Count > 0)
        {
            var cur = toExpand.Dequeue();
            if (cur.IsGenericType)
                foreach (var g in cur.GetGenericArguments()) Consider(g);
            foreach (var n in cur.GetNestedTypes()) Consider(n);
        }

        result.Remove(root);
        return result;
    }

    // ---- M9-02：命名空间/目录边界一致性不变量（防回归）----
    // 项目采用「关注点命名空间」约定（Models=Domain、Services=Application、Controllers=Api、
    // Interfaces=Ports、Data/Infrastructure=Infra），目录仅作物理分层。以下不变量固化 M9-02 治理结果，
    // 防止后续脚手架/手工编辑再次引入命名空间偏差（此前的 src. 前缀、迁移双命名空间、Domain 层误用 Services.BI 等）。

    private static readonly string? SourceRoot = FindSourceRoot();

    private static string? FindSourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "SuperBuilder_AI", "src");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static IEnumerable<(string FilePath, string Namespace)> EnumerateSourceNamespaces(string? root)
    {
        if (root == null) yield break;
        foreach (var fp in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(fp);
            if (name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)) continue;
            var bytes = File.ReadAllBytes(fp);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                bytes = bytes[3..];
            var text = Encoding.UTF8.GetString(bytes);
            if (text.Length >= 400 && text.Substring(0, 400).Contains("auto-generated")) continue;
            yield return (fp, ParseNs(text));
        }
    }

    private static string ParseNs(string text)
    {
        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
        var m = Regex.Match(text, @"^\s*namespace\s+([A-Za-z0-9_.]+)\s*[\{;]", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    [Fact]
    public void SourceRoot_Should_Be_Discoverable()
    {
        Assert.NotNull(SourceRoot); // 测试必须能定位 SuperBuilder_AI/src，否则以下不变量无法执行
    }

    [Fact]
    public void NoNamespace_Should_Contain_Illegal_Src_Prefix()
    {
        var bad = EnumerateSourceNamespaces(SourceRoot)
            .Where(x => !string.IsNullOrEmpty(x.Namespace) &&
                        (x.Namespace.StartsWith("SuperBuilder_AI.src") || x.Namespace.Contains(".src.")))
            .ToList();
        Assert.Empty(bad); // M9-02 修复的 SuperBuilder_AI.src.Infrastructure.Persistence.Migrations 偏差不得复现
    }

    [Fact]
    public void AllMigrations_Should_Share_Single_Namespace()
    {
        const string expected = "SuperBuilder_AI.Infrastructure.Persistence.Migrations";
        var root = SourceRoot ?? throw new InvalidOperationException("SourceRoot not found");
        var migrationsDir = Path.Combine(root, "Infrastructure", "Persistence", "Migrations");
        var violations = Directory.EnumerateFiles(migrationsDir, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(fp => (fp, Ns: ParseNs(File.ReadAllText(fp))))
            .Where(x => x.Ns != expected)
            .ToList();
        Assert.Empty(violations); // 迁移必须统一在同一命名空间，避免 EF 发现路径分裂
    }

    [Fact]
    public void EverySourceFile_Should_Have_SuperBuilderAi_Namespace_ExceptProgram()
    {
        var violations = EnumerateSourceNamespaces(SourceRoot)
            .Where(x => Path.GetFileName(x.FilePath) != "Program.cs"
                        && (string.IsNullOrEmpty(x.Namespace) || !x.Namespace.StartsWith("SuperBuilder_AI.")))
            .ToList();
        Assert.Empty(violations); // 除 Program.cs（顶级语句）外，所有源文件须归属 SuperBuilder_AI 命名空间树
    }

    [Fact]
    public void DomainLayer_Should_Use_Models_Namespace()
    {
        var root = SourceRoot ?? throw new InvalidOperationException("SourceRoot not found");
        var domainDir = Path.Combine(root, "Domain");
        var violations = Directory.EnumerateFiles(domainDir, "*.cs", SearchOption.AllDirectories)
            .Select(fp => (fp, Ns: ParseNs(File.ReadAllText(fp))))
            .Where(x => !x.Ns.StartsWith("SuperBuilder_AI.Models"))
            .ToList();
        Assert.Empty(violations); // Domain 层文件须归属 Models 关注点命名空间（M9-02 修复的 Domain/BiQuery 中 Services.BI 偏差不得复现）
    }

    // ---- M9-03：BusinessTerm 强类型化不变量（防回归）----
    // 业务术语须以编译期强类型 BusinessTerm 流转，替代自由 string，
    // 防止后续脚手架/手工编辑重新引入裸 string 业务术语（拼写/大小写漂移与误用）。

    [Fact]
    public void BusinessTerm_Should_Be_ValueType_NotStringAlias()
    {
        var t = typeof(SuperBuilder_AI.Models.BI.BusinessTerm);
        Assert.True(t.IsValueType, "BusinessTerm 必须是值类型（非 string 别名），以在热路径避免分配并提供编译期约束");
        Assert.False(t == typeof(string), "BusinessTerm 不得退化成 string 别名");
    }

    [Fact]
    public void QueryPlanDiagnostics_BusinessTerms_ShouldBe_ListOfBusinessTerm()
    {
        var prop = typeof(SuperBuilder_AI.Services.BI.QueryPlanBuilder.QueryPlanDiagnostics)
            .GetProperty("BusinessTerms");
        Assert.NotNull(prop);
        var listArg = prop!.PropertyType.IsGenericType
            ? prop.PropertyType.GetGenericArguments()[0]
            : null;
        Assert.Equal(typeof(SuperBuilder_AI.Models.BI.BusinessTerm), listArg);
    }

    [Fact]
    public void BusinessTermExtractor_CollectBusinessTerms_ShouldReturn_ListOfBusinessTerm()
    {
        var method = typeof(SuperBuilder_AI.Services.BI.BusinessTermExtractor)
            .GetMethod("CollectBusinessTerms", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var ret = method!.ReturnType;
        Assert.True(ret.IsGenericType, "CollectBusinessTerms 应返回泛型集合");
        Assert.Equal(typeof(SuperBuilder_AI.Models.BI.BusinessTerm), ret.GetGenericArguments()[0]);
    }
}
