using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.PublicSchedule;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.API.Controllers;

/// The season price for people with no account.
///
/// The public schedule and the /plan page used to ask SeasonsController, which is [Authorize] on the
/// whole class, so every signed-out visitor got a 401 and saw "—" (or $0) instead of the price.
///
/// Not behind the public-schedule flag: /plan is always public and needs the price regardless.
[ApiVersion("1.0")]
[ApiController]
[AllowAnonymous]
public class PublicSeasonController : ControllerBase
{
    private const string CacheKey = "PublicSeason:Current";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly ISeasonRepository _seasons;
    private readonly ISeasonPlanChoiceRepository _choices;
    private readonly IFeatureFlagService _flags;
    private readonly ICacheService _cache;

    public PublicSeasonController(
        ISeasonRepository seasons,
        ISeasonPlanChoiceRepository choices,
        IFeatureFlagService flags,
        ICacheService cache)
    {
        _seasons = seasons;
        _choices = choices;
        _flags = flags;
        _cache = cache;
    }

    /// The open season, or 204 when none is open.
    [HttpGet("api/v{version:apiVersion}/public/seasons/current")]
    [ProducesResponseType(typeof(PublicSeasonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<PublicSeasonDto>> GetCurrent()
    {
        var spotsEnforced = await _flags.IsEnabledAsync(FeatureFlagKeys.SeasonPlanChoice);
        var key = $"{CacheKey}:{(spotsEnforced ? "spots" : "plain")}";

        var cached = await _cache.GetAsync<PublicSeasonDto>(key);
        if (cached is null)
        {
            var season = await _seasons.GetCurrentSeasonAsync();
            if (season is null) return NoContent();

            cached = new PublicSeasonDto
            {
                Name = season.Name,
                StartDate = season.StartDate,
                EndDate = season.EndDate,
                Price = season.Price,
            };

            if (spotsEnforced)
            {
                // The same definition of a taken spot as the dashboard and the plan page. Counting
                // only paid passes here left the countdown advertising a full season as empty.
                var taken = (await _choices.GetSpotHolderIdsAsync(season.Id, includeProfilePlan: true)).Count;
                cached.Capacity = season.SeasonPassCapacity;
                cached.SpotsLeft = Math.Max(0, season.SeasonPassCapacity - taken);
            }

            await _cache.SetAsync(key, cached, CacheTtl);
        }

        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(cached);
    }
}
