using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces.BI.Evaluation;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Controllers;

[ApiController]
[Route("evaluation/diagnostics")]
public sealed class GoldenBaselineDiagnosticsController : ControllerBase
{
    private readonly IGoldenBaselineRegistry _registry;

    public GoldenBaselineDiagnosticsController(IGoldenBaselineRegistry registry)
    {
        _registry = registry;
    }

    [HttpGet("golden-baselines")]
    public ActionResult<object> GoldenBaselines()
    {
        var baselines = _registry.List();
        var current = _registry.GetCurrent();
        return Ok(new
        {
            passed = current is not null,
            count = baselines.Count,
            currentVersion = current?.Version,
            current,
            baselines
        });
    }

    [HttpGet("golden-baseline/{version}")]
    public ActionResult<GoldenBaseline> GoldenBaseline(string version)
    {
        var baseline = _registry.Get(version);
        return baseline is null ? NotFound() : Ok(baseline);
    }
}
