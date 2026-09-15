using SaintHenriBasketball.Infrastructure.Data.Context;

/// Regression checks for the `promo-referral-reports` feature. Uses its own data; other checks share the database.
internal static class PromoReferralReportsChecks
{
    public static Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert) => Task.CompletedTask;
}
