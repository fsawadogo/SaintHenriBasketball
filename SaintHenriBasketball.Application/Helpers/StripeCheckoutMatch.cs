namespace SaintHenriBasketball.Application.Helpers;

/// Rules shared by Stripe Checkout creation and the webhook that confirms it.
public static class StripeCheckoutMatch
{
    public static long ToCents(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    /// <summary>
    /// True when a paid Checkout Session belongs to this payment: same owner, CAD, exact amount,
    /// and the drop-in session or the season it was opened for (never both).
    /// </summary>
    public static bool Matches(IReadOnlyDictionary<string, string>? metadata, string? currency, long? amountTotal,
        Guid userId, Guid? sessionId, Guid? seasonId, decimal amount)
    {
        if (metadata is null || currency != "cad" || amountTotal != ToCents(amount)) return false;
        if (!metadata.TryGetValue("userId", out var owner) || owner != userId.ToString()) return false;
        if (sessionId is Guid session)
            return seasonId is null && !metadata.ContainsKey("seasonId")
                && metadata.TryGetValue("sessionId", out var sessionValue) && sessionValue == session.ToString();
        if (seasonId is Guid season)
            return !metadata.ContainsKey("sessionId")
                && metadata.TryGetValue("seasonId", out var seasonValue) && seasonValue == season.ToString();
        return false;
    }
}
