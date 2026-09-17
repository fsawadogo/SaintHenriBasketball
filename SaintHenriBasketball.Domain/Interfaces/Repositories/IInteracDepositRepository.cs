using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// A pending payment with the player it belongs to, for matching a deposit against.
public record PendingPaymentCandidate(Payment Payment, string PlayerName);

public interface IInteracDepositRepository
{
    Task<InteracDeposit?> GetAsync(Guid id);

    /// Null when this email has not been stored before.
    Task<InteracDeposit?> FindDuplicateAsync(string fingerprint, string? messageId);

    Task AddAsync(InteracDeposit deposit);

    Task SaveAsync();

    /// Deposits newest first, optionally only those still waiting for an admin.
    Task<IReadOnlyList<InteracDeposit>> ListAsync(bool unmatchedOnly, int limit);

    /// Pending payments created in the window, with the player's name, for matching.
    Task<IReadOnlyList<PendingPaymentCandidate>> GetPendingCandidatesAsync(DateTime createdAfterUtc);

    /// The player's name for one payment, for display.
    Task<string?> GetPlayerNameAsync(Guid paymentId);
}
