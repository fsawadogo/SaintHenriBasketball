using SaintHenriBasketball.Application.DTOs.Session;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SessionDeletionService(ISessionRepository sessions, ICacheService cache) : ISessionDeletionService
{
    public const string HasPaymentsMessage =
        "This session has payment records, so it can't be deleted. Cancel it instead: players are told and their payments stay on record.";

    public async Task<SessionDeletionPreviewDto> PreviewAsync(Guid sessionId) =>
        ToDto(sessionId, await sessions.GetDeletionImpactAsync(sessionId) ?? throw new NotFoundException(nameof(Session), sessionId));

    public async Task<SessionDeletionPreviewDto> DeleteAsync(Guid sessionId)
    {
        var impact = await sessions.GetDeletionImpactAsync(sessionId) ?? throw new NotFoundException(nameof(Session), sessionId);
        if (impact.Payments > 0) throw new ValidationException(HasPaymentsMessage);

        // The repository re-checks for payments inside its transaction, in case one was created meanwhile.
        if (!await sessions.DeleteWithDependentsAsync(sessionId))
        {
            var current = await sessions.GetDeletionImpactAsync(sessionId);
            if (current == null) throw new NotFoundException(nameof(Session), sessionId);
            throw new ValidationException(HasPaymentsMessage);
        }

        await SessionCacheKeys.InvalidateAsync(cache, sessionId);
        await cache.RemoveAsync("AllSessions");
        return ToDto(sessionId, impact);
    }

    private static SessionDeletionPreviewDto ToDto(Guid sessionId, SessionDeletionImpact impact) => new()
    {
        SessionId = sessionId,
        Registrations = impact.Registrations,
        AttendanceAnswers = impact.AttendanceAnswers,
        Waitlisted = impact.Waitlisted,
        Feedback = impact.Feedback,
        Recaps = impact.Recaps,
        Payments = impact.Payments,
        CanDelete = impact.Payments == 0,
        BlockedReason = impact.Payments == 0 ? null : HasPaymentsMessage,
    };
}
