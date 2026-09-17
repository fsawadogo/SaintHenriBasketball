using SaintHenriBasketball.Application.DTOs.InteracDeposits;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IInteracDepositService
{
    /// Stores one forwarded confirmation email and matches it when it can be matched safely.
    Task<IngestInteracEmailResultDto> IngestAsync(IngestInteracEmailDto email);

    /// Deposits newest first, each with the payment it suggests when nothing was matched outright.
    Task<IReadOnlyList<InteracDepositDto>> ListAsync(bool unmatchedOnly);

    /// An admin ties a deposit to a pending payment, which is then marked paid.
    Task<InteracDepositDto> MatchAsync(Guid depositId, Guid paymentId, Guid? adminId, string adminName);

    /// An admin sets a deposit aside: it is not a session payment.
    Task IgnoreAsync(Guid depositId, string? note, Guid? adminId, string adminName);
}
