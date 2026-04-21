using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.FeatureFlags;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;
using System.Security.Claims;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
public class FeatureFlagsController : BaseApiController
{
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<FeatureFlagsController> _logger;

    public FeatureFlagsController(
        IFeatureFlagService featureFlagService,
        ILogger<FeatureFlagsController> logger)
    {
        _featureFlagService = featureFlagService;
        _logger = logger;
    }

    [HttpGet("api/v{version:apiVersion}/admin/feature-flags")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureFlagDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeatureFlagDto>>> GetAll()
    {
        var flags = await _featureFlagService.GetAllAsync();
        return Ok(flags);
    }

    [HttpPut("api/v{version:apiVersion}/admin/feature-flags/{key}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(FeatureFlagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeatureFlagDto>> Toggle(string key, [FromBody] ToggleFeatureFlagDto body)
    {
        try
        {
            var adminId = GetUserId();
            var adminName = User?.FindFirstValue(ClaimTypes.Name)
                            ?? User?.FindFirstValue(ClaimTypes.Email)
                            ?? "Admin";

            var updated = await _featureFlagService.SetEnabledAsync(key, body.Enabled, adminId, adminName);
            return Ok(updated);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Toggle attempt on unknown feature flag {Key}", key);
            return NotFound(ex.Message);
        }
    }

    [HttpGet("api/v{version:apiVersion}/feature-flags/public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyDictionary<string, bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyDictionary<string, bool>>> GetPublic()
    {
        var flags = await _featureFlagService.GetPublicFlagsAsync();
        return Ok(flags);
    }

    // Re-runs the startup seeder. Safe to call repeatedly — existing flag rows
    // keep their Enabled state; missing rows get created with Enabled=false.
    [HttpPost("api/v{version:apiVersion}/admin/feature-flags/seed-defaults")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureFlagDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeatureFlagDto>>> SeedDefaults()
    {
        await _featureFlagService.SeedDefaultsAsync(FeatureFlagDefinitions.All);
        var flags = await _featureFlagService.GetAllAsync();
        _logger.LogInformation("Feature flags re-seeded; {Count} flags now registered", flags.Count);
        return Ok(flags);
    }
}

public class ToggleFeatureFlagDto
{
    public bool Enabled { get; set; }
}
