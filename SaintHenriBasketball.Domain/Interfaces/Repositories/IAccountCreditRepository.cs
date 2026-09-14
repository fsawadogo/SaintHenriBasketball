using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IAccountCreditRepository
{
    Task<decimal> GetBalanceAsync(Guid userId);

    /// Ledger rows for the user, newest first.
    Task<IReadOnlyList<AccountCredit>> GetByUserAsync(Guid userId);

    /// Inserts the row unless a unique index says it already exists (same redemption, or the
    /// same payment + kind). Returns false for the duplicate instead of throwing.
    Task<bool> TryAddAsync(AccountCredit credit);
}
