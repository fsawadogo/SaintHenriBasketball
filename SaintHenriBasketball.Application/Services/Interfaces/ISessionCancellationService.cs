using SaintHenriBasketball.Application.DTOs.Session;

namespace SaintHenriBasketball.Application.Services.Interfaces;

/// Cancelling a session end to end: status, players' pending and paid drop-ins, and notifications.
public interface ISessionCancellationService
{
    Task<SessionCancellationPreviewDto> PreviewAsync(Guid sessionId);

    Task<SessionCancellationResultDto> CancelAsync(Guid sessionId, CancelSessionRequest request);
}
