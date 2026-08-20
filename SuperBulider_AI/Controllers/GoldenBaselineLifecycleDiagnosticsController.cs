using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces.BI.Evaluation;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBuilder_AI.Controllers;

[ApiController]
[Route("evaluation/diagnostics")]
public sealed class GoldenBaselineLifecycleDiagnosticsController : ControllerBase
{
    private readonly IGoldenBaselineRegistry _registry;
    private readonly IGoldenBaselinePersistence _persistence;
    private readonly GoldenBaselinePersistenceService _persistenceService;
    private readonly GoldenBaselineLifecycleValidator _validator;

    public GoldenBaselineLifecycleDiagnosticsController(
        IGoldenBaselineRegistry registry,
        IGoldenBaselinePersistence persistence,
        GoldenBaselinePersistenceService persistenceService,
        GoldenBaselineLifecycleValidator validator)
    {
        _registry = registry;
        _persistence = persistence;
        _persistenceService = persistenceService;
        _validator = validator;
    }

    [HttpGet("golden-baseline-lifecycle")]
    public async Task<ActionResult<object>> Lifecycle([FromQuery] string? version = null, CancellationToken cancellationToken = default)
    {
        var baseline = !string.IsNullOrWhiteSpace(version)
            ? _registry.Get(version)
            : _registry.GetCurrent();

        if (baseline is null)
        {
            var persisted = !string.IsNullOrWhiteSpace(version)
                ? await _persistence.GetAsync(version, cancellationToken)
                : (await _persistence.ListAsync(cancellationToken)).FirstOrDefault();
            if (persisted is not null)
                baseline = GoldenBaselinePersistenceMapper.ToBaseline(persisted);
        }

        if (baseline is null)
            return NotFound(new { passed = false, message = "No Golden Baseline is available for lifecycle validation." });

        var scorecard = await _validator.ValidateAsync(baseline, cancellationToken);
        return Ok(scorecard);
    }

    [HttpGet("golden-baseline-consistency")]
    public async Task<ActionResult<object>> Consistency(CancellationToken cancellationToken = default)
    {
        var registry = _registry.List();
        var persisted = await _persistence.ListAsync(cancellationToken);
        var registryVersions = registry.Select(x => x.Version).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var persistedVersions = persisted.Select(x => x.Version).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var registryOnly = registryVersions.Except(persistedVersions, StringComparer.OrdinalIgnoreCase).ToList();
        var persistenceOnly = persistedVersions.Except(registryVersions, StringComparer.OrdinalIgnoreCase).ToList();
        return Ok(new
        {
            passed = registryOnly.Count == 0 && persistenceOnly.Count == 0,
            registryCount = registry.Count,
            persistenceCount = persisted.Count,
            registryOnly,
            persistenceOnly,
            currentVersion = _registry.GetCurrent()?.Version
        });
    }
}
