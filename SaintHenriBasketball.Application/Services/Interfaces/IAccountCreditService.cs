using SaintHenriBasketball.Application.DTOs.Credits;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IAccountCreditService
{
    Task<AccountCreditsDto> GetForUserAsync(Guid userId);

    /// Adds (positive) or removes (negative) credit with a required note; never below a zero balance.
    Task<AccountCreditsDto> AdjustAsync(Guid userId, decimal amount, string? note, Guid? adminUserId);
}
