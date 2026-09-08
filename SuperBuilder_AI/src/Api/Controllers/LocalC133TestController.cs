using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// C.13.3 本地开发测试入口。
/// 不创建 Test Project，直接通过 Controller 调用正式 Service 链。
/// </summary>
[ApiController]
[Route("evaluation/local-c13-3")]
public sealed class LocalC133TestController : ControllerBase
{
    private readonly IMetadataCsvFixtureService _fixtureService;
    private readonly IWebHostEnvironment _environment;

    public LocalC133TestController(
        IMetadataCsvFixtureService fixtureService,
        IWebHostEnvironment environment)
    {
        _fixtureService = fixtureService;
        _environment = environment;
    }

    /// <summary>
    /// C.13.3-04：从 Document CSV 导入 Metadata Fixture。
    /// </summary>
    [HttpPost("metadata-fixture/import")]
    public async Task<ActionResult<object>> ImportMetadataFixture()
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        try
        {
            var result = await _fixtureService.ImportAsync();
            return Ok(new
            {
                passed = result.TableCount > 0 && result.ColumnCount > 0 && result.SemanticCount > 0,
                stage = "MetadataCsvFixture",
                fixture = result
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                passed = false,
                stage = "MetadataCsvFixture",
                exceptionType = ex.GetType().FullName,
                message = ex.Message,
                innerMessage = ex.InnerException?.Message
            });
        }
    }

    /// <summary>
    /// C.13.3-05：读取当前 CSV Fixture 的预期来源位置，不修改任何数据。
    /// </summary>
    [HttpGet("metadata-fixture/source")]
    public ActionResult<object> MetadataFixtureSource()
    {
        var path = Path.Combine(_environment.ContentRootPath, "Document");
        return Ok(new
        {
            passed = Directory.Exists(path),
            contentRoot = _environment.ContentRootPath,
            documentPath = path,
            table = Path.Combine(path, "table.csv"),
            column = Path.Combine(path, "column.csv"),
            semantic = Path.Combine(path, "Semantic.csv"),
            tableExists = System.IO.File.Exists(Path.Combine(path, "table.csv")),
            columnExists = System.IO.File.Exists(Path.Combine(path, "column.csv")),
            semanticExists = System.IO.File.Exists(Path.Combine(path, "Semantic.csv"))
        });
    }
}
