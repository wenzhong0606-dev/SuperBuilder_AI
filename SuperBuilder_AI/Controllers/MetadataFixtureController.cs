using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// C.13.3 Controller 回归专用 Metadata CSV Fixture 接口。
///
/// 仅允许 Development 环境使用。
/// 正式运行仍然从数据库读取 Metadata。
/// </summary>
[ApiController]
[Route("evaluation/diagnostics/metadata-fixture")]
public sealed class MetadataFixtureController : ControllerBase
{
    private readonly IMetadataCsvFixtureService _fixtureService;
    private readonly IWebHostEnvironment _environment;

    public MetadataFixtureController(
        IMetadataCsvFixtureService fixtureService,
        IWebHostEnvironment environment)
    {
        _fixtureService = fixtureService;
        _environment = environment;
    }

    [HttpGet("import")]
    public async Task<ActionResult<MetadataCsvFixtureResult>> Import()
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        var result = await _fixtureService.ImportAsync();
        return Ok(new
        {
            passed = result.TableCount > 0 && result.ColumnCount > 0 && result.SemanticCount > 0,
            fixture = result
        });
    }
}