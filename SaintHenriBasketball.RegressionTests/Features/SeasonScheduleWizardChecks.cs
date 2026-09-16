using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.SeasonSchedule;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the `season-schedule-wizard` feature. Uses its own data dated 2043+, so other checks don't overlap.
internal static class SeasonScheduleWizardChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        static SeasonScheduleDayDto Day(int dayOfWeek, string start, string end, int capacity = 20, decimal price = 10m, string location = "717 Saint-Ferdinand") =>
            new() { DayOfWeek = dayOfWeek, StartTime = start, EndTime = end, MaxCapacity = capacity, DropInPrice = price, Location = location };

        static Exception? Failure(Action action)
        {
            try { action(); return null; }
            catch (Exception ex) { return ex; }
        }

        // ---- Time normalising ----
        assert(SeasonSchedulePlanner.NormalizeTime("9:30") == "09:30"
            && SeasonSchedulePlanner.NormalizeTime("09:30:00") == "09:30"
            && SeasonSchedulePlanner.NormalizeTime("23:59") == "23:59", "schedule: times normalise to HH:mm");
        assert(SeasonSchedulePlanner.NormalizeTime("24:00") is null
            && SeasonSchedulePlanner.NormalizeTime("10h") is null
            && SeasonSchedulePlanner.NormalizeTime("") is null, "schedule: impossible times are refused");

        // ---- Several days over a season ----
        var start = new DateTime(2043, 9, 1);  // Tuesday
        var end = new DateTime(2043, 9, 30);   // Wednesday
        var days = SeasonSchedulePlanner.ValidateDays(start, end, new List<SeasonScheduleDayDto> { Day(6, "10:00", "12:00"), Day(2, "19:00", "21:00") });
        var planned = SeasonSchedulePlanner.Plan(start, end, days);
        assert(planned.Count == 9, "schedule: 4 Saturdays and 5 Tuesdays in September 2043");
        var expected = new[]
        {
            (new DateTime(2043, 9, 1), "19:00"), (new DateTime(2043, 9, 5), "10:00"), (new DateTime(2043, 9, 8), "19:00"),
            (new DateTime(2043, 9, 12), "10:00"), (new DateTime(2043, 9, 15), "19:00"), (new DateTime(2043, 9, 19), "10:00"),
            (new DateTime(2043, 9, 22), "19:00"), (new DateTime(2043, 9, 26), "10:00"), (new DateTime(2043, 9, 29), "19:00"),
        };
        assert(planned.Select(p => (p.Date, p.StartTime)).SequenceEqual(expected),
            "schedule: the plan lists exactly the right dates and times, in order");

        // ---- The clock change doesn't shift dates ----
        var novemberDays = SeasonSchedulePlanner.ValidateDays(new DateTime(2043, 10, 25), new DateTime(2043, 11, 15), new List<SeasonScheduleDayDto> { Day(0, "10:00", "12:00") });
        var november = SeasonSchedulePlanner.Plan(new DateTime(2043, 10, 25), new DateTime(2043, 11, 15), novemberDays);
        assert(november.Select(p => p.Date).SequenceEqual(new[] { new DateTime(2043, 10, 25), new DateTime(2043, 11, 1), new DateTime(2043, 11, 8), new DateTime(2043, 11, 15) }),
            "schedule: weekly dates stay weekly across the November clock change");

        // ---- Two sessions on the same weekday at different times are allowed ----
        var twice = SeasonSchedulePlanner.ValidateDays(start, end, new List<SeasonScheduleDayDto> { Day(6, "10:00", "12:00"), Day(6, "13:00", "15:00") });
        assert(SeasonSchedulePlanner.Plan(start, end, twice).Count == 8, "schedule: the same weekday twice at different times is allowed");

        // ---- Refused input ----
        assert(Failure(() => SeasonSchedulePlanner.ValidateDays(end, start, new List<SeasonScheduleDayDto> { Day(6, "10:00", "12:00") })) is ValidationException,
            "schedule: the end date must be on or after the start date");
        assert(Failure(() => SeasonSchedulePlanner.ValidateDays(start, start.AddDays(400), new List<SeasonScheduleDayDto> { Day(6, "10:00", "12:00") })) is ValidationException,
            "schedule: a season lasts at most 366 days");
        assert(Failure(() => SeasonSchedulePlanner.ValidateDays(start, end, new List<SeasonScheduleDayDto> { Day(6, "12:00", "10:00") })) is ValidationException,
            "schedule: the end time must be after the start time");
        assert(Failure(() => SeasonSchedulePlanner.ValidateDays(start, end, new List<SeasonScheduleDayDto> { Day(6, "10:00", "12:00", capacity: 0) })) is ValidationException,
            "schedule: at least one spot per session");
        assert(Failure(() => SeasonSchedulePlanner.ValidateDays(start, end, new List<SeasonScheduleDayDto> { Day(6, "10:00", "12:00", price: -1m) })) is ValidationException,
            "schedule: the drop-in price can't be negative");
        assert(Failure(() => SeasonSchedulePlanner.ValidateDays(start, end, new List<SeasonScheduleDayDto> { Day(6, "10:00", "12:00", location: "  ") })) is ValidationException,
            "schedule: the gym address is required");
        assert(Failure(() => SeasonSchedulePlanner.ValidateDays(start, end, new List<SeasonScheduleDayDto> { Day(6, "10:00", "12:00"), Day(6, "10:00", "11:00") })) is ValidationException,
            "schedule: the same weekday and start time can't be listed twice");
        assert(Failure(() => SeasonSchedulePlanner.ValidateDays(start, end, new List<SeasonScheduleDayDto> { Day(7, "10:00", "12:00") })) is ValidationException,
            "schedule: the weekday must be 0 to 6");
        // A season is capped at 366 days, so one session day per weekday can never exceed 400 sessions on its own;
        // list every weekday twice (14 entries, at the session-day limit) to actually cross the 400-session cap.
        var everyDayTwice = Enumerable.Range(0, 7).SelectMany(d => new[] { Day(d, "06:00", "07:00"), Day(d, "10:00", "12:00") }).ToList();
        assert(Failure(() => SeasonSchedulePlanner.Plan(new DateTime(2043, 1, 1), new DateTime(2043, 12, 31),
            SeasonSchedulePlanner.ValidateDays(new DateTime(2043, 1, 1), new DateTime(2043, 12, 31), everyDayTwice))) is ValidationException,
            "schedule: a season can't hold more than 400 sessions");

        // ---- No session days at all is allowed ----
        assert(SeasonSchedulePlanner.Plan(start, end, SeasonSchedulePlanner.ValidateDays(start, end, new List<SeasonScheduleDayDto>())).Count == 0,
            "schedule: a season with no session days plans nothing");

        // ---- Season details ----
        assert(Failure(() => SeasonSchedulePlanner.ValidateSeason("  ", 100m, null)) is ValidationException, "schedule: the season needs a name");
        assert(Failure(() => SeasonSchedulePlanner.ValidateSeason("Fall 2043", -5m, null)) is ValidationException, "schedule: the season price can't be negative");
        assert(Failure(() => SeasonSchedulePlanner.ValidateSeason("Fall 2043", 100m, new string('x', 501))) is ValidationException, "schedule: notes are at most 500 characters");

        // ---- Duplicate key ----
        assert(SeasonSchedulePlanner.Key(new DateTime(2043, 9, 5, 13, 0, 0), "10:00:00") == SeasonSchedulePlanner.Key(new DateTime(2043, 9, 5), "10:00"),
            "schedule: a session is identified by its date and start time, however the time is written");

        // ---- Service: preview, create, duplicates, skips, all-or-nothing ----
        var tag = Guid.NewGuid().ToString("N")[..8];
        var cache = new RecordingCache();
        SeasonScheduleService Service(ApplicationDbContext context) => new(
            new SeasonRepository(context, NullLogger<SeasonRepository>.Instance),
            new SeasonScheduleRepository(context),
            new AuditLogService(new AuditLogRepository(context)),
            cache,
            NullLogger<SeasonScheduleService>.Instance);

        async Task<Exception?> FailureAsync(Func<SeasonScheduleService, Task> action)
        {
            try { await using var context = db(); await action(Service(context)); return null; }
            catch (Exception ex) { return ex; }
        }
        async Task CloseOpenSeasonsAsync()
        {
            await using var context = db();
            await context.Seasons.Where(s => s.Status == SeasonStatus.Open)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, SeasonStatus.Closed));
        }

        var seasonStart = new DateTime(2044, 9, 5);   // Monday
        var seasonEnd = new DateTime(2044, 10, 2);    // Sunday
        var wizardDays = new List<SeasonScheduleDayDto> { Day(6, "10:00", "12:00", 20, 10m, $"Wizard court {tag}"), Day(2, "19:00", "21:00", 12, 15m, $"Wizard gym {tag}") };

        // A session that already exists on one of the Saturdays, and a cancelled one that must not block its date.
        var existingSaturday = new DateTime(2044, 9, 10);
        var cancelledSaturday = new DateTime(2044, 9, 17);
        await using (var context = db())
        {
            context.Sessions.Add(new Session(existingSaturday, 20, 10m, "10:00:00", "12:00", $"Existing court {tag}"));
            context.Sessions.Add(new Session(cancelledSaturday, 20, 10m, "10:00", "12:00", $"Cancelled court {tag}") { Status = SessionStatus.Cancelled });
            await context.SaveChangesAsync();
        }

        await CloseOpenSeasonsAsync();

        SeasonSchedulePreviewDto preview;
        await using (var context = db())
            preview = await Service(context).PreviewAsync(new SeasonSchedulePreviewRequestDto { StartDate = seasonStart, EndDate = seasonEnd, Days = wizardDays });

        assert(preview.Sessions.Count == 8, "wizard preview: 4 Saturdays and 4 Tuesdays");
        assert(preview.Sessions.Count(s => s.AlreadyExists) == 1
            && preview.Sessions.Single(s => s.AlreadyExists).Date == existingSaturday, "wizard preview: an existing session at the same time is marked");
        assert(preview.Sessions.Single(s => s.Date == cancelledSaturday).AlreadyExists == false, "wizard preview: a cancelled session doesn't block its date");
        assert(preview.SessionsToCreate == 7 && preview.SessionsAlreadyExisting == 1, "wizard preview: the totals match");
        assert(preview.WillBeClosed == false && preview.OpenSeasonName is null, "wizard preview: with no season open the new one would be open");

        // ---- Create, skipping one Tuesday ----
        var skipTuesday = new DateTime(2044, 9, 20);
        SeasonScheduleCreateResultDto created;
        await using (var context = db())
            created = await Service(context).CreateAsync(new CreateSeasonWithScheduleDto
            {
                Name = $"Wizard season {tag}", StartDate = seasonStart, EndDate = seasonEnd, Price = 120m, Notes = "Bring both shirts",
                Days = wizardDays, Skip = new List<SeasonScheduleSkipDto> { new() { Date = skipTuesday, StartTime = "19:00" } },
            }, null, "Regression");

        assert(created.SessionsCreated == 6 && created.SessionsSkipped == 1 && created.SessionsAlreadyExisting == 1, "wizard create: created, skipped and existing counts");
        assert(created.Status == SeasonStatus.Open, "wizard create: the season opens when no other season is open");

        await using (var context = db())
        {
            var saved = await context.Sessions.AsNoTracking()
                // "Wizard " narrows to sessions the wizard itself created; the fixture's "Existing"/"Cancelled" court
                // sessions above also carry the tag (for their own uniqueness) but must not be counted here.
                .Where(s => s.SessionDate >= seasonStart && s.SessionDate <= seasonEnd && s.Location!.Contains("Wizard") && s.Location.Contains(tag)).ToListAsync();
            assert(saved.Count == 6, "wizard create: six sessions saved");
            assert(saved.Count(s => s.StartTime == "19:00" && s.MaxCapacity == 12 && s.DropInPrice == 15m) == 3, "wizard create: each day keeps its own time, spots and price");
            assert(saved.All(s => s.Status == SessionStatus.Open), "wizard create: sessions are open");
            assert(saved.All(s => s.SessionDate != skipTuesday), "wizard create: the unticked date is not created");
            assert(await context.AuditLogs.AnyAsync(a => a.EntityId == created.SeasonId && a.Action == SeasonScheduleService.CreatedAction), "wizard create: one activity log entry");
        }

        assert(cache.Removed.Contains("AllSeasons") && cache.Removed.Contains("CurrentSeason")
            && cache.Removed.Contains(SessionCacheKeys.UpcomingSessions) && cache.Removed.Contains(SessionCacheKeys.AvailableSessions)
            && cache.Removed.Contains("AllSessions") && cache.Removed.Contains("PublicSchedule:Upcoming*"), "wizard create: season, session and public schedule caches cleared");

        // ---- A second season while the first is open is created closed ----
        SeasonScheduleCreateResultDto second;
        await using (var context = db())
            second = await Service(context).CreateAsync(new CreateSeasonWithScheduleDto
            {
                Name = $"Wizard next {tag}", StartDate = new DateTime(2045, 1, 7), EndDate = new DateTime(2045, 1, 28), Price = 130m,
                Days = new List<SeasonScheduleDayDto> { Day(6, "10:00", "12:00", 20, 10m, $"Wizard court {tag}") },
            }, null, "Regression");
        assert(second.Status == SeasonStatus.Closed, "wizard create: a second season is created closed while another is open");

        await using (var context = db())
            assert((await Service(context).PreviewAsync(new SeasonSchedulePreviewRequestDto
            {
                StartDate = new DateTime(2045, 3, 4), EndDate = new DateTime(2045, 3, 25),
                Days = new List<SeasonScheduleDayDto> { Day(6, "10:00", "12:00", 20, 10m, $"Wizard court {tag}") },
            })).WillBeClosed, "wizard preview: warns that the season would be created closed");

        // ---- Nothing is saved when one session can't be saved ----
        var brokenName = $"Wizard broken {tag}";
        var brokenDate = new DateTime(2046, 5, 5);
        var brokenFailure = await FailureAsync(async _ =>
        {
            await using var context = db();
            var repository = new SeasonScheduleRepository(context);
            // Notes are limited to 500 characters in the database, so this insert fails.
            var season = new Season(new DateTime(2046, 5, 1), new DateTime(2046, 5, 31), 100m, new string('x', 600)) { Name = brokenName };
            await repository.AddSeasonWithSessionsAsync(season, new[] { new Session(brokenDate, 20, 10m, "10:00", "12:00", $"Broken court {tag}") });
        });
        assert(brokenFailure is not null, "wizard save: a failing insert throws");
        await using (var context = db())
        {
            assert(!await context.Seasons.AnyAsync(s => s.Name == brokenName), "wizard save: the season is not saved when a session fails");
            assert(!await context.Sessions.AnyAsync(s => s.SessionDate == brokenDate), "wizard save: no session is saved either");
        }

        // ---- Refused input reaches the service too ----
        assert(await FailureAsync(s => s.CreateAsync(new CreateSeasonWithScheduleDto
        {
            Name = "", StartDate = seasonStart, EndDate = seasonEnd, Price = 100m, Days = wizardDays,
        }, null, "Regression")) is ValidationException, "wizard create: a season without a name is refused");

        await CloseOpenSeasonsAsync();
    }
}
