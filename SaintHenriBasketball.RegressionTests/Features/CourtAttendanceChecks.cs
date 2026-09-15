using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.CourtAttendance;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the `court-attendance` feature. Uses its own data; other checks share the database.
internal static class CourtAttendanceChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var cache = new KeyRecordingCache();
        CourtAttendanceService ServiceFor(ApplicationDbContext context) => new(
            new CourtAttendanceRepository(context), new ParticipationRepository(context), new SessionRepository(context),
            new PaymentRepository(context), new SeasonRepository(context, NullLogger<SeasonRepository>.Instance), cache);
        async Task<Exception?> TryAsync(Func<CourtAttendanceService, Task> action)
        {
            try { await using var context = db(); await action(ServiceFor(context)); return null; }
            catch (Exception ex) when (ex is ValidationException or NotFoundException) { return ex; }
        }
        ApplicationUser Player(string name, PaymentPlan plan = PaymentPlan.DropIn) =>
            new($"courtatt_{name}", $"courtatt_{name}@example.test", "test-only", "Court", name, plan) { EmailConfirmed = true };
        SessionAttendance Answer(Session s, ApplicationUser u, bool attending, AttendanceOutcome outcome = AttendanceOutcome.Unmarked, DateTime? checkIn = null) =>
            new() { Id = Guid.NewGuid(), SessionId = s.Id, UserId = u.Id, IsAttending = attending, Outcome = outcome, CheckInTime = checkIn, CreatedOn = DateTime.UtcNow, LastUpdated = DateTime.UtcNow };

        var today = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;

        // ---- Roster contents and payment status ----
        var court = new Session(today.AddDays(-2), 10, 12m, "10:00", "12:00", "Court attendance court");
        var later = new Session(today.AddDays(3), 10, 12m, "10:00", "12:00", "Court attendance future court");
        var cancelledCourt = new Session(today.AddDays(-1), 10, 12m, "10:00", "12:00", "Court attendance cancelled court") { Status = SessionStatus.Cancelled };
        var courtSeason = new Season(today.AddDays(-30), today.AddDays(30), 100m) { Name = "Court attendance season" };
        var dropPaid = Player("droppaid");
        var dropPending = Player("droppending");
        var dropSilent = Player("dropsilent");
        var seasonCovered = Player("seasoncovered", PaymentPlan.Season);
        var seasonUnpaid = Player("seasonunpaid", PaymentPlan.Season);
        var cancelledPlayer = Player("cancelled");
        var walkIn = Player("walkin");
        var deactivated = Player("deactivated");
        deactivated.IsDeactivated = true;
        var presetCheckIn = DateTime.UtcNow.AddDays(-2).AddHours(-1);
        await using (var context = db())
        {
            context.Sessions.AddRange(court, later, cancelledCourt);
            context.Seasons.Add(courtSeason);
            context.Users.AddRange(dropPaid, dropPending, dropSilent, seasonCovered, seasonUnpaid, cancelledPlayer, walkIn, deactivated);
            foreach (var registered in new[] { dropPaid, dropPending, dropSilent, seasonCovered, seasonUnpaid })
                context.SessionRegistrations.Add(new SessionRegistration(registered.Id, court.Id, registered.PaymentPlan));
            context.SessionRegistrations.Add(new SessionRegistration(dropPaid.Id, cancelledCourt.Id, PaymentPlan.DropIn));
            context.SessionAttendances.AddRange(
                Answer(court, dropPaid, true, checkIn: presetCheckIn),
                Answer(court, dropSilent, true),
                Answer(court, cancelledPlayer, false));
            context.Payments.AddRange(
                new Payment(dropPaid.Id, 12m, PaymentPlan.DropIn, court.Id) { Status = PaymentStatus.Completed, Reference = "COURTATT-PAID", CreatedAt = DateTime.UtcNow },
                new Payment(dropPending.Id, 12m, PaymentPlan.DropIn, court.Id) { Reference = "COURTATT-PENDING", CreatedAt = DateTime.UtcNow },
                new Payment(seasonCovered.Id, 100m, PaymentPlan.Season) { SeasonId = courtSeason.Id, Status = PaymentStatus.Completed, Reference = "COURTATT-SEASON", CreatedAt = DateTime.UtcNow },
                // A pending season fee is not a season pass.
                new Payment(seasonUnpaid.Id, 100m, PaymentPlan.Season) { SeasonId = courtSeason.Id, Reference = "COURTATT-SEASON-PENDING", CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();
        }

        SessionRosterDto roster;
        await using (var context = db()) roster = await ServiceFor(context).GetRosterAsync(court.Id);
        RosterPlayerDto Line(SessionRosterDto r, ApplicationUser u) => r.Players.Single(p => p.UserId == u.Id);
        assert(roster.Players.Count == 5 && roster.Players.All(p => p.UserId != cancelledPlayer.Id)
            && roster.Session.Counts is { Total: 5, Unmarked: 5, Attended: 0, NoShow: 0, WalkIn: 0 }
            && roster.Session.SessionDate == court.SessionDate.ToString("yyyy-MM-dd") && roster.Session.StartTime == "10:00" && roster.Session.MaxCapacity == 10
            && roster.Session.StartsAt.Kind == DateTimeKind.Utc,
            "court roster lists registered players only, with the session header and per-outcome counts");
        assert(Line(roster, dropPaid) is { PaymentStatus: RosterPaymentStatus.Completed, IsAttending: true, Outcome: "Unmarked" }
            && Line(roster, dropPaid).CheckInTime?.Kind == DateTimeKind.Utc && Line(roster, dropPaid).Email == dropPaid.Email
            && Line(roster, dropPending).PaymentStatus == RosterPaymentStatus.Pending && Line(roster, dropPending).IsAttending == null
            && Line(roster, dropSilent).PaymentStatus == RosterPaymentStatus.NotBilled
            && Line(roster, seasonCovered).PaymentStatus == RosterPaymentStatus.CoveredBySeasonPass
            && Line(roster, seasonUnpaid).PaymentStatus == RosterPaymentStatus.SeasonFeeUnpaid,
            "court roster shows each player's answer and drop-in or season payment status");
        assert(await TryAsync(s => s.GetRosterAsync(Guid.NewGuid())) is NotFoundException, "court roster for an unknown session is not found");

        // ---- Marking outcomes ----
        RosterPlayerDto marked;
        await using (var context = db()) marked = await ServiceFor(context).SetOutcomeAsync(court.Id, dropSilent.Id, "attended");
        await using (var context = db())
        {
            var row = await context.SessionAttendances.AsNoTracking().SingleAsync(a => a.SessionId == court.Id && a.UserId == dropSilent.Id);
            assert(marked is { Outcome: "Attended", IsAttending: true } && row.Outcome == AttendanceOutcome.Attended && row.IsAttending
                && row.CheckInTime is DateTime stamped && Math.Abs((stamped - DateTime.UtcNow).TotalMinutes) < 5 && marked.CheckInTime?.Kind == DateTimeKind.Utc
                && cache.Removed.Contains($"Attendance:Session:{court.Id}:Attendees") && cache.Removed.Contains($"Attendance:User:{dropSilent.Id}"),
                "marking attended records the outcome, stamps a missing check-in time and clears the session caches");
        }
        await using (var context = db()) await ServiceFor(context).SetOutcomeAsync(court.Id, dropPaid.Id, "Attended");
        await using (var context = db()) marked = await ServiceFor(context).SetOutcomeAsync(court.Id, seasonUnpaid.Id, "NoShow");
        await using (var context = db())
        {
            var kept = await context.SessionAttendances.AsNoTracking().SingleAsync(a => a.SessionId == court.Id && a.UserId == dropPaid.Id);
            var created = await context.SessionAttendances.AsNoTracking().SingleAsync(a => a.SessionId == court.Id && a.UserId == seasonUnpaid.Id);
            assert(kept.Outcome == AttendanceOutcome.Attended && kept.CheckInTime is DateTime keptTime && Math.Abs((keptTime - presetCheckIn).TotalSeconds) < 1,
                "marking attended keeps an existing check-in time");
            assert(created is { Outcome: AttendanceOutcome.NoShow, IsAttending: true, CheckInTime: null, UpdateReason: CourtAttendanceService.OutcomeReason }
                && marked.Outcome == "NoShow" && await context.SessionRegistrations.CountAsync(r => r.SessionId == court.Id) == 5,
                "marking a registered player who never answered creates their attendance row without changing registrations");
        }
        await using (var context = db()) await ServiceFor(context).SetOutcomeAsync(court.Id, dropPending.Id, "NoShow");
        await using (var context = db()) marked = await ServiceFor(context).SetOutcomeAsync(court.Id, dropPending.Id, " unmarked ");
        await using (var context = db())
            assert(marked.Outcome == "Unmarked" && (await context.SessionAttendances.AsNoTracking().SingleAsync(a => a.SessionId == court.Id && a.UserId == dropPending.Id)).Outcome == AttendanceOutcome.Unmarked,
                "an outcome can be set back to unmarked");
        var badOutcomes = new List<Exception?>();
        foreach (var bad in new[] { "WalkIn", "Maybe", "1", "", null })
            badOutcomes.Add(await TryAsync(s => s.SetOutcomeAsync(court.Id, dropPaid.Id, bad)));
        assert(badOutcomes.All(e => e is ValidationException), "only Attended, NoShow and Unmarked can be set as an outcome");
        assert(await TryAsync(s => s.SetOutcomeAsync(court.Id, cancelledPlayer.Id, "Attended")) is NotFoundException
            && await TryAsync(s => s.SetOutcomeAsync(Guid.NewGuid(), dropPaid.Id, "Attended")) is NotFoundException
            && await TryAsync(s => s.SetOutcomeAsync(cancelledCourt.Id, dropPaid.Id, "Attended")) is ValidationException,
            "outcomes are refused for players off the roster, unknown sessions and cancelled sessions");
        await using (var context = db()) { context.SessionRegistrations.Add(new SessionRegistration(dropPaid.Id, later.Id, PaymentPlan.DropIn)); await context.SaveChangesAsync(); }
        assert(await TryAsync(s => s.SetOutcomeAsync(later.Id, dropPaid.Id, "NoShow")) is ValidationException
            && !await AnyOutcomeAsync(db, later.Id), "outcomes can't be marked before the session opens for check-in");

        // ---- Walk-ins ----
        RosterPlayerDto added;
        await using (var context = db()) added = await ServiceFor(context).AddWalkInAsync(court.Id, walkIn.Id);
        await using (var context = db())
        {
            var registration = await context.SessionRegistrations.AsNoTracking().SingleOrDefaultAsync(r => r.SessionId == court.Id && r.UserId == walkIn.Id);
            var row = await context.SessionAttendances.AsNoTracking().SingleAsync(a => a.SessionId == court.Id && a.UserId == walkIn.Id);
            var session = await context.Sessions.AsNoTracking().SingleAsync(s => s.Id == court.Id);
            assert(added is { Outcome: "WalkIn", IsAttending: true, PaymentStatus: RosterPaymentStatus.NotBilled } && added.CheckInTime?.Kind == DateTimeKind.Utc
                && registration?.PaymentPlan == PaymentPlan.DropIn && row is { Outcome: AttendanceOutcome.WalkIn, IsAttending: true } && row.CheckInTime != null
                && session.RegisteredPlayersCount == 6,
                "a walk-in is registered on their plan, marked WalkIn with a check-in time and counted in the session");
        }
        await using (var context = db()) roster = await ServiceFor(context).GetRosterAsync(court.Id);
        assert(roster.Session.Counts is { Total: 6, Attended: 2, NoShow: 1, WalkIn: 1, Unmarked: 2 },
            "roster counts reflect marked outcomes and walk-ins");
        assert(await TryAsync(s => s.AddWalkInAsync(court.Id, deactivated.Id)) is ValidationException
            && await TryAsync(s => s.AddWalkInAsync(court.Id, dropPaid.Id)) is ValidationException
            && await TryAsync(s => s.AddWalkInAsync(court.Id, walkIn.Id)) is ValidationException
            && await TryAsync(s => s.AddWalkInAsync(court.Id, Guid.NewGuid())) is NotFoundException
            && await TryAsync(s => s.AddWalkInAsync(cancelledCourt.Id, walkIn.Id)) is ValidationException
            && await TryAsync(s => s.AddWalkInAsync(later.Id, walkIn.Id)) is ValidationException,
            "walk-ins are refused for deactivated players, players already on the roster, unknown players, cancelled sessions and sessions not yet open");
        await using (var context = db())
            assert(!await context.SessionRegistrations.AnyAsync(r => r.UserId == deactivated.Id) && !await context.SessionRegistrations.AnyAsync(r => r.SessionId == later.Id && r.UserId == walkIn.Id)
                && await context.SessionRegistrations.CountAsync(r => r.SessionId == court.Id && r.UserId == walkIn.Id) == 1,
                "refused walk-ins register nobody");
        await using (var context = db())
        {
            var tiny = new Session(today.AddDays(-2), 1, 12m, "10:00", "12:00", "Court attendance full court");
            context.Sessions.Add(tiny);
            context.SessionRegistrations.Add(new SessionRegistration(dropPaid.Id, tiny.Id, PaymentPlan.DropIn));
            await context.SaveChangesAsync();
            assert(await TryAsync(s => s.AddWalkInAsync(tiny.Id, walkIn.Id)) is ValidationException, "a walk-in can't overbook a full session");
        }
        assert(await TryAsync(s => s.SetOutcomeAsync(court.Id, walkIn.Id, "NoShow")) is ValidationException, "a walk-in's outcome can't be changed to another outcome");

        // ---- No-show stats over a window ----
        var flaky = Player("flaky");
        var steady = Player("steady");
        var s10 = new Session(today.AddDays(-10), 10, 12m, "10:00", "12:00", "Court attendance stats 10");
        var s20 = new Session(today.AddDays(-20), 10, 12m, "10:00", "12:00", "Court attendance stats 20");
        var s100 = new Session(today.AddDays(-100), 10, 12m, "10:00", "12:00", "Court attendance stats 100");
        var sCancelled = new Session(today.AddDays(-5), 10, 12m, "10:00", "12:00", "Court attendance stats cancelled") { Status = SessionStatus.Cancelled };
        var sFuture = new Session(today.AddDays(4), 10, 12m, "10:00", "12:00", "Court attendance stats future");
        await using (var context = db())
        {
            context.Users.AddRange(flaky, steady);
            context.Sessions.AddRange(s10, s20, s100, sCancelled, sFuture);
            foreach (var s in new[] { s10, s20, s100, sCancelled, sFuture })
                context.SessionRegistrations.Add(new SessionRegistration(flaky.Id, s.Id, PaymentPlan.DropIn));
            context.SessionRegistrations.AddRange(new SessionRegistration(steady.Id, s10.Id, PaymentPlan.DropIn), new SessionRegistration(steady.Id, s20.Id, PaymentPlan.DropIn));
            context.SessionAttendances.AddRange(
                Answer(s10, flaky, true, AttendanceOutcome.NoShow), Answer(s20, flaky, true, AttendanceOutcome.NoShow),
                Answer(s100, flaky, true, AttendanceOutcome.NoShow), Answer(sCancelled, flaky, true, AttendanceOutcome.NoShow),
                Answer(sFuture, flaky, true, AttendanceOutcome.NoShow), Answer(s10, steady, true, AttendanceOutcome.Attended));
            await context.SaveChangesAsync();
        }
        NoShowStatsPageDto flakyDefault, flakyWide, steadyDefault, everyone, firstPage, nobody;
        await using (var context = db())
        {
            var service = ServiceFor(context);
            flakyDefault = await service.GetNoShowStatsAsync(new NoShowStatsQuery { UserId = flaky.Id });
            flakyWide = await service.GetNoShowStatsAsync(new NoShowStatsQuery { UserId = flaky.Id, From = DateOnly.FromDateTime(today.AddDays(-120)), To = DateOnly.FromDateTime(today) });
            steadyDefault = await service.GetNoShowStatsAsync(new NoShowStatsQuery { UserId = steady.Id });
            everyone = await service.GetNoShowStatsAsync(new NoShowStatsQuery { PageSize = 500 });
            firstPage = await service.GetNoShowStatsAsync(new NoShowStatsQuery { PageSize = 1 });
            nobody = await service.GetNoShowStatsAsync(new NoShowStatsQuery { UserId = Guid.NewGuid() });
        }
        assert(flakyDefault.Total == 1 && flakyDefault.Items[0] is { SessionsRegistered: 2, NoShows: 2, Attended: 0, Unmarked: 0, NoShowRate: 100 }
            && flakyDefault.To == today.ToString("yyyy-MM-dd") && flakyDefault.From == today.AddDays(-90).ToString("yyyy-MM-dd"),
            "no-show stats default to the last 90 days of past sessions and skip cancelled and upcoming sessions");
        assert(flakyWide.Items.Single() is { SessionsRegistered: 3, NoShows: 3 }, "no-show stats use a custom window");
        assert(steadyDefault.Items.Single() is { SessionsRegistered: 2, Attended: 1, NoShows: 0, Unmarked: 1, NoShowRate: 0 } && nobody.Total == 0,
            "no-show stats count attended and unmarked sessions and rate no-shows over marked sessions only");
        var flakyIndex = everyone.Items.ToList().FindIndex(i => i.UserId == flaky.Id);
        var steadyIndex = everyone.Items.ToList().FindIndex(i => i.UserId == steady.Id);
        var walkInLine = everyone.Items.SingleOrDefault(i => i.UserId == walkIn.Id);
        assert(flakyIndex == 0 && steadyIndex > flakyIndex && everyone.Items.Zip(everyone.Items.Skip(1)).All(p => p.First.NoShows >= p.Second.NoShows)
            && walkInLine is { Attended: 1, SessionsRegistered: 1 } && everyone.Items.Any(i => i.UserId == seasonUnpaid.Id && i.NoShows == 1)
            && firstPage.Items.Count == 1 && firstPage.Total == everyone.Total && firstPage.Items[0].UserId == flaky.Id,
            "no-show stats sort by no-shows, count walk-ins as attended and page on request");
        assert(await TryAsync(s => s.GetNoShowStatsAsync(new NoShowStatsQuery { From = DateOnly.FromDateTime(today), To = DateOnly.FromDateTime(today.AddDays(-1)) })) is ValidationException,
            "no-show stats refuse a start date after the end date");
    }

    private static async Task<bool> AnyOutcomeAsync(Func<ApplicationDbContext> db, Guid sessionId)
    {
        await using var context = db();
        return await context.SessionAttendances.AnyAsync(a => a.SessionId == sessionId);
    }

    private sealed class KeyRecordingCache : ICacheService
    {
        public List<string> Removed { get; } = new();
        public Task<T?> GetAsync<T>(string key) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null) => Task.CompletedTask;
        public Task RemoveAsync(string key) { Removed.Add(key); return Task.CompletedTask; }
        public Task RemoveByPrefixAsync(string prefix) { Removed.Add(prefix + "*"); return Task.CompletedTask; }
    }
}
