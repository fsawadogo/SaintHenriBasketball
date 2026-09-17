namespace SaintHenriBasketball.Domain.Enums;

public enum InteracDepositStatus
{
    /// Received, and no payment claimed it yet.
    Unmatched,
    /// Tied to a payment, which was marked paid.
    Matched,
    /// An admin decided this deposit is not a session payment (a refund, a gift, a duplicate).
    Ignored
}
