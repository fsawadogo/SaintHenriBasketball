using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Clearing out a flood of signups that never confirmed an address.
///
/// A bot registered roughly a hundred accounts with scraped addresses; each one sent a confirmation
/// email from the club's domain to somebody who never asked for it. The accounts themselves are
/// inert, but they bury the roster — and a real person whose confirmation mail went astray looks
/// exactly the same, so anything with history is kept. Data sits in 2023.
internal static class RegistrationAbuseChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var old = DateTime.UtcNow.AddDays(-30);

        // CreatedOn is set by the entity itself, so the age these checks depend on is written
        // through EF rather than the constructor.
        var ages = new Dictionary<ApplicationUser, DateTime>();
        ApplicationUser Account(string who, bool confirmed, DateTime created)
        {
            var user = new ApplicationUser($"ab_{who}_{tag}", $"ab-{who}-{tag}@example.test", "test-only", who, $"Abuse{tag}", PaymentPlan.DropIn)
            { EmailConfirmed = confirmed };
            ages[user] = created;
            return user;
        }

        var litter = Account("litter", confirmed: false, created: old);
        var alsoLitter = Account("litter2", confirmed: false, created: old);
        var recent = Account("recent", confirmed: false, created: DateTime.UtcNow.AddHours(-3));
        var real = Account("real", confirmed: true, created: old);
        var stuckButPlaying = Account("stuck", confirmed: false, created: old);
        var holdsCredit = Account("credit", confirmed: false, created: old);
        var boss = Account("boss", confirmed: false, created: old);
        boss.IsAdmin = true;

        var session = new Session(new DateTime(2023, 4, 1), 20, 11m, "10:00", "12:00", "Saint-Henri");

        await using (var context = db())
        {
            context.Users.AddRange(litter, alsoLitter, recent, real, stuckButPlaying, holdsCredit, boss);
            context.Sessions.Add(session);
            // This one never confirmed either, but they booked a place: a person, not litter.
            context.SessionRegistrations.Add(new SessionRegistration(stuckButPlaying.Id, session.Id, PaymentPlan.DropIn));
            // And this one holds money. The model refuses to cascade a delete through the credit
            // ledger, so an account the purge claims it can take is one the database will not let go.
            context.AccountCredits.Add(new AccountCredit(holdsCredit.Id, 20m, AccountCreditKind.ManualAdjustment, note: "test-only"));
            foreach (var (user, created) in ages)
                context.Entry(user).Property(nameof(ApplicationUser.CreatedOn)).CurrentValue = created;
            await context.SaveChangesAsync();
        }

        UnconfirmedAccountPurgeService Service(ApplicationDbContext context) =>
            new(new UnconfirmedAccountRepository(context), NullLogger<UnconfirmedAccountPurgeService>.Instance);

        // --- The preview ---
        UnconfirmedPurgeResultDto preview;
        await using (var context = db()) preview = await Service(context).RunAsync(olderThanDays: 7, createdAfter: null, dryRun: true);

        assert(preview.Accounts.Any(a => a.Contains($"ab-litter-{tag}", StringComparison.Ordinal)),
            "registration abuse: a never-confirmed account with no history is listed for removal");
        assert(!preview.Accounts.Any(a => a.Contains($"ab-recent-{tag}", StringComparison.Ordinal)),
            "registration abuse: someone who signed up this morning is left alone — mail takes time to find");
        assert(!preview.Accounts.Any(a => a.Contains($"ab-real-{tag}", StringComparison.Ordinal)),
            "registration abuse: a confirmed player is never a candidate");
        assert(!preview.Accounts.Any(a => a.Contains($"ab-stuck-{tag}", StringComparison.Ordinal)),
            "registration abuse: an unconfirmed player who booked a session is kept — that is a person, not litter");
        assert(!preview.Accounts.Any(a => a.Contains($"ab-credit-{tag}", StringComparison.Ordinal)),
            "registration abuse: an unconfirmed account holding credit is kept — the ledger is a financial record, and the delete would fail on it anyway");
        assert(!preview.Accounts.Any(a => a.Contains($"ab-boss-{tag}", StringComparison.Ordinal)),
            "registration abuse: an admin is never swept up, whatever their confirmation state");

        await using (var context = db())
        {
            var stillThere = await context.Users.CountAsync(u => u.Id == litter.Id);
            assert(stillThere == 1, "registration abuse: the preview removes nothing");
        }

        // --- The purge ---
        UnconfirmedPurgeResultDto purged;
        await using (var context = db()) purged = await Service(context).RunAsync(olderThanDays: 7, createdAfter: null, dryRun: false);

        assert(purged.Deleted >= 2, "registration abuse: the litter is removed");
        assert(purged.KeptWithHistory >= 1, "registration abuse: and it reports what it kept, not only what it took");

        await using (var context = db())
        {
            assert(await context.Users.CountAsync(u => u.Id == litter.Id || u.Id == alsoLitter.Id) == 0,
                "registration abuse: the accounts are really gone afterwards");
            assert(await context.Users.CountAsync(u => u.Id == stuckButPlaying.Id) == 1,
                "registration abuse: the player who booked a session is still there");
            assert(await context.Users.CountAsync(u => u.Id == recent.Id) == 1,
                "registration abuse: so is this morning's signup");
            assert(await context.Users.CountAsync(u => u.Id == boss.Id) == 1,
                "registration abuse: so is the admin");
            // The real point of the check above: the purge ran to completion. Before the credit
            // ledger was counted as history, this account reached the delete and the foreign key
            // stopped it there, partway through the sweep.
            assert(await context.Users.CountAsync(u => u.Id == holdsCredit.Id) == 1,
                "registration abuse: the account holding credit survived the live purge");
        }

        // --- Clearing a burst without touching the people who came before it ---
        // Age alone takes the oldest first, which during a flood is precisely backwards: the litter
        // is new and the genuine half-finished signups are old.
        var longAgo = Account("longago", confirmed: false, created: DateTime.UtcNow.AddDays(-200));
        var inBurst = Account("burst", confirmed: false, created: DateTime.UtcNow.AddDays(-10));

        await using (var context = db())
        {
            context.Users.AddRange(longAgo, inBurst);
            foreach (var user in new[] { longAgo, inBurst })
                context.Entry(user).Property(nameof(ApplicationUser.CreatedOn)).CurrentValue = ages[user];
            await context.SaveChangesAsync();
        }

        await using (var context = db())
        {
            var windowed = await Service(context).RunAsync(
                olderThanDays: 7, createdAfter: DateTime.UtcNow.AddDays(-20), dryRun: true);

            assert(windowed.Accounts.Any(a => a.Contains($"ab-burst-{tag}", StringComparison.Ordinal)),
                "purge window: an account inside the window is listed");
            assert(!windowed.Accounts.Any(a => a.Contains($"ab-longago-{tag}", StringComparison.Ordinal)),
                "purge window: someone who signed up long before the window is spared — age alone would have taken them first");
        }

        await using (var context = db())
        {
            var unbounded = await Service(context).RunAsync(olderThanDays: 7, createdAfter: null, dryRun: true);
            assert(unbounded.Accounts.Any(a => a.Contains($"ab-longago-{tag}", StringComparison.Ordinal)),
                "purge window: with no window given the old account is a candidate again, so the bound is opt-in");
        }

        // A floor on the age, so nobody can sweep the last hour's signups by passing zero.
        await using (var context = db())
        {
            var aggressive = await Service(context).RunAsync(olderThanDays: 0, createdAfter: null, dryRun: true);
            assert(!aggressive.Accounts.Any(a => a.Contains($"ab-recent-{tag}", StringComparison.Ordinal)),
                "registration abuse: asking for everything still spares accounts younger than the floor");
        }
    }
}
