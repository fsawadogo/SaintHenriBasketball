using SaintHenriBasketball.Application.DTOs.WaitlistAdmin;

namespace SaintHenriBasketball.Application.Services.Interfaces;

/// Admin management of session waitlists (`waitlist-admin`).
public interface IWaitlistAdminService
{
    Task<WaitlistSessionDto> GetSessionWaitlistAsync(Guid sessionId);
    Task<WaitlistDemandDto> GetDemandAsync(int weeks = 8);
    Task<WaitlistOfferResultDto> SendOfferAsync(Guid entryId);
    Task RemoveEntryAsync(Guid entryId);
    Task<WaitlistSessionDto> MoveEntryAsync(Guid entryId, int position);
}
