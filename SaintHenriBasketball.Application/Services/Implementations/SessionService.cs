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
    private readonly IMapper _mapper;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        ISessionRepository sessionRepository,
        IMapper mapper,
        ILogger<SessionService> logger)
    {
        _sessionRepository = sessionRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SessionDto> CreateSessionAsync(CreateSessionDto createSessionDto)
    {
        if (createSessionDto.SessionDate < DateTime.UtcNow)
        {
            throw new ValidationException("Session date cannot be in the past");
        }

        var session = new TrainingSession(
            createSessionDto.SessionDate,
            createSessionDto.MaxCapacity,
            createSessionDto.DropInPrice);

        await _sessionRepository.AddAsync(session);

        _logger.LogInformation("Training session created successfully. ID: {SessionId}", session.Id);
        return _mapper.Map<SessionDto>(session);
    }

    public async Task<SessionDto> GetSessionAsync(Guid id)
    {
        var session = await _sessionRepository.GetSessionWithRegistrationsAsync(id);
        if (session == null)
        {
            throw new NotFoundException(nameof(TrainingSession), id);
        }

        return _mapper.Map<SessionDto>(session);
    }

    public async Task<IReadOnlyList<SessionDto>> GetUpcomingSessionsAsync()
    {
        var sessions = await _sessionRepository.GetUpcomingSessionsAsync();
        return _mapper.Map<IReadOnlyList<SessionDto>>(sessions);
    }

    public async Task UpdateSessionAsync(Guid id, UpdateSessionDto updateSessionDto)
    {
        var session = await _sessionRepository.GetSessionWithRegistrationsAsync(id);
        if (session == null)
        {
            throw new NotFoundException(nameof(TrainingSession), id);
        }

        if (updateSessionDto.SessionDate < DateTime.UtcNow)
        {
            throw new ValidationException("Session date cannot be in the past");
        }

        _mapper.Map(updateSessionDto, session);
        await _sessionRepository.UpdateAsync(session);

        _logger.LogInformation("Training session updated successfully. ID: {SessionId}", session.Id);
    }

    public async Task CancelSessionAsync(Guid id)
    {
        var session = await _sessionRepository.GetSessionWithRegistrationsAsync(id);
        if (session == null)
        {
            throw new NotFoundException(nameof(TrainingSession), id);
        }

        session.UpdateStatus(SessionStatus.Cancelled);
        await _sessionRepository.UpdateAsync(session);

        _logger.LogInformation("Training session cancelled successfully. ID: {SessionId}", session.Id);
    }
}
