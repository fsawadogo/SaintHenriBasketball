using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IReferralRepository
{
    Task<ReferralCode?> GetCodeByOwnerAsync(Guid ownerUserId);
    Task<ReferralCode?> GetCodeByValueAsync(string code);
    Task AddCodeAsync(ReferralCode code);
    Task UpdateCodeAsync(ReferralCode code);

    Task<bool> HasRefereeRedeemedAsync(Guid refereeUserId);
    Task AddRedemptionAsync(ReferralRedemption redemption);
    Task UpdateRedemptionAsync(ReferralRedemption redemption);
    Task<IReadOnlyList<ReferralRedemption>> GetRedemptionsAsync(int page = 1, int pageSize = 50);
    Task<ReferralRedemption?> GetRedemptionByIdAsync(Guid id);
}
