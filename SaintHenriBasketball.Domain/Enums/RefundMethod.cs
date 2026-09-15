namespace SaintHenriBasketball.Domain.Enums;

/// How a completed payment's money went back to the player.
public enum RefundMethod
{
    /// Refunded to the card through Stripe.
    Card = 0,
    /// Added to the player's account credit.
    AccountCredit = 1,
    /// Sent back outside the app, e.g. an Interac e-Transfer from the club.
    Manual = 2,
}
