using SaintHenriBasketball.Application.DTOs.Credits;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class AccountCreditService : IAccountCreditService
{
    private readonly IAccountCreditRepository _repository;

    public AccountCreditService(IAccountCreditRepository repository)
    {
        _repository = repository;
    }

    public async Task<AccountCreditsDto> GetForUserAsync(Guid userId)
    {
        var entries = await _repository.GetByUserAsync(userId);
        return new AccountCreditsDto
        {
            // Summed from the same rows that are listed, so the two always agree.
            Balance = entries.Sum(e => e.Amount),
            Entries = entries.Select(e => new AccountCreditEntryDto
            {
                Id = e.Id,
                Amount = e.Amount,
                Kind = e.Kind,
                CreatedAt = e.CreatedAt,
                PaymentId = e.PaymentId,
            }).ToList(),
        };
    }
}
