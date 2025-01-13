using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SeasonsController : BaseApiController
{
    private readonly ISeasonService _seasonService;
    private readonly ILogger<SeasonsController> _logger;

    public SeasonsController(
        ISeasonService seasonService,
        ILogger<SeasonsController> logger)
    {
        _seasonService = seasonService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new season (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeasonDto>> CreateSeason([FromBody] CreateSeasonDto createSeasonDto)
    {
        try
        {
            var result = await _seasonService.CreateSeasonAsync(createSeasonDto);
            return CreatedAtAction(nameof(GetSeason), new { id = result.Id }, result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get all seasons
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeasonDto>>> GetAllSeasons()
    {
        var seasons = await _seasonService.GetAllSeasonsAsync();
        return Ok(seasons);
    }

    /// <summary>
    /// Get current active season
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonDto>> GetCurrentSeason()
    {
        try
        {
            var season = await _seasonService.GetCurrentSeasonAsync();
            return Ok(season);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Get a specific season by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonDto>> GetSeason(Guid id)
    {
        try
        {
            var season = await _seasonService.GetSeasonAsync(id);
            return Ok(season);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Update season status (Admin only)
    /// </summary>
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSeasonStatus(Guid id, [FromBody] SeasonStatus status)
    {
        try
        {
            await _seasonService.UpdateSeasonStatusAsync(id, status);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Update a season (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSeason(Guid id, [FromBody] UpdateSeasonDto updateSeasonDto)
    {
        try
        {
            await _seasonService.UpdateSeasonAsync(id, updateSeasonDto);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Delete a season (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSeason(Guid id)
    {
        try
        {
            await _seasonService.DeleteSeasonAsync(id);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Register current user for a season
    /// </summary>
    [HttpPost("{seasonId}/register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonDto>> RegisterForSeason(Guid seasonId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var result = await _seasonService.RegisterUserForSeasonAsync(seasonId, Guid.Parse(userId));
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Get all users registered for a season (Admin only)
    /// </summary>
    [HttpGet("{seasonId}/users")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<SeasonUserDto>>> GetRegisteredUsers(Guid seasonId)
    {
        try
        {
            var users = await _seasonService.GetRegisteredUsersAsync(seasonId);
            return Ok(users);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Unregister current user from a season
    /// </summary>
    [HttpDelete("{seasonId}/register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnregisterFromSeason(Guid seasonId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _seasonService.UnregisterUserFromSeasonAsync(seasonId, Guid.Parse(userId));
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}