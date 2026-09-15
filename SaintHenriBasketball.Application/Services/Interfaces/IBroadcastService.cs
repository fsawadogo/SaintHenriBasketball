using SaintHenriBasketball.Application.DTOs.Broadcast;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IBroadcastService
{
    Task<BroadcastAudiencePreviewDto> PreviewAudienceAsync(BroadcastAudience audience);
    /// Validates the broadcast, records it in the history and queues it for background delivery.
    Task<SendBroadcastResultDto> QueueAsync(SendBroadcastRequestDto request, Guid? adminId, string adminName);
    /// Sends a queued broadcast and records the outcome in the history and the audit log.
    Task<SendBroadcastResultDto> DeliverAsync(QueuedBroadcast broadcast);
    /// Records that delivery stopped before finishing.
    Task MarkFailedAsync(Guid broadcastId);
    Task<BroadcastHistoryPageDto> GetHistoryAsync(int page, int pageSize);
    /// Throws NotFoundException when there is no such broadcast.
    Task<BroadcastDetailDto> GetBroadcastAsync(Guid id);
}
