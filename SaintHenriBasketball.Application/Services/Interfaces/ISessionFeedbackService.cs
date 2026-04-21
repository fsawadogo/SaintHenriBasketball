using SaintHenriBasketball.Application.DTOs.SessionFeedback;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISessionFeedbackService
{
    Task SubmitAsync(Guid userId, Guid sessionId, int rating, string? comment);
    Task<PendingFeedbackDto?> GetPendingForUserAsync(Guid userId);
    Task<SessionFeedbackSummaryDto> GetForSessionAsync(Guid sessionId);
}
