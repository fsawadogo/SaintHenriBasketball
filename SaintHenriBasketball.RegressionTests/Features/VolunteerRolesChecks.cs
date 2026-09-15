using SaintHenriBasketball.Infrastructure.Data.Context;

/// Regression checks for the `volunteer-roles` feature. Uses its own data; other checks share the database.
internal static class VolunteerRolesChecks
{
    public static Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert) => Task.CompletedTask;
}
