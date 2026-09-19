namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// One never-confirmed account, and whether anything in the club's records points at it.
public record PurgeableAccount(Guid Id, string Name, string? Email, DateTime CreatedOn, bool HasHistory);

public interface IUnconfirmedAccountRepository
{
    /// Accounts that never confirmed their address and were created before <paramref name="cutoff"/>.
    /// Admins are never included, whatever their confirmation state.
    Task<IReadOnlyList<PurgeableAccount>> GetPurgeableAsync(DateTime cutoff);

    /// Removes one account. Only ever called for an account reported as having no history.
    Task DeleteAsync(Guid userId);
}
