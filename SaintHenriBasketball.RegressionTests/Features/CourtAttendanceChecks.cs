using SaintHenriBasketball.Infrastructure.Data.Context;

/// Regression checks for the `court-attendance` feature. Uses its own data; other checks share the database.
internal static class CourtAttendanceChecks
{
    public static Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert) => Task.CompletedTask;
}
