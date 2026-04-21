using SaintHenriBasketball.Application.DTOs.QrCheckIn;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IQrCheckInService
{
    Task<SessionQrTokenDto> GenerateTokenAsync(Guid sessionId, string checkInBaseUrl);
    Task<QrCheckInResultDto> CheckInAsync(Guid userId, string token);
}
