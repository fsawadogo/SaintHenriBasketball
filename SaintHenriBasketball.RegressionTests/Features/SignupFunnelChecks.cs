using SaintHenriBasketball.Application.DTOs.SignupFunnel;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the `signup-funnel` feature. Uses its own data; other checks share the database.
/// Every player here signs up inside one narrow window, and the report is asked for that window only,
/// so players created by other checks cannot change the counts.
internal static class SignupFunnelChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        SignupFunnelService ServiceFor(ApplicationDbContext context) => new(new SignupFunnelRepository(context));

        var tag = Guid.NewGuid().ToString("N")[..8];
        // A two-day window in 2002 that no other check writes to.
        var dayOne = new DateTime(2002, 5, 14, 12, 0, 0, DateTimeKind.Utc);
        var dayTwoLocal = SessionTimeHelper.ToLocal(dayOne).Date.AddMonths(1);
        var dayTwo = SessionTimeHelper.ToUtc(dayTwoLocal.AddHours(12));

        ApplicationUser Player(string name, DateTime createdOn, bool confirmed = true, bool deactivated = false)
        {
            var user = new ApplicationUser($"sf_{name}_{tag}", $"sf-{name}-{tag}@example.test", "test-only", "Funnel", name, PaymentPlan.DropIn)
            {
                EmailConfirmed = confirmed,
                IsDeactivated = deactivated,
            };
            typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.CreatedOn))!.SetValue(user, createdOn);
            return user;
        }

        // Signed up and stopped there.
        var stopped = Player("stopped", dayOne, confirmed: false);
        // Confirmed but never reserved.
        var confirmedOnly = Player("confirmed", dayOne);
        // Reserved but never paid or played.
        var reservedOnly = Player("reserved", dayOne);
        // Paid and played, a month later.
        var full = Player("full", dayTwo);
        // Signed up in the window then left the club.
        var gone = Player("gone", dayOne, deactivated: true);
        // An admin: never counted, because admins do not sign up.
        var admin = Player("admin", dayOne);
        admin.IsAdmin = true;
        // Outside the window.
        var before = Player("before", dayOne.AddMonths(-6));

        var session = new Session(SessionTimeHelper.ToLocal(dayTwo).Date.AddDays(10), 10, 10m, "10:00", "12:00", "Saint-Henri");
        var cancelledSession = new Session(SessionTimeHelper.ToLocal(dayOne).Date.AddDays(3), 10, 10m, "10:00", "12:00", "Saint-Henri") { Status = SessionStatus.Cancelled };

        await using (var context = db())
        {
            context.Users.AddRange(stopped, confirmedOnly, reservedOnly, full, gone, admin, before);
            context.Sessions.AddRange(session, cancelledSession);
            context.SessionRegistrations.AddRange(
                new SessionRegistration(reservedOnly.Id, session.Id, PaymentPlan.DropIn),
                new SessionRegistration(full.Id, session.Id, PaymentPlan.DropIn));
            context.SessionAttendances.AddRange(
                new SessionAttendance { SessionId = session.Id, UserId = full.Id, IsAttending = true },
                // Said yes to a session that was then cancelled: never played.
                new SessionAttendance { SessionId = cancelledSession.Id, UserId = reservedOnly.Id, IsAttending = true });
            context.Payments.AddRange(
                new Payment(full.Id, 10m, PaymentPlan.DropIn, session.Id) { Status = PaymentStatus.Completed, PaymentDate = dayTwo, CreatedAt = dayTwo, Reference = $"SF-{tag}-1" },
                // Pending is an intention, not a payment.
                new Payment(reservedOnly.Id, 10m, PaymentPlan.DropIn, session.Id) { Status = PaymentStatus.Pending, PaymentDate = dayTwo, CreatedAt = dayTwo, Reference = $"SF-{tag}-2" });
            await context.SaveChangesAsync();
        }

        var from = new DateTimeOffset(dayOne.AddDays(-1));
        var to = new DateTimeOffset(dayTwo.AddDays(1));
        SignupFunnelDto report;
        await using (var context = db())
            report = await ServiceFor(context).GetAsync(from, to);

        int Stage(string key) => report.Stages.Single(s => s.Key == key).Count;

        assert(Stage("registered") == 5, "signup funnel: counts players who signed up in the period, and never admins");
        assert(Stage("confirmed") == 4, "signup funnel: counts confirmed email addresses");
        assert(Stage("reserved") == 2 && Stage("paid") == 1 && Stage("played") == 1,
            "signup funnel: reserving, paying and playing are each counted once per player, and a pending payment does not count");

        var playedStage = report.Stages.Single(s => s.Key == "played");
        assert(Math.Abs(playedStage.ShareOfRegistered - 0.2) < 0.0001 && playedStage.DroppedSincePrevious == 0,
            "signup funnel: each stage reports its share of the players who registered");
        assert(report.Stages.Single(s => s.Key == "reserved").DroppedSincePrevious == 2,
            "signup funnel: the drop between two stages is how many stopped there");

        assert(report.Deactivated == 1, "signup funnel: players who have since left are reported apart");

        assert(report.ByMonth.Count == 2 && report.ByMonth[0].Registered == 4 && report.ByMonth[1].Registered == 1
            && report.ByMonth[1].Played == 1,
            "signup funnel: months are listed oldest first, by the month the player signed up");

        assert(report.MedianDaysToFirstPlay == 10, "signup funnel: reports the median wait from signing up to playing");

        string? refusal = null;
        try
        {
            await using var context = db();
            await ServiceFor(context).GetAsync(new DateTimeOffset(dayTwo), new DateTimeOffset(dayOne));
        }
        catch (ValidationException ex) { refusal = ex.Message; }
        assert(refusal != null, "signup funnel: a period that ends before it starts is refused");

        string? tooLong = null;
        try
        {
            await using var context = db();
            await ServiceFor(context).GetAsync(new DateTimeOffset(dayOne.AddYears(-10)), new DateTimeOffset(dayTwo));
        }
        catch (ValidationException ex) { tooLong = ex.Message; }
        assert(tooLong != null, "signup funnel: a period longer than three years is refused rather than scanned");
    }
}
