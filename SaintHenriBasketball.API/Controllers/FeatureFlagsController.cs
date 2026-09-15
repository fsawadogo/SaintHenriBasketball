using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.FeatureFlags;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
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

    /// Flags the client app gates on. Anonymous callers and players get public flags only;
    /// an authenticated admin also receives admin-only flags so admin screens can gate on them.
    /// While volunteer-roles is on, a volunteer also receives the admin-only flags their staff screens gate on.
    [HttpGet("api/v{version:apiVersion}/feature-flags/public")]
    [AllowAnonymous]
    [SaintHenriBasketball.API.Filters.AllowTwoFactorEnrollment]
    [ProducesResponseType(typeof(IReadOnlyDictionary<string, bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyDictionary<string, bool>>> GetPublic()
    {
        var authenticated = User.Identity?.IsAuthenticated == true;
        var includeAdminOnly = authenticated && User.IsInRole("Admin");
        var flags = await _featureFlagService.GetClientFlagsAsync(includeAdminOnly);
        Response.Headers.Vary = "Authorization";

        var staffRole = authenticated && !includeAdminOnly ? StaffAccess.RoleFromClaim(User.FindFirstValue(StaffAccess.ClaimType)) : null;
        if (staffRole is { } role && role != Domain.Enums.StaffRole.None && await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.VolunteerRoles))
        {
            var adminFlags = await _featureFlagService.GetClientFlagsAsync(includeAdminOnly: true);
            var merged = new Dictionary<string, bool>(flags);
            foreach (var key in StaffAccess.ClientFlagKeys(role))
                if (adminFlags.TryGetValue(key, out var enabled)) merged[key] = enabled;
            return Ok(merged);
        }
        return Ok(flags);
    }

    // Idempotent: existing rows keep their Enabled state; missing rows are created disabled.
    [HttpPost("api/v{version:apiVersion}/admin/feature-flags/seed-defaults")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureFlagDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeatureFlagDto>>> SeedDefaults()
    {
        var flags = await _featureFlagService.SeedDefaultsAsync(FeatureFlagDefinitions.All);
        _logger.LogInformation("Feature flags re-seeded; {Count} flags now registered", flags.Count);
        return Ok(flags);
    }
}

public class ToggleFeatureFlagDto
{
    public bool Enabled { get; set; }
}
