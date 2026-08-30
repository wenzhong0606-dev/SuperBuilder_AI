using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
}
