using AutoMapper;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class PlayerService : IPlayerService
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<PlayerService> _logger;

    public PlayerService(
        IPlayerRepository playerRepository,
        IMapper mapper,
        ILogger<PlayerService> logger)
    {
        _playerRepository = playerRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PlayerDto> CreatePlayerAsync(CreatePlayerDto createPlayerDto)
    {
        // Validate email uniqueness
        var existingPlayer = await _playerRepository.GetPlayerByEmailAsync(createPlayerDto.Email);
        if (existingPlayer != null)
        {
            throw new ValidationException($"Player with email {createPlayerDto.Email} already exists");
        }

        var player = new Player(createPlayerDto.Name, createPlayerDto.Email, createPlayerDto.PaymentPlan);
        await _playerRepository.AddAsync(player);

        _logger.LogInformation("Player created successfully. ID: {PlayerId}", player.Id);
        return _mapper.Map<PlayerDto>(player);
    }

    public async Task<PlayerDto> GetPlayerAsync(Guid id)
    {
        var player = await _playerRepository.GetPlayerWithRegistrationsAsync(id);
        if (player == null)
        {
            throw new NotFoundException(nameof(Player), id);
        }

        return _mapper.Map<PlayerDto>(player);
    }

    public async Task<IReadOnlyList<PlayerDto>> GetAllPlayersAsync()
    {
        var players = await _playerRepository.GetAllAsync();
        return _mapper.Map<IReadOnlyList<PlayerDto>>(players);
    }

    public async Task UpdatePlayerAsync(Guid id, UpdatePlayerDto updatePlayerDto)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        if (player == null)
        {
            throw new NotFoundException(nameof(Player), id);
        }

        // Check email uniqueness if email is being changed
        if (player.Email != updatePlayerDto.Email)
        {
            var existingPlayer = await _playerRepository.GetPlayerByEmailAsync(updatePlayerDto.Email);
            if (existingPlayer != null)
            {
                throw new ValidationException($"Player with email {updatePlayerDto.Email} already exists");
            }
        }

        _mapper.Map(updatePlayerDto, player);
        await _playerRepository.UpdateAsync(player);

        _logger.LogInformation("Player updated successfully. ID: {PlayerId}", player.Id);
    }

    public async Task DeletePlayerAsync(Guid id)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        if (player == null)
        {
            throw new NotFoundException(nameof(Player), id);
        }

        await _playerRepository.DeleteAsync(player);
        _logger.LogInformation("Player deleted successfully. ID: {PlayerId}", id);
    }
}
