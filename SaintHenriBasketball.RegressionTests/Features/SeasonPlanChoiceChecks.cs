using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for `season-plan-choice`. Uses its own data dated 2044+, so it cannot collide
/// with the other checks' fixtures.
internal static class SeasonPlanChoiceChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        // ---- SeasonForDate: the rule the billing job depends on ----
        var a = new Season(new DateTime(2044, 1, 10), new DateTime(2044, 4, 4), 100m) { Name = "A 2044" };
        var b = new Season(new DateTime(2044, 3, 1), new DateTime(2044, 6, 30), 110m) { Name = "B 2044" };
        var only = new List<Season> { a };
        var both = new List<Season> { a, b };

        assert(SeasonForDate.Resolve(only, new DateTime(2044, 2, 1))?.Name == "A 2044",
            "seasonForDate: one covering season resolves");
        assert(SeasonForDate.Resolve(only, new DateTime(2044, 1, 10))?.Name == "A 2044"
            && SeasonForDate.Resolve(only, new DateTime(2044, 4, 4))?.Name == "A 2044",
            "seasonForDate: both end dates are inclusive");
        assert(SeasonForDate.Resolve(only, new DateTime(2044, 8, 1)) is null,
            "seasonForDate: a date outside every season resolves to nothing");
        assert(SeasonForDate.Resolve(both, new DateTime(2044, 3, 15)) is null,
            "seasonForDate: overlapping seasons resolve to nothing rather than guessing");
        assert(SeasonForDate.Explain(both, new DateTime(2044, 3, 15)).Contains("2 seasons"),
            "seasonForDate: explains an overlap");
        assert(SeasonForDate.Explain(only, new DateTime(2044, 8, 1)).Contains("no season"),
            "seasonForDate: explains a gap");

        // ---- Capacity default ----
        await using (var context = db())
        {
            var season = new Season(new DateTime(2044, 9, 1), new DateTime(2044, 12, 1), 100m) { Name = "Capacity 2044" };
            context.Seasons.Add(season);
            await context.SaveChangesAsync();
            var saved = await context.Seasons.SingleAsync(s => s.Name == "Capacity 2044");
            assert(saved.SeasonPassCapacity == 15, "capacity: a new season defaults to 15 pass spots");
        }

        // ---- Choices, spots and the reset ----
        await using (var context = db())
        {
            var repo = new SeasonPlanChoiceRepository(context, NullLogger<SeasonPlanChoiceRepository>.Instance);

            var season = new Season(new DateTime(2044, 9, 2), new DateTime(2044, 12, 2), 100m)
            { Name = "Choices 2044", SeasonPassCapacity = 2 };
            context.Seasons.Add(season);

            ApplicationUser Player(string tag) =>
                new(tag, tag + "@example.test", "hash", tag, "Player", PaymentPlan.DropIn) { EmailConfirmed = true };

            var paid = Player("spc-paid");
            var chosenUnpaid = Player("spc-unpaid");
            var dropIn = Player("spc-dropin");
            context.Users.AddRange(paid, chosenUnpaid, dropIn);
            await context.SaveChangesAsync();

            // Everyone chooses; only one of them pays.
            await repo.UpsertAsync(season.Id, paid.Id, PaymentPlan.Season);
            await repo.UpsertAsync(season.Id, chosenUnpaid.Id, PaymentPlan.Season);
            await repo.UpsertAsync(season.Id, dropIn.Id, PaymentPlan.DropIn);

            assert((await repo.CountPaidPassesAsync(season.Id)) == 0,
                "spots: a choice with no payment holds no spot");

            // The 3-arg constructor: a season payment has no session, and the 4-arg overload takes a
            // non-nullable Guid sessionId.
            context.Payments.Add(new Payment(paid.Id, 100m, PaymentPlan.Season)
            { SeasonId = season.Id, Status = PaymentStatus.Completed, Reference = "SEASON-SPC-1", CreatedAt = DateTime.UtcNow });
            // A pending row for the same player must not count twice.
            context.Payments.Add(new Payment(paid.Id, 100m, PaymentPlan.Season)
            { SeasonId = season.Id, Status = PaymentStatus.Pending, Reference = "SEASON-SPC-1-PENDING", CreatedAt = DateTime.UtcNow });
            // Another player's pending payment holds nothing.
            context.Payments.Add(new Payment(chosenUnpaid.Id, 100m, PaymentPlan.Season)
            { SeasonId = season.Id, Status = PaymentStatus.Pending, Reference = "SEASON-SPC-2", CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();

            assert((await repo.CountPaidPassesAsync(season.Id)) == 1,
                "spots: counts distinct paid players, not payment rows");
            assert(await repo.HasPaidPassAsync(season.Id, paid.Id)
                && !await repo.HasPaidPassAsync(season.Id, chosenUnpaid.Id),
                "spots: a pending payment is not a held spot");

            // Upsert is idempotent and replaces rather than duplicating.
            await repo.UpsertAsync(season.Id, dropIn.Id, PaymentPlan.Season);
            await repo.UpsertAsync(season.Id, dropIn.Id, PaymentPlan.DropIn);
            assert((await context.SeasonPlanChoices.CountAsync(c => c.SeasonId == season.Id && c.UserId == dropIn.Id)) == 1,
                "choice: choosing twice replaces, never duplicates");

            var unpaidIds = await repo.GetUnpaidChoiceUserIdsAsync(season.Id);
            assert(unpaidIds.Count == 2 && !unpaidIds.Contains(paid.Id),
                "reset preview: names only the players without a paid pass");

            var cleared = await repo.ClearUnpaidForSeasonAsync(season.Id);
            assert(cleared == 2, "reset: clears exactly the unpaid choices");
            assert(await repo.GetAsync(season.Id, paid.Id) is not null,
                "reset: a paid player keeps their choice");
            assert(await repo.GetAsync(season.Id, chosenUnpaid.Id) is null,
                "reset: an unpaid choice is gone");
            assert((await repo.CountPaidPassesAsync(season.Id)) == 1,
                "reset: the paid pass still holds its spot afterwards");
        }
    }
}
