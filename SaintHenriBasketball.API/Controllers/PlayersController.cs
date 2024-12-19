using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace SaintHenriBasketball.API.Controllers;

public class PlayersController : BaseApiController
{
    private readonly IPlayerService _playerService;
    private readonly ILogger<PlayersController> _logger;

    public PlayersController(IPlayerService playerService, ILogger<PlayersController> logger)
    {
        _playerService = playerService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlayerDto>> CreatePlayer([FromBody] CreatePlayerDto createPlayerDto)
    {
        try
        {
            var result = await _playerService.CreatePlayerAsync(createPlayerDto);
            return CreatedAtAction(nameof(GetPlayer), new { id = result.Id }, result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerDto>> GetPlayer(Guid id)
    {
        try
        {
            var player = await _playerService.GetPlayerAsync(id);
            return Ok(player);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PlayerDto>>> GetAllPlayers()
    {
        var players = await _playerService.GetAllPlayersAsync();
        return Ok(players);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePlayer(Guid id, [FromBody] UpdatePlayerDto updatePlayerDto)
    {
        try
        {
            await _playerService.UpdatePlayerAsync(id, updatePlayerDto);
            return NoContent();
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

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePlayer(Guid id)
    {
        try
        {
            await _playerService.DeletePlayerAsync(id);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}