using SaintHenriBasketball.Application.DTOs.Broadcast;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IBroadcastService
{
    Task<BroadcastAudiencePreviewDto> PreviewAudienceAsync(BroadcastAudience audience);
    Task<SendBroadcastResultDto> SendAsync(SendBroadcastRequestDto request);
}
