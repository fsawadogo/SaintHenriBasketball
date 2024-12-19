using AutoMapper;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class RegistrationService : IRegistrationService
{
    private readonly ISessionRegistrationRepository _registrationRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<RegistrationService> _logger;

    public RegistrationService(
        ISessionRegistrationRepository registrationRepository,
        ISessionRepository sessionRepository,
        IPlayerRepository playerRepository,
        IMapper mapper,
        ILogger<RegistrationService> logger)
    {
        _registrationRepository = registrationRepository;
        _sessionRepository = sessionRepository;
        _playerRepository = playerRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SessionRegistrationDto> RegisterPlayerForSessionAsync(Guid playerId, Guid sessionId)
    {
        var session = await _sessionRepository.GetSessionWithRegistrationsAsync(sessionId);
        if (session == null)
        {
            throw new NotFoundException(nameof(TrainingSession), sessionId);
        }

        var player = await _playerRepository.GetByIdAsync(playerId);
        if (player == null)
        {
            throw new NotFoundException(nameof(Player), playerId);
        }

        // Check if player is already registered
        var existingRegistration = await _registrationRepository
            .GetRegistrationByPlayerAndSessionAsync(playerId, sessionId);
        if (existingRegistration != null)
        {
            throw new ValidationException("Player is already registered for this session");
        }

        if (!session.CanRegister())
        {
            throw new ValidationException("Cannot register for this session");
        }

        var registration = new SessionRegistration(playerId, sessionId);
        await _registrationRepository.AddAsync(registration);

        _logger.LogInformation("Player {PlayerId} registered for session {SessionId}", playerId, sessionId);

        return _mapper.Map<SessionRegistrationDto>(registration);
    }

    public async Task CancelRegistrationAsync(Guid playerId, Guid sessionId)
    {
        var registration = await _registrationRepository
            .GetRegistrationByPlayerAndSessionAsync(playerId, sessionId);
        if (registration == null)
        {
            throw new NotFoundException("Registration", $"Player: {playerId}, Session: {sessionId}");
        }

        registration.UpdateStatus(RegistrationStatus.Cancelled);
        await _registrationRepository.UpdateAsync(registration);

        _logger.LogInformation("Registration cancelled for player {PlayerId} in session {SessionId}",
            playerId, sessionId);
    }

    public async Task<IReadOnlyList<SessionRegistrationDto>> GetPlayerRegistrationsAsync(Guid playerId)
    {
        var registrations = await _registrationRepository.GetRegistrationsByPlayerAsync(playerId);
        return _mapper.Map<IReadOnlyList<SessionRegistrationDto>>(registrations);
    }
}
