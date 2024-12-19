using SaintHenriBasketball.API.Controllers;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace SaintHenriBasketball.API.Controllers;

public class SessionsController : BaseApiController
{
    private readonly ISessionService _sessionService;
    private readonly IRegistrationService _registrationService;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ISessionService sessionService,
        IRegistrationService registrationService,
        ILogger<SessionsController> logger)
    {
        _sessionService = sessionService;
        _registrationService = registrationService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionDto>> CreateSession([FromBody] CreateSessionDto createSessionDto)
    {
        try
        {
            var result = await _sessionService.CreateSessionAsync(createSessionDto);
            return CreatedAtAction(nameof(GetSession), new { id = result.Id }, result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionDto>> GetSession(Guid id)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(id);
            return Ok(session);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SessionDto>>> GetUpcomingSessions()
    {
        var sessions = await _sessionService.GetUpcomingSessionsAsync();
        return Ok(sessions);
    }

    [HttpPost("{sessionId}/registrations/{playerId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionRegistrationDto>> RegisterPlayer(Guid sessionId, Guid playerId)
    {
        try
        {
            var registration = await _registrationService.RegisterPlayerForSessionAsync(playerId, sessionId);
            return Ok(registration);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{sessionId}/registrations/{playerId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelRegistration(Guid sessionId, Guid playerId)
    {
        try
        {
            await _registrationService.CancelRegistrationAsync(playerId, sessionId);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelSession(Guid id)
    {
        try
        {
            await _sessionService.CancelSessionAsync(id);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
