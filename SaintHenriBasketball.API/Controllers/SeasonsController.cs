using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using System.Security.Claims;

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating season");
            return StatusCode(500, "An error occurred while creating the season");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting season {SeasonId}", id);
            return StatusCode(500, "An error occurred while retrieving the season");
        }
    }

    /// <summary>
    /// Get all seasons
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeasonDto>>> GetAllSeasons()
    {
        try
        {
            var seasons = await _seasonService.GetAllSeasonsAsync();
            return Ok(seasons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all seasons");
            return StatusCode(500, "An error occurred while retrieving the seasons");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating season {SeasonId}", id);
            return StatusCode(500, "An error occurred while updating the season");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting season {SeasonId}", id);
            return StatusCode(500, "An error occurred while deleting the season");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering for season {SeasonId}", seasonId);
            return StatusCode(500, "An error occurred while registering for the season");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering from season {SeasonId}", seasonId);
            return StatusCode(500, "An error occurred while unregistering from the season");
        }
    }
}