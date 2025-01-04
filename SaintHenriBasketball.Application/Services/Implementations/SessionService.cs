using AutoMapper;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SessionService : ISessionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionRegistrationRepository _registrationRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        ISessionRepository sessionRepository,
        ISessionRegistrationRepository registrationRepository,
        IMapper mapper,
        ILogger<SessionService> logger)
    {
        _sessionRepository = sessionRepository;
        _registrationRepository = registrationRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SessionDto> CreateSessionAsync(CreateSessionDto createSessionDto)
    {
        if (createSessionDto.SessionDate < DateTime.UtcNow)
        {
            throw new ValidationException("Session date cannot be in the past");
        }

        if (createSessionDto.MaxCapacity <= 0)
        {
            throw new ValidationException("Maximum capacity must be greater than zero");
        }

        if (createSessionDto.DropInPrice < 0)
        {
            throw new ValidationException("Drop-in price cannot be negative");
        }

        var session = new Session(
            createSessionDto.SessionDate,
            createSessionDto.MaxCapacity,
            createSessionDto.DropInPrice);

        await _sessionRepository.AddAsync(session);

        _logger.LogInformation("Basketball session created successfully. ID: {SessionId}", session.Id);

        return _mapper.Map<SessionDto>(session);
    }

    public async Task<SessionDetailDto> GetSessionAsync(Guid id)
    {
        var session = await _sessionRepository.GetByIdAsync(id);
        if (session == null)
        {
            throw new NotFoundException(nameof(Session), id);
        }

        var sessionDetail = _mapper.Map<SessionDetailDto>(session);

        return sessionDetail;
    }

    public async Task<IReadOnlyList<SessionDto>> GetUpcomingSessionsAsync()
    {
        var sessions = await _sessionRepository.GetUpcomingSessionsAsync();
        return _mapper.Map<IReadOnlyList<SessionDto>>(sessions)
            .OrderBy(s => s.SessionDate)
            .ToList();
    }

    public async Task<IReadOnlyList<SessionDto>> GetAvailableSessionsAsync()
    {
        var sessions = await _sessionRepository.GetAvailableSessionsAsync();
        return _mapper.Map<IReadOnlyList<SessionDto>>(sessions)
            .Where(s => s.RegisteredPlayersCount < s.MaxCapacity)
            .OrderBy(s => s.SessionDate)
            .ToList();
    }

    public async Task UpdateSessionAsync(Guid id, UpdateSessionDto updateSessionDto)
    {
        var session = await _sessionRepository.GetByIdAsync(id);
        if (session == null)
        {
            throw new NotFoundException(nameof(Session), id);
        }

        if (updateSessionDto.SessionDate < DateTime.UtcNow)
        {
            throw new ValidationException("Session date cannot be in the past");
        }

        if (updateSessionDto.MaxCapacity < session.RegisteredPlayersCount)
        {
            throw new ValidationException("New capacity cannot be less than current number of registered players");
        }

        _mapper.Map(updateSessionDto, session);

        // Check if session should be marked as full
        if (session.RegisteredPlayersCount >= session.MaxCapacity)
        {
            session.Status = SessionStatus.Full;
        }
        else if (session.Status == SessionStatus.Full)
        {
            session.Status = SessionStatus.Open;
        }

        await _sessionRepository.UpdateAsync(session);

        _logger.LogInformation("Basketball session updated successfully. ID: {SessionId}", session.Id);
    }

    public async Task CancelSessionAsync(Guid id)
    {
        var session = await _sessionRepository.GetByIdAsync(id);
        if (session == null)
        {
            throw new NotFoundException(nameof(Session), id);
        }

        if (session.Status == SessionStatus.Completed)
        {
            throw new ValidationException("Cannot cancel a completed session");
        }

        session.Status = SessionStatus.Cancelled;
        await _sessionRepository.UpdateAsync(session);

        _logger.LogInformation("Basketball session cancelled successfully. ID: {SessionId}", session.Id);
    }
}