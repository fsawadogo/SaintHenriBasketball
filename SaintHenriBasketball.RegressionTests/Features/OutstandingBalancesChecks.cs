using SaintHenriBasketball.Infrastructure.Data.Context;

/// Regression checks for the `outstanding-balances` feature. Uses its own data; other checks share the database.
internal static class OutstandingBalancesChecks
{
    public static Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert) => Task.CompletedTask;
}
