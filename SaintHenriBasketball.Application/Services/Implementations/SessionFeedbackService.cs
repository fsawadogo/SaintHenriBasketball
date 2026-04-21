using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.SessionFeedback;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SessionFeedbackService : ISessionFeedbackService
{
    private static readonly TimeSpan FeedbackWindow = TimeSpan.FromHours(48);

    private readonly ISessionFeedbackRepository _repository;
    private readonly ISessionAttendanceRepository _attendanceRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<SessionFeedbackService> _logger;

    public SessionFeedbackService(
        ISessionFeedbackRepository repository,
        ISessionAttendanceRepository attendanceRepository,
        ISessionRepository sessionRepository,
        ILogger<SessionFeedbackService> logger)
    {
        _repository = repository;
        _attendanceRepository = attendanceRepository;
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    public async Task SubmitAsync(Guid userId, Guid sessionId, int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
            throw new ValidationException("Rating must be between 1 and 5");

        var attendance = await _attendanceRepository.GetAttendanceAsync(sessionId, userId);
        if (attendance is null || !attendance.IsAttending)
            throw new ValidationException("Only players who attended the session can leave feedback");

        if (attendance.Session is null)
            throw new NotFoundException($"Session {sessionId} not found");

        var sessionEnd = attendance.Session.SessionDate.Date;
        if (DateTime.UtcNow - sessionEnd > FeedbackWindow + TimeSpan.FromDays(1))
            throw new ValidationException("Feedback window has closed for this session");

        if (await _repository.ExistsAsync(sessionId, userId))
            throw new ValidationException("You've already submitted feedback for this session");

        await _repository.AddAsync(new SessionFeedback(sessionId, userId, rating, comment?.Trim()));
        _logger.LogInformation(
            "Session feedback submitted by {UserId} for session {SessionId}: {Rating}", userId, sessionId, rating);
    }

    public async Task<PendingFeedbackDto?> GetPendingForUserAsync(Guid userId)
    {
        var history = await _attendanceRepository.GetUserAttendanceHistoryAsync(userId);
        var cutoff = DateTime.UtcNow - FeedbackWindow;

        var candidate = history
            .Where(a => a.IsAttending && a.Session is not null)
            .Where(a => a.Session!.SessionDate <= DateTime.UtcNow && a.Session.SessionDate >= cutoff.Date)
            .OrderByDescending(a => a.Session!.SessionDate)
            .FirstOrDefault();

        if (candidate is null) return null;

        if (await _repository.ExistsAsync(candidate.SessionId, userId))
            return null;

        return new PendingFeedbackDto
        {
            SessionId = candidate.SessionId,
            SessionDate = candidate.Session!.SessionDate,
            StartTime = candidate.Session.StartTime,
            Location = candidate.Session.Location,
        };
    }

    public async Task<SessionFeedbackSummaryDto> GetForSessionAsync(Guid sessionId)
    {
        var items = await _repository.GetBySessionAsync(sessionId);
        return new SessionFeedbackSummaryDto
        {
            Count = items.Count,
            AverageRating = items.Count == 0 ? 0 : items.Average(f => f.Rating),
            Items = items.Select(ToDto).ToList(),
        };
    }

    private static SessionFeedbackDto ToDto(SessionFeedback f) => new()
    {
        Id = f.Id,
        SessionId = f.SessionId,
        UserId = f.UserId,
        Rating = f.Rating,
        Comment = f.Comment,
        CreatedOn = f.CreatedOn,
    };
}
