using SaintHenriBasketball.Application.DTOs.Referrals;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IReferralService
{
    Task<ReferralCodeDto> GetOrCreateOwnCodeAsync(Guid userId, string shareBaseUrl);
    Task RedeemAsync(Guid refereeUserId, string code);
    Task<IReadOnlyList<ReferralRedemptionDto>> GetRedemptionsAsync(int page = 1, int pageSize = 50);
    Task UpdateRedemptionStatusAsync(Guid redemptionId, int newStatus);
}
