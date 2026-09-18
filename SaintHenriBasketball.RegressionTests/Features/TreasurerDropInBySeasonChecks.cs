using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.TreasurerReport;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Drop-in earnings attributed to a season.
///
/// A drop-in payment is created against a session and never carries a SeasonId, so scoping the
/// treasurer report to a season used to report no drop-in revenue at all. The season is worked out
/// from the session's date instead. All data sits in 2011, a year no other check writes to.
internal static class TreasurerDropInBySeasonChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        TreasurerReportService ServiceFor(ApplicationDbContext context) => new(
            new TreasurerReportRepository(context),
            new SeasonRepository(context, NullLogger<SeasonRepository>.Instance),
            new AuditLogService(new AuditLogRepository(context)));

        var tag = Guid.NewGuid().ToString("N")[..8];
        DateTime Utc(int month, int day, int hour = 15) => new(2011, month, day, hour, 0, 0, DateTimeKind.Utc);

        var season = new Season(new DateTime(2011, 9, 1), new DateTime(2011, 12, 31), 100m) { Name = $"Drop-in season {tag}" };

        // Two sessions inside the season, one outside it entirely.
        var first = new Session(new DateTime(2011, 9, 10), 20, 10m, "10:00", "12:00", "Saint-Henri");
        var second = new Session(new DateTime(2011, 10, 8), 20, 10m, "10:00", "12:00", "Saint-Henri");
        var quiet = new Session(new DateTime(2011, 11, 12), 20, 10m, "10:00", "12:00", "Saint-Henri");
        var outside = new Session(new DateTime(2011, 6, 4), 20, 10m, "10:00", "12:00", "Saint-Henri");

        var ana = new ApplicationUser($"dib_ana_{tag}", $"dib-ana-{tag}@example.test", "test-only", "Ana", $"Drop{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        var bo = new ApplicationUser($"dib_bo_{tag}", $"dib-bo-{tag}@example.test", "test-only", "Bo", $"Drop{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };

        Payment DropIn(ApplicationUser user, Session session, decimal amount, PaymentStatus status, DateTime paid, string suffix) =>
            new(user.Id, amount, PaymentPlan.DropIn, session.Id)
            {
                Status = status,
                PaymentDate = paid,
                CreatedAt = paid,
                Reference = $"DROPIN-DIB{tag}{suffix}",
            };

        var anaFirst = DropIn(ana, first, 10m, PaymentStatus.Completed, Utc(9, 10), "1");
        var boFirst = DropIn(bo, first, 10m, PaymentStatus.Completed, Utc(9, 10), "2");
        var anaSecond = DropIn(ana, second, 12m, PaymentStatus.Completed, Utc(10, 8), "3");

        // Refunded to a card: the money left, so it is not takings.
        var refundedOut = DropIn(bo, second, 10m, PaymentStatus.Refunded, Utc(10, 8), "4");
        refundedOut.RefundMethod = RefundMethod.Card;
        refundedOut.RefundedOn = Utc(10, 12);

        // Refunded as account credit: the club kept the cash, so it still counts.
        var refundedCredit = DropIn(ana, second, 8m, PaymentStatus.Refunded, Utc(10, 8), "5");
        refundedCredit.RefundMethod = RefundMethod.AccountCredit;
        refundedCredit.RefundedOn = Utc(10, 12);

        // Never paid, and a session outside the season — neither is this season's money.
        var pending = DropIn(bo, second, 10m, PaymentStatus.Pending, Utc(10, 8), "6");
        var offSeason = DropIn(ana, outside, 500m, PaymentStatus.Completed, Utc(6, 4), "7");

        await using (var context = db())
        {
            context.Seasons.Add(season);
            context.Sessions.AddRange(first, second, quiet, outside);
            context.Users.AddRange(ana, bo);
            context.Payments.AddRange(anaFirst, boFirst, anaSecond, refundedOut, refundedCredit, pending, offSeason);
            await context.SaveChangesAsync();
        }

        TreasurerReportDto report;
        await using (var context = db())
            report = await ServiceFor(context).GetReportAsync(new TreasurerReportQuery { SeasonId = season.Id });

        // 10 + 10 + 12 = 32 completed. The account-credit refund is money received and kept.
        assert(report.Overall.CollectedByPlan.DropIn.Amount == 32m && report.Overall.CollectedByPlan.DropIn.Count == 3,
            "drop-in by season: a season scope reports its drop-in takings, which used to come back as zero");

        assert(!report.BySeason.Any(s => s.SeasonId == null && s.Totals.CollectedByPlan.DropIn.Amount > 0),
            "drop-in by season: drop-ins are no longer dumped in the \"No season\" row");

        assert(report.Overall.CollectedByPlan.DropIn.Amount != 532m,
            "drop-in by season: a session outside the season's dates is not counted in it");

        // --- Per-session rows ---
        var rows = report.DropInBySession;
        assert(rows.Count == 3 && rows[0].SessionDate < rows[1].SessionDate && rows[1].SessionDate < rows[2].SessionDate,
            "drop-in by session: one row per session of the season, oldest first");

        var firstRow = rows.Single(r => r.SessionId == first.Id);
        assert(firstRow.Collected == 20m && firstRow.PlayersPaid == 2,
            "drop-in by session: a session reports what it took and how many players paid");

        var secondRow = rows.Single(r => r.SessionId == second.Id);
        assert(secondRow.Collected == 20m,
            "drop-in by session: a card refund is taken off, an account-credit refund is not — the club still has that money");
        // Ana is the only player whose money stayed: she paid 12, and her 8 came back as credit.
        // Bo's card refund left, and his other payment is still pending.
        assert(secondRow.PlayersPaid == 1,
            "drop-in by session: a player counts once however many times they paid");

        var quietRow = rows.Single(r => r.SessionId == quiet.Id);
        assert(quietRow.Collected == 0m && quietRow.PlayersPaid == 0,
            "drop-in by session: a session that took nothing is shown as zero, not left out");

        assert(!rows.Any(r => r.SessionId == outside.Id),
            "drop-in by session: a session outside the season is not listed");

        assert(rows.Sum(r => r.Collected) == 40m,
            "drop-in by session: the rows add up to the season's drop-in takings");

        // A date range has no sessions to break down.
        TreasurerReportDto ranged;
        await using (var context = db())
            ranged = await ServiceFor(context).GetReportAsync(new TreasurerReportQuery
            {
                From = new DateTime(2011, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                To = new DateTime(2011, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            });
        assert(ranged.DropInBySession.Count == 0,
            "drop-in by session: a date-range report carries no per-session rows, which would not add up to it");
        assert(ranged.Overall.CollectedByPlan.DropIn.Amount >= 32m,
            "drop-in by session: the same payments still count in a date-range report");
    }
}
