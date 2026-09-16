using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.PublicSchedule;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[RequireFeature(FeatureFlagKeys.PublicSchedule)]
public class PublicScheduleController : ControllerBase
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly ISessionRepository _sessionRepository;
    private readonly ICacheService _cache;
    private readonly IFeatureFlagService _flags;

    public PublicScheduleController(ISessionRepository sessionRepository, ICacheService cache, IFeatureFlagService flags)
    {
        _sessionRepository = sessionRepository;
        _cache = cache;
        _flags = flags;
    }

    [HttpGet("api/v{version:apiVersion}/public/sessions/upcoming")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<PublicSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublicSessionDto>>> GetUpcoming([FromQuery] int take = 12)
    {
        var clampedTake = Math.Clamp(take, 1, 30);
        // Full sessions are listed with a badge only while the wizard flag is on, so the key carries the choice.
        var includeFull = await _flags.IsEnabledAsync(FeatureFlagKeys.SeasonScheduleWizard);
        var cacheKey = $"PublicSchedule:Upcoming:{(includeFull ? "full" : "open")}-{clampedTake}";

        var cached = await _cache.GetAsync<List<PublicSessionDto>>(cacheKey);
        if (cached is not null)
        {
            Response.Headers["Cache-Control"] = "public, max-age=60";
            return Ok(cached);
        }

        var upcoming = await _sessionRepository.GetUpcomingSessionsAsync();
        var result = PublicScheduleSelector.Select(upcoming, DateTime.UtcNow, clampedTake, includeFull);

        await _cache.SetAsync(cacheKey, result, CacheTtl);
        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(result);
    }
}
