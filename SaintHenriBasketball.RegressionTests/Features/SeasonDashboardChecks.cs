using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Application.DTOs.SeasonDashboard;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the `season-dashboard` feature. Uses its own data; other checks share the database.
/// The season sits in 2003, a year no other check writes to, so its totals are exact.
internal static class SeasonDashboardChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        SeasonDashboardService ServiceFor(ApplicationDbContext context) => new(new SeasonDashboardRepository(context));

        var tag = Guid.NewGuid().ToString("N")[..8];
        var today = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;

        // A season that ended in 2003, so every one of its sessions has already been played.
        var season = new Season(new DateTime(2003, 9, 6), new DateTime(2003, 12, 20), 90m)
        {
            Name = $"Automne 2003 {tag}",
            SeasonPassCapacity = 3,
        };

        var passHolder = new ApplicationUser($"sd_pass_{tag}", $"sd-pass-{tag}@example.test", "test-only", "Paule", "Passe", PaymentPlan.Season) { EmailConfirmed = true };
        var chooser = new ApplicationUser($"sd_choice_{tag}", $"sd-choice-{tag}@example.test", "test-only", "Chloé", "Choix", PaymentPlan.Season) { EmailConfirmed = true };
        var dropIn = new ApplicationUser($"sd_drop_{tag}", $"sd-drop-{tag}@example.test", "test-only", "Denis", "Séance", PaymentPlan.DropIn) { EmailConfirmed = true };

        var played = new Session(new DateTime(2003, 9, 13), 10, 10m, "10:00", "12:00", "Saint-Henri");
        var alsoPlayed = new Session(new DateTime(2003, 9, 20), 10, 10m, "10:00", "12:00", "Saint-Henri");
        var cancelled = new Session(new DateTime(2003, 9, 27), 10, 10m, "10:00", "12:00", "Saint-Henri") { Status = SessionStatus.Cancelled };
        // Outside the season's dates: its money and its places must not count.
        var outside = new Session(new DateTime(2003, 12, 27), 10, 10m, "10:00", "12:00", "Saint-Henri");

        Payment Pay(ApplicationUser user, decimal amount, PaymentPlan plan, PaymentStatus status, DateTime createdUtc, Guid? sessionId = null, Guid? seasonId = null) =>
            new(user.Id, amount, plan)
            {
                Status = status,
                PaymentDate = createdUtc,
                CreatedAt = createdUtc,
                SessionId = sessionId,
                SeasonId = seasonId,
                Reference = $"SD-{tag}-{Guid.NewGuid():N}"[..20],
            };

        // The pass sold, a pending pass (holds no spot), drop-ins collected and pending, and a refund.
        var passPaid = Pay(passHolder, 90m, PaymentPlan.Season, PaymentStatus.Completed, new DateTime(2003, 9, 1, 15, 0, 0, DateTimeKind.Utc), seasonId: season.Id);
        var passPending = Pay(chooser, 90m, PaymentPlan.Season, PaymentStatus.Pending, SessionTimeHelper.ToUtc(today.AddDays(-40)), seasonId: season.Id);
        var dropPaid = Pay(dropIn, 10m, PaymentPlan.DropIn, PaymentStatus.Completed, new DateTime(2003, 9, 13, 15, 0, 0, DateTimeKind.Utc), sessionId: played.Id);
        var dropPending = Pay(dropIn, 10m, PaymentPlan.DropIn, PaymentStatus.Pending, SessionTimeHelper.ToUtc(today.AddDays(-9)), sessionId: alsoPlayed.Id);
        var dropRefunded = Pay(dropIn, 10m, PaymentPlan.DropIn, PaymentStatus.Refunded, new DateTime(2003, 9, 20, 15, 0, 0, DateTimeKind.Utc), sessionId: alsoPlayed.Id);
        var outsideSeason = Pay(dropIn, 55m, PaymentPlan.DropIn, PaymentStatus.Completed, new DateTime(2003, 12, 27, 15, 0, 0, DateTimeKind.Utc), sessionId: outside.Id);

        await using (var context = db())
        {
            context.Seasons.Add(season);
            context.Users.AddRange(passHolder, chooser, dropIn);
            context.Sessions.AddRange(played, alsoPlayed, cancelled, outside);
            context.SeasonPlanChoices.AddRange(
                new SeasonPlanChoice(season.Id, passHolder.Id, PaymentPlan.Season),
                new SeasonPlanChoice(season.Id, chooser.Id, PaymentPlan.Season),
                new SeasonPlanChoice(season.Id, dropIn.Id, PaymentPlan.DropIn));
            context.SessionRegistrations.AddRange(
                new SessionRegistration(passHolder.Id, played.Id, PaymentPlan.Season),
                new SessionRegistration(dropIn.Id, played.Id, PaymentPlan.DropIn),
                new SessionRegistration(passHolder.Id, alsoPlayed.Id, PaymentPlan.Season));
            context.SessionAttendances.AddRange(
                new SessionAttendance { SessionId = played.Id, UserId = passHolder.Id, IsAttending = true },
                new SessionAttendance { SessionId = played.Id, UserId = dropIn.Id, IsAttending = true },
                // Said no: reserved but not attended.
                new SessionAttendance { SessionId = alsoPlayed.Id, UserId = passHolder.Id, IsAttending = false });
            context.Payments.AddRange(passPaid, passPending, dropPaid, dropPending, dropRefunded, outsideSeason);
            await context.SaveChangesAsync();
        }

        SeasonDashboardDto dashboard;
        await using (var context = db())
            dashboard = (await ServiceFor(context).GetAsync(season.Id))!;

        assert(dashboard.Passes.Capacity == 3 && dashboard.Passes.Sold == 1 && dashboard.Passes.Left == 2 && dashboard.Passes.Unpaid == 1,
            "season dashboard: a pass is held by a completed season payment, and an unpaid choice counts as unpaid, not sold");

        assert(dashboard.Money.Collected == 100m && dashboard.Money.CollectedFromPasses == 90m && dashboard.Money.CollectedFromDropIns == 10m
            && dashboard.Money.Refunded == 10m,
            "season dashboard: collected counts completed payments only, split by plan, and refunds are reported apart");

        assert(dashboard.Money.Pending == 100m && dashboard.Money.PendingCount == 2 && dashboard.Money.OldestPendingDays == 40,
            "season dashboard: pending money is counted with the age of the oldest pending payment");

        var bucket = (string label) => dashboard.Money.PendingByAge.Single(b => b.Label == label);
        assert(bucket("0-7 days").Count == 0 && bucket("8-30 days").Count == 1 && bucket("8-30 days").Amount == 10m
            && bucket("31-60 days").Count == 1 && bucket("31-60 days").Amount == 90m && bucket("60+ days").Count == 0,
            "season dashboard: pending money is bucketed by how many days it has been waiting");

        assert(dashboard.Sessions.Count == 3 && dashboard.Sessions.All(s => s.SessionId != outside.Id),
            "season dashboard: only sessions inside the season's dates are listed");

        var playedRow = dashboard.Sessions.Single(s => s.SessionId == played.Id);
        assert(playedRow.Reserved == 2 && playedRow.Attended == 2 && playedRow.DropInsCollected == 10m && playedRow.DropInsPending == 0
            && playedRow.HasHappened,
            "season dashboard: a session reports its own reservations, attendance and drop-in money");

        var secondRow = dashboard.Sessions.Single(s => s.SessionId == alsoPlayed.Id);
        assert(secondRow.Reserved == 1 && secondRow.Attended == 0 && secondRow.DropInsPending == 1 && secondRow.DropInsCollected == 0m,
            "season dashboard: a player who said no is reserved but not attended, and a refund is not collected money");

        // 2 attended out of 20 places on the two sessions that went ahead; the cancelled one offers none.
        assert(dashboard.Attendance.SessionsTotal == 3 && dashboard.Attendance.SessionsPlayed == 2
            && dashboard.Attendance.AttendedTotal == 2 && dashboard.Attendance.ReservedTotal == 3
            && Math.Abs(dashboard.Attendance.FillRate - 0.1) < 0.0001,
            "season dashboard: the fill rate counts attendance against the places offered by sessions that went ahead");

        assert(dashboard.Seasons.Any(s => s.SeasonId == season.Id) && dashboard.SeasonId == season.Id && dashboard.Price == 90m,
            "season dashboard: the answer names its season and lists the seasons an admin can switch to");

        string? refusal = null;
        try
        {
            await using var context = db();
            await ServiceFor(context).GetAsync(Guid.NewGuid());
        }
        catch (NotFoundException ex) { refusal = ex.Message; }
        assert(refusal != null, "season dashboard: an unknown season is refused with 404, not answered with empty figures");

        SeasonDashboardDto? fallback;
        await using (var context = db())
            fallback = await ServiceFor(context).GetAsync(null);
        assert(fallback != null, "season dashboard: with no season given, the current or next season answers");
    }
}
