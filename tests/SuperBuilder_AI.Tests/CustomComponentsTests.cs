using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.AppBuilder;
using SuperBuilder_AI.Models.Components;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.AppBuilder;
using SuperBuilder_AI.Services.Components;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>M7-09 自定义组件的安全 DSL、租户隔离与不可变发布版本闭环。</summary>
public sealed class CustomComponentsTests
{
    private static SuperBIContext CreateContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        var db = new SuperBIContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static CustomComponentDslSerializer Serializer() => new(new AppDslSerializer());

    private static ComponentsController Controller(SuperBIContext db, long tenantId, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new("tid", tenantId.ToString()),
            new(ClaimTypes.Name, "component-owner")
        };
        claims.AddRange(permissions.Select(x => new Claim("perm", x)));
        return new ComponentsController(db, Serializer())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
                }
            }
        };
    }

    private static string Dsl(string name, string key = "stock-card", string type = AppComponentTypes.Kpi)
    {
        var serializer = Serializer();
        return serializer.Serialize(new CustomComponentDsl
        {
            Key = key,
            Name = name,
            Description = $"{name} description",
            Root = new ComponentPlan
            {
                Id = "root",
                Type = type,
                Title = name,
                Order = 1,
                Properties = new Dictionary<string, string> { ["format"] = "N2" },
                Style = new AppComponentStyle { Palette = "primary", ShowBorder = true, Padding = "normal" }
            }
        });
    }

    [Fact]
    public void Serializer_Rejects_ExecutableMarkup_And_UnknownTypes()
    {
        var serializer = Serializer();

        Assert.False(serializer.TryDeserialize(Dsl("<script>alert(1)</script>"), out _, out var markupErrors));
        Assert.Contains(markupErrors, x => x.Contains("HTML", StringComparison.OrdinalIgnoreCase));

        Assert.False(serializer.TryDeserialize(Dsl("Unknown", type: "raw-html"), out _, out var typeErrors));
        Assert.Contains(typeErrors, x => x.Contains("类型", StringComparison.OrdinalIgnoreCase));

        Assert.True(serializer.TryDeserialize(serializer.Blueprint(AppComponentTypes.Table), out var blueprint, out var validErrors));
        Assert.Empty(validErrors);
        Assert.Equal(AppComponentTypes.Table, blueprint!.Root.Type);
    }

    [Fact]
    public async Task Crud_Is_TenantScoped_And_Requires_FineGrainedPermission()
    {
        using var db = CreateContext(out var connection);
        await using var _ = connection;
        var all = new[]
        {
            IdentityPermissions.AppView, IdentityPermissions.AppCreate, IdentityPermissions.AppEdit,
            IdentityPermissions.AppDelete, IdentityPermissions.AppPublish
        };
        var tenant1 = Controller(db, 1, all);

        var createdResult = await tenant1.Create(new ComponentsController.SaveComponentRequest(1, Dsl("Stock card")), default);
        Assert.IsType<ComponentsController.ComponentDetail>(Assert.IsType<CreatedAtActionResult>(createdResult).Value);

        var list = Assert.IsType<List<ComponentsController.ComponentSummary>>(Assert.IsType<OkObjectResult>(await tenant1.List(1)).Value);
        Assert.Single(list);

        var updated = Assert.IsType<ComponentsController.ComponentDetail>(Assert.IsType<OkObjectResult>(
            await tenant1.Update("stock-card", new ComponentsController.SaveComponentRequest(1, Dsl("Updated stock card")), 1)).Value);
        Assert.Equal("Updated stock card", updated.Name);

        var tenant2 = Controller(db, 2, all);
        await Assert.ThrowsAsync<SuperBuilderException>(() => tenant2.List(1));

        var readOnly = Controller(db, 1, IdentityPermissions.AppView);
        var denied = Assert.IsType<ObjectResult>(await readOnly.Create(new ComponentsController.SaveComponentRequest(1, Dsl("Denied", "denied-card")), default));
        Assert.Equal(StatusCodes.Status403Forbidden, denied.StatusCode);

        Assert.IsType<NoContentResult>(await tenant1.Delete("stock-card", 1));
        Assert.Empty(Assert.IsType<List<ComponentsController.ComponentSummary>>(
            Assert.IsType<OkObjectResult>(await tenant1.List(1)).Value));
    }

    [Fact]
    public async Task Publish_Keeps_Draft_Isolated_And_Rollback_Creates_NewVersion()
    {
        using var db = CreateContext(out var connection);
        await using var _ = connection;
        var permissions = new[]
        {
            IdentityPermissions.AppView, IdentityPermissions.AppCreate,
            IdentityPermissions.AppEdit, IdentityPermissions.AppPublish
        };
        var controller = Controller(db, 7, permissions);

        await controller.Create(new ComponentsController.SaveComponentRequest(7, Dsl("Version one")), default);
        var first = Assert.IsType<ComponentsController.ComponentPublishResult>(
            Assert.IsType<OkObjectResult>(await controller.Publish("stock-card", 7)).Value);
        Assert.Equal(1, first.Version);

        await controller.Update("stock-card", new ComponentsController.SaveComponentRequest(7, Dsl("Draft two")), 7);
        var publishedV1 = Assert.IsType<ComponentsController.ComponentRenderView>(
            Assert.IsType<OkObjectResult>(await controller.Render("stock-card", 7)).Value);
        Assert.Equal("Version one", publishedV1.Root.Title);

        var second = Assert.IsType<ComponentsController.ComponentPublishResult>(
            Assert.IsType<OkObjectResult>(await controller.Publish("stock-card", 7)).Value);
        Assert.Equal(2, second.Version);

        var rollback = Assert.IsType<ComponentsController.ComponentPublishResult>(
            Assert.IsType<OkObjectResult>(await controller.Rollback("stock-card", 1, 7)).Value);
        Assert.Equal(3, rollback.Version);
        Assert.Equal(1, rollback.RolledBackFromVersion);

        var rendered = Assert.IsType<ComponentsController.ComponentRenderView>(
            Assert.IsType<OkObjectResult>(await controller.Render("stock-card", 7)).Value);
        Assert.Equal(3, rendered.Version);
        Assert.Equal("Version one", rendered.Root.Title);

        var versions = Assert.IsType<List<ComponentsController.ComponentVersionSummary>>(
            Assert.IsType<OkObjectResult>(await controller.Versions("stock-card", 7)).Value);
        Assert.Equal(new[] { 3, 2, 1 }, versions.Select(x => x.Version));
        Assert.Equal(3, await db.CustomComponentVersions.IgnoreQueryFilters().CountAsync());
    }
}
