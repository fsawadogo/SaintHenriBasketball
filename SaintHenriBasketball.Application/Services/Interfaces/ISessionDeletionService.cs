using SaintHenriBasketball.Application.DTOs.Session;

namespace SaintHenriBasketball.Application.Services.Interfaces;

/// Permanently deletes sessions created by mistake. Sessions with payments must be cancelled instead.
public interface ISessionDeletionService
{
    Task<SessionDeletionPreviewDto> PreviewAsync(Guid sessionId);

    /// Returns what was removed. Throws ValidationException when the session has payments.
    Task<SessionDeletionPreviewDto> DeleteAsync(Guid sessionId);
}
