using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Season pass capacity, enforced where it cannot be walked around.
///
/// The cap used to live in one service method, so every other way of acquiring the season plan —
/// the plan-update endpoint, and registration, where PaymentPlan.Season is the zero default — got a
/// spot for free. Counting and claiming were also separate steps, so two players could both take
/// the last one. Data sits in 2018.
internal static class SeasonCapacityEnforcementChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        SeasonPlanChoiceRepository Choices(ApplicationDbContext c) =>
            new(c, NullLogger<SeasonPlanChoiceRepository>.Instance);

        // Spot counting includes every profile still set to the season plan, and this database is
        // shared with checks that create plenty. So the cap is set relative to what is already
        // there: room for exactly two more.
        var season = new Season(new DateTime(2018, 9, 1), new DateTime(2018, 12, 31), 90m)
        { Name = $"Cap {tag}", SeasonPassCapacity = 2 };

        ApplicationUser Player(string who) =>
            new($"cap_{who}_{tag}", $"cap-{who}-{tag}@example.test", "test-only", who, $"Cap{tag}", PaymentPlan.DropIn)
            { EmailConfirmed = true };

        var first = Player("first");
        var second = Player("second");
        var third = Player("third");

        await using (var context = db())
        {
            context.Seasons.Add(season);
            context.Users.AddRange(first, second, third);
            await context.SaveChangesAsync();
        }

        await using (var context = db())
        {
            var ambient = (await Choices(context).GetSpotHolderIdsAsync(season.Id, includeProfilePlan: true)).Count;
            var tracked = await context.Seasons.FindAsync(season.Id);
            tracked!.SeasonPassCapacity = ambient + 2;
            await context.SaveChangesAsync();
            season.SeasonPassCapacity = ambient + 2;
        }

        // --- The cap holds ---
        bool tookFirst, tookSecond, tookThird;
        await using (var context = db()) tookFirst = await Choices(context).TryTakeSeasonSpotAsync(season.Id, first.Id, season.SeasonPassCapacity);
        await using (var context = db()) tookSecond = await Choices(context).TryTakeSeasonSpotAsync(season.Id, second.Id, season.SeasonPassCapacity);
        await using (var context = db()) tookThird = await Choices(context).TryTakeSeasonSpotAsync(season.Id, third.Id, season.SeasonPassCapacity);

        assert(tookFirst && tookSecond, "season capacity: the spots a season has are given out");
        assert(!tookThird, "season capacity: the player after the last spot is refused");

        await using (var context = db())
        {
            var holders = await Choices(context).GetSpotHolderIdsAsync(season.Id, includeProfilePlan: true);
            assert(holders.Count(id => id == first.Id || id == second.Id || id == third.Id) == 2,
                "season capacity: exactly two of the three hold a spot afterwards");
        }

        // Taking a spot sets the profile plan too, inside the same lock — otherwise the count and
        // the profile disagree until some later write catches up.
        await using (var context = db())
        {
            var saved = await context.Users.AsNoTracking().FirstAsync(u => u.Id == first.Id);
            var refused = await context.Users.AsNoTracking().FirstAsync(u => u.Id == third.Id);
            assert(saved.PaymentPlan == PaymentPlan.Season, "season capacity: taking a spot records the plan on the player");
            assert(refused.PaymentPlan == PaymentPlan.DropIn, "season capacity: a refused player is left on drop-in, not half-moved");
        }

        // --- Holding a spot is idempotent ---
        bool again;
        await using (var context = db()) again = await Choices(context).TryTakeSeasonSpotAsync(season.Id, first.Id, season.SeasonPassCapacity);
        assert(again, "season capacity: a player who already holds a spot is never refused their own pass");

        await using (var context = db())
        {
            var holders = await Choices(context).GetSpotHolderIdsAsync(season.Id, includeProfilePlan: true);
            assert(holders.Count(id => id == first.Id) == 1,
                "season capacity: re-affirming does not consume a second spot");
        }

        // --- Two players going for the last spot at once ---
        var racy = new Season(new DateTime(2018, 9, 1), new DateTime(2018, 12, 31), 90m)
        { Name = $"Race {tag}", SeasonPassCapacity = 1 };
        var racerA = Player("racea");
        var racerB = Player("raceb");

        await using (var context = db())
        {
            context.Seasons.Add(racy);
            context.Users.AddRange(racerA, racerB);
            await context.SaveChangesAsync();
        }

        await using (var context = db())
        {
            // Room for exactly one more, so the two racers are genuinely competing for the last spot.
            var ambient = (await Choices(context).GetSpotHolderIdsAsync(racy.Id, includeProfilePlan: true)).Count;
            var tracked = await context.Seasons.FindAsync(racy.Id);
            tracked!.SeasonPassCapacity = ambient + 1;
            await context.SaveChangesAsync();
            racy.SeasonPassCapacity = ambient + 1;
        }

        var results = await Task.WhenAll(
            Take(db, racy.Id, racerA.Id, racy.SeasonPassCapacity),
            Take(db, racy.Id, racerB.Id, racy.SeasonPassCapacity));

        assert(results.Count(taken => taken) == 1,
            "season capacity: two players claiming the last spot at the same moment — exactly one gets it");

        await using (var context = db())
        {
            var holders = await Choices(context).GetSpotHolderIdsAsync(racy.Id, includeProfilePlan: true);
            assert(holders.Count(id => id == racerA.Id || id == racerB.Id) == 1,
                "season capacity: a one-spot season ends with one holder, never two");
        }

        static async Task<bool> Take(Func<ApplicationDbContext> db, Guid seasonId, Guid userId, int capacity)
        {
            await Task.Yield();
            try
            {
                await using var context = db();
                return await new SeasonPlanChoiceRepository(context, NullLogger<SeasonPlanChoiceRepository>.Instance)
                    .TryTakeSeasonSpotAsync(seasonId, userId, capacity);
            }
            catch (DbUpdateException)
            {
                // Losing a write race is a refusal, not a crash for the caller to puzzle over.
                return false;
            }
        }
    }
}
