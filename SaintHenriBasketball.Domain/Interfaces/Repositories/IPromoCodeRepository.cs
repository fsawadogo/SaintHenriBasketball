using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IPromoCodeRepository
{
    Task<IReadOnlyList<PromoCode>> GetAllAsync();
    Task<PromoCode?> GetByIdAsync(Guid id);
    Task<PromoCode?> GetByCodeAsync(string code);

    /// False when the code already exists (unique index), instead of throwing.
    Task<bool> TryAddAsync(PromoCode promoCode);
    Task UpdateAsync(PromoCode promoCode);
    Task DeleteAsync(Guid id);

    /// Payments keep a foreign key to the promo they used, so such codes cannot be deleted.
    Task<bool> IsUsedByPaymentsAsync(Guid id);
}
