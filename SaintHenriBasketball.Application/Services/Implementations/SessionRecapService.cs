using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.SessionRecap;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SessionRecapService : ISessionRecapService
{
    private readonly ISessionRecapRepository _repository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<SessionRecapService> _logger;

    public SessionRecapService(
        ISessionRecapRepository repository,
        ISessionRepository sessionRepository,
        ILogger<SessionRecapService> logger)
    {
        _repository = repository;
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    public async Task<SessionRecapDto> CreateAsync(Guid sessionId, string photoUrl, string? caption, Guid adminId)
    {
        if (!IsValidHttpUrl(photoUrl))
            throw new ValidationException("PhotoUrl must be a valid http(s) URL");

        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new NotFoundException($"Session {sessionId} not found");

        var recap = new SessionRecap(session.Id, photoUrl.Trim(), caption?.Trim(), adminId);
        await _repository.AddAsync(recap);
        _logger.LogInformation("Session recap added by {AdminId} for session {SessionId}", adminId, sessionId);

        return ToDto(recap, session.SessionDate);
    }

    public async Task DeleteAsync(Guid recapId)
    {
        var recap = await _repository.GetByIdAsync(recapId)
            ?? throw new NotFoundException($"Recap {recapId} not found");
        await _repository.DeleteAsync(recap.Id);
    }

    public async Task<IReadOnlyList<SessionRecapDto>> GetBySessionAsync(Guid sessionId)
    {
        var items = await _repository.GetBySessionAsync(sessionId);
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        return items.Select(r => ToDto(r, session?.SessionDate)).ToList();
    }

    public async Task<IReadOnlyList<SessionRecapDto>> GetRecentAsync(int take = 6)
    {
        var recent = await _repository.GetRecentAsync(take);
        var sessionIds = recent.Select(r => r.SessionId).Distinct().ToList();
        var sessionDates = new Dictionary<Guid, DateTime>();
        foreach (var id in sessionIds)
        {
            var s = await _sessionRepository.GetByIdAsync(id);
            if (s is not null) sessionDates[id] = s.SessionDate;
        }
        return recent.Select(r => ToDto(r, sessionDates.TryGetValue(r.SessionId, out var d) ? d : null)).ToList();
    }

    private static SessionRecapDto ToDto(SessionRecap r, DateTime? sessionDate) => new()
    {
        Id = r.Id,
        SessionId = r.SessionId,
        PhotoUrl = r.PhotoUrl,
        Caption = r.Caption,
        CreatedOn = r.CreatedOn,
        SessionDate = sessionDate,
    };

    private static bool IsValidHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
