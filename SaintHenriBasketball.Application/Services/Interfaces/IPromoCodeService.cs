using SaintHenriBasketball.Application.DTOs.PromoCodes;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IPromoCodeService
{
    Task<IReadOnlyList<PromoCodeDto>> GetAllAsync();
    Task<PromoCodeDto> CreateAsync(UpsertPromoCodeDto body);
    Task<PromoCodeDto> UpdateAsync(Guid id, UpsertPromoCodeDto body);
    Task DeleteAsync(Guid id);
    Task<ValidatePromoCodeResultDto> ValidateAsync(ValidatePromoCodeDto body);
}
