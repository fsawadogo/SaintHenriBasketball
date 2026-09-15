using SaintHenriBasketball.Application.DTOs.Waitlist;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IWaitlistService
{
    Task<WaitlistDto> JoinWaitlistAsync(Guid userId, JoinWaitlistDto request);
    Task LeaveWaitlistAsync(Guid userId, Guid sessionId);
    Task<IReadOnlyList<WaitlistDto>> GetSessionWaitlistAsync(Guid sessionId);
    Task<WaitlistDto?> GetUserWaitlistEntryAsync(Guid userId, Guid sessionId);
    Task PromoteNextAsync(Guid sessionId);
    /// Offers a place to one specific waiting entry with the same status, expiry, notification and email as PromoteNextAsync.
    Task<WaitlistOfferOutcome> OfferEntryAsync(Guid entryId);
    Task ProcessOffersAsync();
}
