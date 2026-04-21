using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.PublicSchedule;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[RequireFeature(FeatureFlagKeys.PublicSchedule)]
public class PublicScheduleController : ControllerBase
{
    private readonly ISessionRepository _sessionRepository;

    public PublicScheduleController(ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    [HttpGet("api/v{version:apiVersion}/public/sessions/upcoming")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<PublicSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublicSessionDto>>> GetUpcoming([FromQuery] int take = 12)
    {
        var upcoming = await _sessionRepository.GetUpcomingSessionsAsync();
        var result = upcoming
            .Where(s => s.Status == SessionStatus.Open)
            .OrderBy(s => s.SessionDate)
            .Take(Math.Clamp(take, 1, 30))
            .Select(s => new PublicSessionDto
            {
                Id = s.Id,
                SessionDate = s.SessionDate,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Location = s.Location,
                MaxCapacity = s.MaxCapacity,
                RegisteredPlayersCount = s.RegisteredPlayersCount,
                SpotsRemaining = Math.Max(0, s.MaxCapacity - s.RegisteredPlayersCount),
                DropInPrice = s.DropInPrice,
            })
            .ToList();
        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(result);
    }
}
