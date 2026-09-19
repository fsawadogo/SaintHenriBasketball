namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// One never-confirmed account, and whether anything in the club's records points at it.
public record PurgeableAccount(Guid Id, string Name, string? Email, DateTime CreatedOn, bool HasHistory);

public interface IUnconfirmedAccountRepository
{
    /// <summary>
    /// Accounts that never confirmed their address, created before <paramref name="cutoff"/> and —
    /// when given — on or after <paramref name="createdAfter"/>. Admins are never included.
    ///
    /// The lower bound is what makes this usable during a flood: age alone marks the oldest
    /// accounts, which is the opposite of what a burst of new ones needs, and the people who
    /// signed up months ago and never finished are exactly who it would take first.
    /// </summary>
    Task<IReadOnlyList<PurgeableAccount>> GetPurgeableAsync(DateTime cutoff, DateTime? createdAfter = null);

    /// Removes one account. Only ever called for an account reported as having no history.
    Task DeleteAsync(Guid userId);
}
