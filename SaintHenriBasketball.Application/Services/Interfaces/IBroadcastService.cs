using SaintHenriBasketball.Application.DTOs.Broadcast;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IBroadcastService
{
    Task<BroadcastAudiencePreviewDto> PreviewAudienceAsync(BroadcastAudience audience);
    /// Sends to the audience and records the sending admin in the audit log.
    Task<SendBroadcastResultDto> SendAsync(SendBroadcastRequestDto request, Guid? adminId, string adminName);
}
