namespace SaintHenriBasketball.Domain.Entities;

public enum AccountCreditKind
{
    ReferralReward = 0,
    AppliedToPayment = 1,
    Released = 2,
}

/// <summary>
/// One row of a player's dollar-credit ledger. Grants are positive, debits negative; the
/// balance is the sum of <see cref="Amount"/>. Rows are never updated: unique indexes on
/// <see cref="ReferralRedemptionId"/> and (<see cref="PaymentId"/>, <see cref="Kind"/>) make
/// every grant, debit and release idempotent.
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

    private AccountCredit() { } // EF Core

    public AccountCredit(Guid userId, decimal amount, AccountCreditKind kind, Guid? referralRedemptionId = null, Guid? paymentId = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Amount = amount;
        Kind = kind;
        ReferralRedemptionId = referralRedemptionId;
        PaymentId = paymentId;
        CreatedAt = DateTime.UtcNow;
    }
}
