using SaintHenriBasketball.Application.DTOs.Credits;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IAccountCreditService
{
    Task<AccountCreditsDto> GetForUserAsync(Guid userId);
}
