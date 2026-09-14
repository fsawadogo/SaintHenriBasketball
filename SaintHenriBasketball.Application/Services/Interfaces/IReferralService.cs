using SaintHenriBasketball.Application.DTOs.Referrals;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IReferralService
{
    Task<ReferralCodeDto> GetOrCreateOwnCodeAsync(Guid userId, string shareBaseUrl);
    Task RedeemAsync(Guid refereeUserId, string code);
    Task<IReadOnlyList<ReferralRedemptionDto>> GetRedemptionsAsync(int page = 1, int pageSize = 50);
    Task UpdateRedemptionStatusAsync(Guid redemptionId, int newStatus, Guid? adminId = null, string adminName = "Admin");

    /// <summary>
    /// Called after a payment becomes Completed. Grants the referrer's credit once when the referrals
    /// flag is on, the payment has a positive amount, the payer's redemption is still Pending and
    /// this is the payer's first paid payment. Never throws: the payment is already completed.
    /// </summary>
    Task GrantRewardForPaymentAsync(Guid payerUserId, Guid paymentId, decimal amount, DateTime completedAt);
}
