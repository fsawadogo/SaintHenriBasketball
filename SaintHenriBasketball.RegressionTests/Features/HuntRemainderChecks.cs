using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Three things the bug hunt found that outlive a single page.
///
/// A token issued before an address was confirmed was a working session, because nothing after
/// sign-in ever looked at EmailConfirmed. And a deactivated player's calendar subscription kept
/// answering, because deactivation only clears the feed token when the account is also anonymised.
/// Data sits in 2019.
internal static class HuntRemainderChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];

        // --- An unconfirmed address cannot carry a session ---
        var unconfirmed = new AuthUserSnapshot(IsDeactivated: false, IsAdmin: false, StaffRole.None, EmailConfirmed: false);
        var confirmed = new AuthUserSnapshot(IsDeactivated: false, IsAdmin: false, StaffRole.None, EmailConfirmed: true);

        assert(TokenUserCheck.Evaluate(unconfirmed, tokenClaimsAdmin: false, tokenStaffRole: null) is not null,
            "unconfirmed signup: a token for an address that was never confirmed is refused on every request");
        assert(TokenUserCheck.Evaluate(confirmed, tokenClaimsAdmin: false, tokenStaffRole: null) is null,
            "unconfirmed signup: a confirmed player is unaffected");

        // Deactivation still outranks it, so the reason given is the accurate one.
        var both = new AuthUserSnapshot(IsDeactivated: true, IsAdmin: false, StaffRole.None, EmailConfirmed: false);
        assert(TokenUserCheck.Evaluate(both, false, null) == "Account deactivated",
            "unconfirmed signup: a deactivated account is still reported as deactivated, not as unconfirmed");

        // --- A deactivated player's calendar feed stops answering ---
        var quit = new ApplicationUser($"ics_gone_{tag}", $"ics-gone-{tag}@example.test", "test-only", "Gil", $"Gone{tag}", PaymentPlan.DropIn)
        { EmailConfirmed = true, IsDeactivated = true, CalendarFeedToken = $"tok-gone-{tag}" };
        var playing = new ApplicationUser($"ics_here_{tag}", $"ics-here-{tag}@example.test", "test-only", "Hana", $"Here{tag}", PaymentPlan.DropIn)
        { EmailConfirmed = true, CalendarFeedToken = $"tok-here-{tag}" };

        await using (var context = db())
        {
            context.Users.AddRange(quit, playing);
            context.Sessions.Add(new Session(DateTime.UtcNow.Date.AddDays(3), 20, 11m, "10:00", "12:00", "Saint-Henri"));
            await context.SaveChangesAsync();
        }

        await using (var context = db())
        {
            var users = new UserRepository(context, NullLogger<UserRepository>.Instance);
            var service = new CalendarSyncService(users,
                new SessionRegistrationRepository(context), NullLogger<CalendarSyncService>.Instance);

            var goneFeed = await service.BuildIcsForTokenAsync(quit.CalendarFeedToken!);
            assert(goneFeed is null,
                "calendar feed: a deactivated player's subscription stops answering, rather than serving the schedule forever");

            var liveFeed = await service.BuildIcsForTokenAsync(playing.CalendarFeedToken!);
            assert(liveFeed is not null && liveFeed.Contains("BEGIN:VCALENDAR", StringComparison.Ordinal),
                "calendar feed: a current player's subscription still works");

            assert(await service.BuildIcsForTokenAsync($"not-a-real-token-{tag}") is null,
                "calendar feed: an unknown token gets nothing");
        }
    }
}
