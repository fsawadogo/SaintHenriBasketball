using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.PublicSchedule;
using SaintHenriBasketball.Application.DTOs.SeasonSchedule;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Mapping;
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
            && cache.Removed.Contains("PublicSchedule:Upcoming*"), "wizard create: season, session and public schedule caches cleared");

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

        // ---- Montreal's date, not the server's ----
        assert(SessionTimeHelper.MontrealToday(new DateTime(2044, 3, 8, 2, 30, 0, DateTimeKind.Utc)) == new DateTime(2044, 3, 7),
            "montreal date: 2:30 UTC is still the previous day in Montreal");
        assert(SessionTimeHelper.MontrealToday(new DateTime(2044, 3, 8, 14, 0, 0, DateTimeKind.Utc)) == new DateTime(2044, 3, 8),
            "montreal date: the afternoon in UTC is the same day in Montreal");

        // ---- What the public page shows ----
        var publicDay = new DateTime(2044, 11, 5);
        var publicNowUtc = SessionTimeHelper.ToUtc(publicDay.AddHours(8));
        var openSession = new Session(publicDay, 20, 10m, "10:00", "12:00", $"Public open {tag}");
        var fullSession = new Session(publicDay, 20, 10m, "13:00", "15:00", $"Public full {tag}") { Status = SessionStatus.Full, RegisteredPlayersCount = 20 };
        var cancelledSession = new Session(publicDay, 20, 10m, "16:00", "18:00", $"Public cancelled {tag}") { Status = SessionStatus.Cancelled };
        var endedSession = new Session(publicDay, 20, 10m, "06:00", "07:00", $"Public ended {tag}");
        var pool = new[] { openSession, fullSession, cancelledSession, endedSession };

        var openOnly = PublicScheduleSelector.Select(pool, publicNowUtc, 12, includeFull: false);
        assert(openOnly.Count == 1 && openOnly[0].StartTime == "10:00", "public schedule: without the flag only open sessions that haven't ended are shown");

        var withFull = PublicScheduleSelector.Select(pool, publicNowUtc, 12, includeFull: true);
        assert(withFull.Count == 2 && withFull[1].IsFull && !withFull[0].IsFull, "public schedule: with the flag full sessions are listed and marked");
        assert(withFull.All(s => s.Location != null && s.Location.Contains(tag)) && withFull[0].SpotsRemaining == 20, "public schedule: the session details come through");
        assert(PublicScheduleSelector.Select(pool, publicNowUtc, 1, includeFull: true).Count == 1, "public schedule: the take limit is respected");

        // ---- Billing is due an hour after a session starts, any day of the week ----
        var billingDay = new DateTime(2044, 11, 8); // Tuesday
        var oneHourIn = SessionTimeHelper.ToUtc(billingDay.AddHours(20));
        assert(!DropInBillingSchedule.IsDue(billingDay, "19:00", SessionTimeHelper.ToUtc(billingDay.AddHours(19).AddMinutes(30))),
            "billing: not due half an hour into the session");
        assert(DropInBillingSchedule.IsDue(billingDay, "19:00", oneHourIn), "billing: due one hour after the start");
        assert(DropInBillingSchedule.IsDue(new DateTime(2044, 11, 5), "10:00", SessionTimeHelper.ToUtc(new DateTime(2044, 11, 5).AddHours(11))),
            "billing: a Saturday 10:00 session is still billed at 11:00");

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["JwtSettings:Key"] = "local-regression-signing-key-long-enough-for-any-hmac-algorithm-0123456789abcdef",
            ["AppUrl"] = "http://localhost", ["Referrals:RewardAmount"] = "10.00" }).Build();
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        FeatureFlagService Flags(ApplicationDbContext context) => new(new FeatureFlagRepository(context),
            new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()), NullLogger<MemoryCacheService>.Instance),
            new AuditLogRepository(context), NullLogger<FeatureFlagService>.Instance);
        PaymentService PaymentsFor(ApplicationDbContext context)
        {
            var users = new UserRepository(context, NullLogger<UserRepository>.Instance);
            var notifications = new NotificationService(new NotificationRepository(context), users, NullLogger<NotificationService>.Instance);
            var referrals = new ReferralService(new ReferralRepository(context), users, NullLogger<ReferralService>.Instance, new PaymentRepository(context),
                Flags(context), notifications, new AuditLogRepository(context), config);
            // No email service: the payment email fails and is logged, and the payment row is still created.
            return new PaymentService(new PaymentRepository(context), users, new SessionRepository(context), new SessionRegistrationRepository(context), mapper,
                NullLogger<PaymentService>.Instance, null!, notifications, new SeasonRepository(context, NullLogger<SeasonRepository>.Instance),
                new PromoCodeRepository(context), new AccountCreditRepository(context), referrals, Flags(context));
        }

        var eveningDate = SessionTimeHelper.MontrealToday();
        var evening = new Session(eveningDate, 20, 14m, "19:00", "21:00", $"Billing court {tag}");
        var eveningPlayer = new ApplicationUser($"wizardbill_{tag}", $"wizardbill_{tag}@example.test", "test-only", "Wizard", "Bill", PaymentPlan.DropIn) { EmailConfirmed = true };
        await using (var context = db())
        {
            context.Sessions.Add(evening);
            context.Users.Add(eveningPlayer);
            context.SessionRegistrations.Add(new SessionRegistration(eveningPlayer.Id, evening.Id, PaymentPlan.DropIn));
            await context.SaveChangesAsync();
        }

        int billed;
        await using (var context = db())
            billed = await PaymentsFor(context).RunDropInBillingAsync(SessionTimeHelper.ToUtc(eveningDate.AddHours(20)));
        assert(billed >= 1, "billing: a Tuesday evening session is billed an hour after it starts");

        await using (var context = db())
            assert(await context.Payments.AnyAsync(p => p.UserId == eveningPlayer.Id && p.SessionId == evening.Id && p.Amount == 14m),
                "billing: the payment uses the session's own drop-in price");

        int billedAgain;
        await using (var context = db())
            billedAgain = await PaymentsFor(context).RunDropInBillingAsync(SessionTimeHelper.ToUtc(eveningDate.AddHours(21)));
        assert(billedAgain == 0, "billing: running again bills nobody twice");

        int billedEarly;
        await using (var context = db())
            billedEarly = await PaymentsFor(context).RunDropInBillingAsync(SessionTimeHelper.ToUtc(eveningDate.AddHours(9)));
        assert(billedEarly == 0, "billing: nothing is billed before a session starts");

        // A session that filled up still has drop-in players to bill.
        var fullDate = SessionTimeHelper.MontrealToday();
        var fullBillingSession = new Session(fullDate, 1, 16m, "18:00", "20:00", $"Billing full court {tag}") { Status = SessionStatus.Full, RegisteredPlayersCount = 1 };
        var fullPlayer = new ApplicationUser($"wizardfull_{tag}", $"wizardfull_{tag}@example.test", "test-only", "Wizard", "Full", PaymentPlan.DropIn) { EmailConfirmed = true };
        await using (var context = db())
        {
            context.Sessions.Add(fullBillingSession);
            context.Users.Add(fullPlayer);
            context.SessionRegistrations.Add(new SessionRegistration(fullPlayer.Id, fullBillingSession.Id, PaymentPlan.DropIn));
            await context.SaveChangesAsync();
        }

        await using (var context = db())
            await PaymentsFor(context).RunDropInBillingAsync(SessionTimeHelper.ToUtc(fullDate.AddHours(19)));
        await using (var context = db())
            assert(await context.Payments.AnyAsync(p => p.UserId == fullPlayer.Id && p.SessionId == fullBillingSession.Id),
                "billing: a session that filled up is still billed");

        // ---- A refunded payment is not re-created by the next sweep ----
        // The session stays billable for up to 48 hours now, so the sweep revisits it after the refund.
        var refundDate = SessionTimeHelper.MontrealToday();
        var refundSession = new Session(refundDate, 20, 12m, "17:00", "19:00", $"Billing refund court {tag}");
        var refundPlayer = new ApplicationUser($"wizardrefund_{tag}", $"wizardrefund_{tag}@example.test", "test-only", "Wizard", "Refund", PaymentPlan.DropIn) { EmailConfirmed = true };
        await using (var context = db())
        {
            context.Sessions.Add(refundSession);
            context.Users.Add(refundPlayer);
            context.SessionRegistrations.Add(new SessionRegistration(refundPlayer.Id, refundSession.Id, PaymentPlan.DropIn));
            context.Payments.Add(new Payment(refundPlayer.Id, 12m, PaymentPlan.DropIn, refundSession.Id)
            {
                Status = PaymentStatus.Refunded,
                RefundedOn = DateTime.UtcNow,
                Reference = $"DROPIN-REFUNDED-{tag}",
                CreatedAt = DateTime.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        await using (var context = db())
            await PaymentsFor(context).RunDropInBillingAsync(SessionTimeHelper.ToUtc(refundDate.AddHours(18)));
        await using (var context = db())
            assert(await context.Payments.CountAsync(p => p.UserId == refundPlayer.Id && p.SessionId == refundSession.Id) == 1,
                "billing: a refunded drop-in payment is not billed again by the sweep");

        // ---- A start time the code can't read is never billed ----
        var badTimeDay = new DateTime(2044, 11, 8);
        assert(!DropInBillingSchedule.IsDue(badTimeDay, "", SessionTimeHelper.ToUtc(badTimeDay.AddHours(11)))
            && !DropInBillingSchedule.IsDue(badTimeDay, "", SessionTimeHelper.ToUtc(badTimeDay.AddHours(23)))
            && !DropInBillingSchedule.IsDue(badTimeDay, "", SessionTimeHelper.ToUtc(badTimeDay.AddDays(2))),
            "billing: a session with an empty start time is never due");
        assert(!DropInBillingSchedule.IsDue(badTimeDay, "7pm", SessionTimeHelper.ToUtc(badTimeDay.AddHours(23)))
            && !DropInBillingSchedule.IsDue(badTimeDay, "19h00", SessionTimeHelper.ToUtc(badTimeDay.AddDays(2)))
            && !DropInBillingSchedule.IsDue(badTimeDay, "   ", SessionTimeHelper.ToUtc(badTimeDay.AddDays(2))),
            "billing: a session with a malformed start time is never due");
        assert(DropInBillingSchedule.ParseStartTime("19:00") == TimeSpan.FromHours(19)
            && DropInBillingSchedule.ParseStartTime("9:30") == new TimeSpan(9, 30, 0)
            && DropInBillingSchedule.ParseStartTime("10:00:00") == TimeSpan.FromHours(10),
            "billing: the start-time formats sessions are actually stored in still parse");

        // ---- A session that has already ended is not the next one ----
        var endedRuleDay = new DateTime(2044, 11, 5);
        var oneOClock = SessionTimeHelper.ToUtc(endedRuleDay.AddHours(13));
        assert(SessionTimeHelper.HasEnded(endedRuleDay, "12:00", oneOClock), "ended session: a session that finished this morning has ended");
        assert(!SessionTimeHelper.HasEnded(endedRuleDay, "21:00", oneOClock), "ended session: tonight's session has not ended");
        assert(!SessionTimeHelper.HasEnded(endedRuleDay.AddDays(1), "10:00", oneOClock), "ended session: tomorrow's session has not ended");
        assert(SessionTimeHelper.HasEnded(endedRuleDay, "not a time", oneOClock)
            && !SessionTimeHelper.HasEnded(endedRuleDay, "not a time", SessionTimeHelper.ToUtc(endedRuleDay.AddHours(9))),
            "ended session: an unreadable end time falls back to noon, as the public schedule does");

        // Seeds a session ending at 00:01 today, so it has already ended at every instant of the day
        // except the one minute after midnight — the only moment this check would be ambiguous.
        var endedToday = new Session(SessionTimeHelper.MontrealToday(), 20, 10m, "00:00", "00:01", $"Ended today {tag}");
        await using (var context = db())
        {
            context.Sessions.Add(endedToday);
            await context.SaveChangesAsync();
        }
        await using (var context = db())
        {
            var next = await new SessionRepository(context).GetNextSessionAsync();
            assert(next is null || !SessionTimeHelper.HasEnded(next.SessionDate, next.EndTime, DateTime.UtcNow),
                "next session: the lookup never returns a session that has already ended");
            assert(next?.Id != endedToday.Id, "next session: a session that ended earlier today is skipped");
        }
    }
}
