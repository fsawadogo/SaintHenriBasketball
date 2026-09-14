using SaintHenriBasketball.Application.DTOs.Broadcast;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IBroadcastService
{
    Task<BroadcastAudiencePreviewDto> PreviewAudienceAsync(BroadcastAudience audience);
    /// Validates the broadcast and queues it for background delivery.
    Task<SendBroadcastResultDto> QueueAsync(SendBroadcastRequestDto request, Guid? adminId, string adminName);
    /// Sends a queued broadcast and records the outcome and sending admin in the audit log.
    Task<SendBroadcastResultDto> DeliverAsync(QueuedBroadcast broadcast);
}
