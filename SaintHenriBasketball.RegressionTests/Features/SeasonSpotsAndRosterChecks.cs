using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Two counts that used to disagree with what an admin could see.
///
/// Season spots counted only completed payments, so a season with players on it read as untouched.
/// The session roster listed only players who had answered a reminder, so most of the people
/// actually coming were missing. Data sits in 2013, a year no other check writes to.
internal static class SeasonSpotsAndRosterChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];

        var season = new Season(new DateTime(2013, 9, 1), new DateTime(2013, 12, 31), 90m)
        { Name = $"Spots {tag}", SeasonPassCapacity = 15 };
        var session = new Session(new DateTime(2013, 9, 28), 20, 10m, "10:00", "12:00", "Saint-Henri");

        // Three ways to be a season player, and one player who is not one.
        var paid = new ApplicationUser($"sp_paid_{tag}", $"sp-paid-{tag}@example.test", "test-only", "Pia", $"Paid{tag}", PaymentPlan.Season) { EmailConfirmed = true };
        var chose = new ApplicationUser($"sp_chose_{tag}", $"sp-chose-{tag}@example.test", "test-only", "Cho", $"Chose{tag}", PaymentPlan.Season) { EmailConfirmed = true };
        var adminSet = new ApplicationUser($"sp_set_{tag}", $"sp-set-{tag}@example.test", "test-only", "Ada", $"Set{tag}", PaymentPlan.Season) { EmailConfirmed = true };
        var dropIn = new ApplicationUser($"sp_drop_{tag}", $"sp-drop-{tag}@example.test", "test-only", "Dee", $"Drop{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        var gone = new ApplicationUser($"sp_gone_{tag}", $"sp-gone-{tag}@example.test", "test-only", "Gus", $"Gone{tag}", PaymentPlan.Season) { EmailConfirmed = true, IsDeactivated = true };

        await using (var context = db())
        {
            context.Seasons.Add(season);
            context.Sessions.Add(session);
            context.Users.AddRange(paid, chose, adminSet, dropIn, gone);
            context.Payments.Add(new Payment(paid.Id, 90m, PaymentPlan.Season)
            {
                SeasonId = season.Id,
                Status = PaymentStatus.Completed,
                Reference = $"SEASON-{tag}",
                CreatedAt = DateTime.UtcNow,
            });
            context.SeasonPlanChoices.Add(new SeasonPlanChoice(season.Id, chose.Id, PaymentPlan.Season));
            context.SeasonPlanChoices.Add(new SeasonPlanChoice(season.Id, dropIn.Id, PaymentPlan.DropIn));

            // Two players hold a place; only one of them has answered a reminder.
            context.SessionRegistrations.Add(new SessionRegistration(paid.Id, session.Id, PaymentPlan.Season));
            context.SessionRegistrations.Add(new SessionRegistration(dropIn.Id, session.Id, PaymentPlan.DropIn));
            context.SessionAttendances.Add(new SessionAttendance { SessionId = session.Id, UserId = paid.Id, IsAttending = true });
            await context.SaveChangesAsync();
        }

        // --- Season spots ---
        await using (var context = db())
        {
            var choices = new SeasonPlanChoiceRepository(context, NullLogger<SeasonPlanChoiceRepository>.Instance);

            var paidOnly = await choices.CountPaidPassesAsync(season.Id);
            assert(paidOnly == 1, "season spots: exactly one of these players has actually paid");

            // Other checks share this database and create their own season-plan users, so assert on
            // who is in the set rather than how big it is.
            var holders = await choices.GetSpotHolderIdsAsync(season.Id, includeProfilePlan: true);
            assert(holders.Contains(paid.Id) && holders.Contains(chose.Id) && holders.Contains(adminSet.Id),
                "season spots: a paid pass, a season choice and an admin-set profile each hold a spot — this read as empty before");
            assert(!holders.Contains(dropIn.Id) && !holders.Contains(gone.Id),
                "season spots: a drop-in player and a deactivated one hold nothing");

            var withoutProfile = await choices.GetSpotHolderIdsAsync(season.Id, includeProfilePlan: false);
            assert(withoutProfile.Contains(paid.Id) && withoutProfile.Contains(chose.Id) && !withoutProfile.Contains(adminSet.Id),
                "season spots: a past season counts only what was paid or chosen for it, not today's profile plans");
        }

        // A player counted once, however many ways they qualify.
        await using (var context = db())
        {
            context.SeasonPlanChoices.Add(new SeasonPlanChoice(season.Id, paid.Id, PaymentPlan.Season));
            await context.SaveChangesAsync();
        }
        await using (var context = db())
        {
            var choices = new SeasonPlanChoiceRepository(context, NullLogger<SeasonPlanChoiceRepository>.Instance);
            var holders = await choices.GetSpotHolderIdsAsync(season.Id, includeProfilePlan: true);
            assert(holders.Count(id => id == paid.Id) == 1,
                "season spots: a player who paid and also chose is one spot, not two");
        }

        // Every surface that shows spots must agree. The public countdown had its own copy of the
        // count and still used paid passes alone, so it advertised a full season as untouched.
        await using (var context = db())
        {
            var choices = new SeasonPlanChoiceRepository(context, NullLogger<SeasonPlanChoiceRepository>.Instance);
            var holders = await choices.GetSpotHolderIdsAsync(season.Id, includeProfilePlan: true);
            var paidOnly = await choices.CountPaidPassesAsync(season.Id);
            assert(holders.Count > paidOnly,
                "season spots: the shared count is not the paid-pass count — any surface using the latter under-reports");
        }

        // --- Session roster ---
        await using (var context = db())
        {
            var roster = await new SessionRepository(context).GetRosterAsync(session.Id);

            assert(roster.Count == 2,
                "session roster: everyone who took a place is listed, not only those who answered a reminder");
            assert(roster.Any(r => r.UserId == paid.Id && r.Confirmed),
                "session roster: a player who confirmed is marked as confirmed");
            assert(roster.Any(r => r.UserId == dropIn.Id && !r.Confirmed),
                "session roster: a player who has not answered still appears, marked unconfirmed");
            assert(!roster.Any(r => r.UserId == gone.Id),
                "session roster: someone who never registered is not listed");
            assert(roster.All(r => !string.IsNullOrWhiteSpace(r.FirstName)),
                "session roster: every entry carries a name to show");
        }
    }
}
