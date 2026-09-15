using SaintHenriBasketball.Application.DTOs.SeasonSchedule;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Infrastructure.Data.Context;

/// Regression checks for the `season-schedule-wizard` feature. Uses its own data dated 2043+, so other checks don't overlap.
internal static class SeasonScheduleWizardChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        await Task.CompletedTask; // database checks arrive in task 2

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
        assert(planned.First().Date == new DateTime(2043, 9, 1) && planned.First().StartTime == "19:00", "schedule: the plan starts on the first matching day");
        assert(planned.All(p => p.DayOfWeek == (int)p.Date.DayOfWeek), "schedule: every session lands on its own weekday");
        assert(planned.Zip(planned.Skip(1)).All(pair => pair.First.Date <= pair.Second.Date), "schedule: the plan is in date order");

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
    }
}
