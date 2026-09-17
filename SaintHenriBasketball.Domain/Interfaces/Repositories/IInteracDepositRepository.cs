using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// A pending payment with the player it belongs to, for matching a deposit against.
public record PendingPaymentCandidate(Payment Payment, string PlayerName);

public interface IInteracDepositRepository
{
    Task<InteracDeposit?> GetAsync(Guid id);

    /// Null when this email has not been stored before. The bank's own reference is the strongest
    /// key: it identifies one transfer, whatever the forwarding service does to the message.
    Task<InteracDeposit?> FindDuplicateAsync(string fingerprint, string? messageId, string? referenceNumber);

    /// Stores the deposit, or returns the one already stored when the database refuses it as a repeat.
    /// Storing first is what makes two simultaneous deliveries of the same email safe.
    Task<(bool Added, InteracDeposit? Existing)> TryAddAsync(InteracDeposit deposit);

    Task SaveAsync();

    /// Deposits newest first, optionally only those still waiting for an admin.
    Task<IReadOnlyList<InteracDeposit>> ListAsync(bool unmatchedOnly, int limit);

    /// Pending payments created in the window, with the player's name, for matching.
    Task<IReadOnlyList<PendingPaymentCandidate>> GetPendingCandidatesAsync(DateTime createdAfterUtc);

    /// The player's name for one payment, for display.
    Task<string?> GetPlayerNameAsync(Guid paymentId);
}
