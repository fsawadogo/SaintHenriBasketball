using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Helpers;

/// <summary>
/// Which payment status changes are allowed. A failed or refunded payment gives back the account
/// credit it used, so it must never return to Completed with the reduced amount (the credit could be
/// spent twice). Reopening a failed payment restores the full charge instead.
/// </summary>
public static class PaymentStatusRules
{
    public static bool CanTransition(PaymentStatus from, PaymentStatus to) => from == to || (from, to) switch
    {
        (PaymentStatus.Pending, PaymentStatus.Completed) => true,
        (PaymentStatus.Pending, PaymentStatus.Failed) => true,
        (PaymentStatus.Completed, PaymentStatus.Refunded) => true,
        (PaymentStatus.Failed, PaymentStatus.Pending) => true,
        _ => false,
    };

    public static string TransitionMessage(PaymentStatus from, PaymentStatus to) =>
        $"A {Label(from)} payment can't be marked {Label(to)}.";

    public const string AmountLockedMessage = "Only a pending payment's amount or plan can be changed.";
    public const string NegativeAmountMessage = "A payment amount can't be negative.";

    private static string Label(PaymentStatus status) => status.ToString().ToLowerInvariant();
}
