using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IPromoCodeRepository
{
    Task<IReadOnlyList<PromoCode>> GetAllAsync();
    Task<PromoCode?> GetByIdAsync(Guid id);
    Task<PromoCode?> GetByCodeAsync(string code);
    Task AddAsync(PromoCode promoCode);
    Task UpdateAsync(PromoCode promoCode);
    Task DeleteAsync(Guid id);
}
