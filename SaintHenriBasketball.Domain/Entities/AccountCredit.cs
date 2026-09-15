namespace SaintHenriBasketball.Domain.Entities;

public enum AccountCreditKind
{
    ReferralReward = 0,
    AppliedToPayment = 1,
    Released = 2,
    /// A completed payment refunded as credit instead of money.
    Refund = 3,
    /// Added or removed by an admin, with a note.
    ManualAdjustment = 4,
}

/// <summary>
/// One row of a player's dollar-credit ledger. Grants are positive, debits negative; the
/// balance is the sum of <see cref="Amount"/>. Rows are never updated: unique indexes on
/// <see cref="ReferralRedemptionId"/> and (<see cref="PaymentId"/>, <see cref="Kind"/>) make
/// every grant, debit, release and refund idempotent.
/// </summary>
public class AccountCredit
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public AccountCreditKind Kind { get; private set; }
    public Guid? ReferralRedemptionId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    /// Why an admin adjusted the balance or refunded a payment as credit.
    public string? Note { get; private set; }
    /// The admin who made a manual adjustment.
    public Guid? CreatedByUserId { get; private set; }

    private AccountCredit() { } // EF Core

    public AccountCredit(Guid userId, decimal amount, AccountCreditKind kind, Guid? referralRedemptionId = null, Guid? paymentId = null,
        string? note = null, Guid? createdByUserId = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Amount = amount;
        Kind = kind;
        ReferralRedemptionId = referralRedemptionId;
        PaymentId = paymentId;
        CreatedAt = DateTime.UtcNow;
        Note = note;
        CreatedByUserId = createdByUserId;
    }
}
