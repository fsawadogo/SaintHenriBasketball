using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.OutstandingBalances;
using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.DTOs.Session;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the `outstanding-balances` feature. Uses its own data; other checks share the database.
internal static class OutstandingBalancesChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        ApplicationUser Player(string name, PaymentPlan plan = PaymentPlan.DropIn) =>
            new($"ob{name}{tag}", $"ob-{name}-{tag}@example.test", "test-only", "Owes", name, plan) { EmailConfirmed = true };

        var admin = Player("admin"); admin.IsAdmin = true;
        var seasonUnpaid = Player("seasonunpaid", PaymentPlan.Season);
        var seasonPending = Player("seasonpending", PaymentPlan.Season);
        var seasonPaid = Player("seasonpaid", PaymentPlan.Season);
        var seasonTransfer = Player("seasontransfer", PaymentPlan.Season);
        var dropIn = Player("dropin");
        var cancelledDropIn = Player("cancelled");
        var transfer = Player("transfer");
        var deactivated = Player("gone", PaymentPlan.Season); deactivated.IsDeactivated = true;
        var optedOut = Player("optedout"); optedOut.PaymentRemindersEnabled = false;
        var reminded = Player("reminded");
        var failing = Player("failing");
        var mine = new[] { seasonUnpaid, seasonPending, seasonPaid, seasonTransfer, dropIn, cancelledDropIn, transfer, deactivated, optedOut, reminded, failing }
            .Select(u => u.Id).ToHashSet();

        var todayLocal = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;
        // Closed so it never becomes another check's "current" open season; it still bills because it runs today.
        var season = new Season(todayLocal.AddDays(-10), todayLocal.AddDays(60), 137m) { Name = $"Balances {tag}", Status = SeasonStatus.Closed };
        var pastSession = new Session(todayLocal.AddDays(-2), 20, 15m, "10:00", "12:00", "Balances court");
        var cancelledSession = new Session(todayLocal.AddDays(-3), 20, 15m, "10:00", "12:00", "Balances court") { Status = SessionStatus.Cancelled };

        Payment DropInFor(ApplicationUser u, Session s, string? reference = null) =>
            new(u.Id, 15m, PaymentPlan.DropIn, s.Id) { Reference = reference ?? $"DROPIN-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        var seasonPendingPayment = new Payment(seasonPending.Id, 120m, PaymentPlan.Season) { SeasonId = season.Id, Reference = $"SEASON-{tag}", CreatedAt = DateTime.UtcNow };
        var seasonTransferPayment = new Payment(seasonTransfer.Id, 137m, PaymentPlan.Season) { SeasonId = season.Id, Reference = $"SEASON-{tag}|INTERAC:OB{tag}", CreatedAt = DateTime.UtcNow };
        var dropInPayment = DropInFor(dropIn, pastSession);
        var transferPayment = DropInFor(transfer, pastSession, $"DROPIN-{tag}|INTERAC:BANK{tag}");
        var optedOutPayment = DropInFor(optedOut, pastSession);
        var remindedRecent = DropInFor(reminded, pastSession);
        var remindedOld = DropInFor(reminded, cancelledSession);
        remindedOld.SessionId = null; // An older drop-in without a session still counts, dated by its payment.
        remindedOld.PaymentDate = DateTime.UtcNow.AddDays(-9);
        var failingPayment = DropInFor(failing, pastSession);
        var recentSentAt = DateTime.UtcNow.AddDays(-2);

        await using (var ctx = db())
        {
            ctx.Users.AddRange(admin, seasonUnpaid, seasonPending, seasonPaid, seasonTransfer, dropIn, cancelledDropIn, transfer, deactivated, optedOut, reminded, failing);
            ctx.Seasons.Add(season);
            ctx.Sessions.AddRange(pastSession, cancelledSession);
            ctx.SeasonRegistrations.AddRange(new[] { seasonUnpaid, seasonPending, seasonPaid, seasonTransfer, deactivated }.Select(u => new SeasonRegistration(season.Id, u.Id)));
            ctx.Payments.AddRange(seasonPendingPayment, seasonTransferPayment, dropInPayment, transferPayment, optedOutPayment, remindedRecent, remindedOld, failingPayment,
                new Payment(seasonPaid.Id, 137m, PaymentPlan.Season) { SeasonId = season.Id, Status = PaymentStatus.Completed, CreatedAt = DateTime.UtcNow },
                DropInFor(cancelledDropIn, cancelledSession),
                DropInFor(deactivated, pastSession));
            ctx.Set<ReminderLog>().AddRange(
                new ReminderLog { UserId = reminded.Id, PaymentId = remindedRecent.Id, Kind = ReminderKinds.DropIn, Status = ReminderStatuses.Sent, SentAt = recentSentAt },
                new ReminderLog { UserId = reminded.Id, PaymentId = remindedOld.Id, Kind = ReminderKinds.DropIn, Status = ReminderStatuses.Sent, SentAt = DateTime.UtcNow.AddDays(-5) });
            await ctx.SaveChangesAsync();
        }

        var email = new RecordingEmailService { FailFor = failing.Email };
        OutstandingBalancesService Service(ApplicationDbContext ctx) => new(
            new OutstandingBalancesRepository(ctx), new PaymentRepository(ctx), email,
            new AuditLogService(new AuditLogRepository(ctx)), NullLogger<OutstandingBalancesService>.Instance);

        await using (var ctx = db())
        {
            var all = await Service(ctx).GetBalancesAsync(new OutstandingBalancesQuery());
            var rows = all.Items.Where(r => mine.Contains(r.UserId)).ToList();
            OutstandingBalanceRowDto? Row(ApplicationUser u, string kind) => rows.SingleOrDefault(r => r.UserId == u.Id && r.Kind == kind);

            var unpaid = Row(seasonUnpaid, ReminderKinds.SeasonFee);
            var pending = Row(seasonPending, ReminderKinds.SeasonFee);
            assert(unpaid?.Amount == 137m && unpaid.SeasonId == season.Id && unpaid.PaymentId == null && unpaid.DaysOutstanding == 0
                && unpaid.Since.Kind == DateTimeKind.Utc && Math.Abs((unpaid.Since - DateTime.UtcNow).TotalMinutes) < 5
                && unpaid.Key == $"season:{season.Id}:{seasonUnpaid.Id}",
                "balances: a player who registers mid-season owes the season price from their registration");
            assert(pending?.Amount == 120m && pending.PaymentId == seasonPendingPayment.Id,
                "balances: a pending season payment is owed at its adjusted amount");
            assert(!rows.Any(r => r.UserId == seasonPaid.Id), "balances: a completed season payment clears the season fee");
            assert(Row(seasonTransfer, ReminderKinds.SeasonFee) == null && Row(seasonTransfer, ReminderKinds.Transfer)?.PaymentId == seasonTransferPayment.Id,
                "balances: a submitted season transfer is listed as awaiting review, not as an unpaid fee");

            var drop = Row(dropIn, ReminderKinds.DropIn);
            var expectedStart = SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(pastSession.SessionDate, pastSession.StartTime));
            assert(drop?.Amount == 15m && drop.SessionId == pastSession.Id && drop.PaymentId == dropInPayment.Id && drop.Since == expectedStart
                && drop.SessionDate?.Kind == DateTimeKind.Utc && drop.DaysOutstanding == 2 && drop.LastReminderSentAt == null && drop.CanRemind,
                "balances: an unpaid drop-in is owed from its session start, in UTC");
            var bank = Row(transfer, ReminderKinds.Transfer);
            assert(bank?.PaymentId == transferPayment.Id && bank.InteracReference == $"BANK{tag}" && Row(transfer, ReminderKinds.DropIn) == null,
                "balances: a pending Interac submission is a transfer awaiting review");
            assert(!rows.Any(r => r.UserId == cancelledDropIn.Id), "balances: a drop-in for a cancelled session is not owed");
            assert(!rows.Any(r => r.UserId == deactivated.Id), "balances: deactivated players are left out");
            assert(Row(optedOut, ReminderKinds.DropIn)?.CanRemind == false, "balances: a player who turned off payment reminders is flagged");
            var recentRow = rows.Single(r => r.PaymentId == remindedRecent.Id);
            assert(recentRow.LastReminderSentAt is DateTime last && last.Kind == DateTimeKind.Utc && Math.Abs((last - recentSentAt).TotalSeconds) < 1,
                "balances: each row shows when its debt was last reminded");

            var kinds = new[] { all.Totals.SeasonFee, all.Totals.DropIn, all.Totals.Transfer };
            assert(all.Totals.All.Count == all.Items.Count && all.Totals.All.Count == kinds.Sum(k => k.Count)
                && all.Totals.All.Amount == all.Items.Sum(r => r.Amount) && all.Totals.Transfer.Count == all.Items.Count(r => r.Kind == ReminderKinds.Transfer),
                "balances: totals add up per kind");
            var ages = all.Items.Select(r => r.Since).ToList();
            assert(ages.SequenceEqual(ages.OrderBy(s => s)), "balances: sorted oldest first by default");

            var dropIns = await Service(ctx).GetBalancesAsync(new OutstandingBalancesQuery { Kind = "dropin", Sort = "amount", Direction = "asc" });
            assert(dropIns.Items.All(r => r.Kind == ReminderKinds.DropIn) && dropIns.Items.Any(r => r.UserId == dropIn.Id)
                && dropIns.Totals.All.Count == all.Totals.All.Count
                && dropIns.Items.Select(r => r.Amount).SequenceEqual(dropIns.Items.Select(r => r.Amount).OrderBy(a => a)),
                "balances: kind filter and amount sort, with totals still covering every kind");
            try { await Service(ctx).GetBalancesAsync(new OutstandingBalancesQuery { Kind = "loans" }); assert(false, "balances: unknown kind rejected"); }
            catch (SaintHenriBasketball.Application.Exceptions.ValidationException) { assert(true, "balances: unknown kind is a validation error"); }
            try { await Service(ctx).SendRemindersAsync(new SendRemindersRequestDto(), admin.Id, "Admin"); assert(false, "reminders: empty selection rejected"); }
            catch (SaintHenriBasketball.Application.Exceptions.ValidationException) { assert(true, "reminders: an empty selection is a validation error"); }
        }

        var keys = new[]
        {
            $"season:{season.Id}:{seasonUnpaid.Id}", $"dropin:{dropInPayment.Id}", $"transfer:{transferPayment.Id}",
            $"dropin:{optedOutPayment.Id}", $"dropin:{remindedRecent.Id}", $"dropin:{remindedOld.Id}", $"dropin:{failingPayment.Id}",
            $"dropin:{Guid.NewGuid()}",
        }.ToList();
        SendRemindersResultDto first;
        await using (var ctx = db())
            first = await Service(ctx).SendRemindersAsync(new SendRemindersRequestDto { Keys = keys }, admin.Id, "Balances Admin");

        assert(first.Sent == 4 && first.EmailsSent == 4 && first.Failed == 1 && first.Skipped == 3,
            "reminders: sent, skipped and failed are counted per debt");
        assert(first.SkippedItems.Any(s => s.UserId == optedOut.Id && s.Reason == OutstandingBalancesService.SkipRemindersOff)
            && !email.Recipients.Contains(optedOut.Email),
            "reminders: players who turned off payment reminders are skipped");
        assert(first.SkippedItems.Any(s => s.Key == $"dropin:{remindedRecent.Id}" && s.Reason == OutstandingBalancesService.SkipRecentlyReminded)
            && email.Recipients.Count(r => r == reminded.Email) == 1,
            "reminders: the same debt is not reminded twice within 3 days, while an older reminder allows another");
        assert(first.SkippedItems.Any(s => s.Reason == OutstandingBalancesService.SkipNoLongerOutstanding && s.UserId == null),
            "reminders: a selected debt that is no longer outstanding is skipped");
        assert(first.FailedItems.SingleOrDefault()?.UserId == failing.Id, "reminders: a send error is reported as failed");
        assert(email.Recipients.Contains(seasonUnpaid.Email) && email.Recipients.Contains(dropIn.Email) && email.Recipients.Contains(transfer.Email),
            "reminders: each reminder is emailed through the email service");

        await using (var ctx = db())
        {
            var again = await Service(ctx).SendRemindersAsync(new SendRemindersRequestDto { Keys = keys.Take(3).ToList() }, admin.Id, "Balances Admin");
            assert(again.Sent == 0 && again.Skipped == 3 && again.SkippedItems.All(s => s.Reason == OutstandingBalancesService.SkipRecentlyReminded),
                "reminders: sending the same selection again right away is skipped");
            var audit = await ctx.AuditLogs.AsNoTracking().Where(a => a.UserId == admin.Id && a.Action == "OutstandingBalances.RemindersSent").ToListAsync();
            assert(audit.Count == 2 && audit.Any(a => a.Details!.Contains("Sent: 4; Skipped: 3; Failed: 1")), "reminders: every send is audited with its counts");

            var byKind = await Service(ctx).SendRemindersAsync(new SendRemindersRequestDto { Kind = "SeasonFee" }, admin.Id, "Balances Admin");
            assert(byKind.SkippedItems.Any(s => s.UserId == seasonUnpaid.Id && s.Reason == OutstandingBalancesService.SkipRecentlyReminded)
                && email.Recipients.Contains(seasonPending.Email) && !email.Recipients.Contains(seasonPaid.Email),
                "reminders: every row of a kind can be reminded at once");

            var listed = (await Service(ctx).GetBalancesAsync(new OutstandingBalancesQuery())).Items.Single(r => r.PaymentId == dropInPayment.Id);
            assert(listed.LastReminderSentAt is DateTime sent && DateTime.UtcNow - sent < TimeSpan.FromMinutes(5), "balances: a sent reminder shows as the last reminder");
        }

        await using (var ctx = db())
        {
            var service = Service(ctx);
            var dropInHistory = await service.GetReminderHistoryAsync(dropIn.Id, 1, 50);
            var entry = dropInHistory.Items.Single(i => i.Status == ReminderStatuses.Sent);
            assert(dropInHistory.Total == 2 && dropInHistory.Items.Count(i => i.Status == ReminderStatuses.Skipped) == 1 && entry.PaymentId == dropInPayment.Id && entry.Kind == ReminderKinds.DropIn
                && entry.Channel == "email" && entry.SentByUserId == admin.Id && entry.SentAt.Kind == DateTimeKind.Utc && entry.Email == dropIn.Email,
                "history: a sent reminder is recorded with its debt, sender and UTC time");
            var skipped = (await service.GetReminderHistoryAsync(optedOut.Id, 1, 50)).Items.Single();
            assert(skipped.Status == ReminderStatuses.Skipped && skipped.Reason == OutstandingBalancesService.SkipRemindersOff, "history: skipped reminders keep their reason");
            var failed = (await service.GetReminderHistoryAsync(failing.Id, 1, 50)).Items.Single();
            assert(failed.Status == ReminderStatuses.Failed, "history: failed reminders are recorded");
            var season1 = (await service.GetReminderHistoryAsync(seasonUnpaid.Id, 1, 50)).Items;
            assert(season1.Any(l => l.Status == ReminderStatuses.Sent && l.SeasonId == season.Id && l.PaymentId == null), "history: season fee reminders record the season");

            var page1 = await service.GetReminderHistoryAsync(reminded.Id, 1, 2);
            var page2 = await service.GetReminderHistoryAsync(reminded.Id, 2, 2);
            assert(page1.Total == 4 && page1.Items.Count == 2 && page2.Items.Count == 2 && page1.PageSize == 2
                && page1.Items.Concat(page2.Items).Select(i => i.Id).Distinct().Count() == 4
                && page1.Items[0].SentAt >= page2.Items[^1].SentAt,
                "history: paged newest first per player");
            var everyone = await service.GetReminderHistoryAsync(null, 1, 500);
            assert(everyone.PageSize == 200 && everyone.Total >= 12, "history: unfiltered history is paged with a capped page size");
            try { await service.GetReminderHistoryAsync(Guid.NewGuid(), 1, 50); assert(false, "history: unknown player rejected"); }
            catch (SaintHenriBasketball.Application.Exceptions.NotFoundException) { assert(true, "history: an unknown player is not found"); }

            // Leave no season running today for checks that follow.
            var own = await ctx.Seasons.SingleAsync(s => s.Id == season.Id);
            own.StartDate = new DateTime(2000, 1, 1);
            own.EndDate = new DateTime(2000, 6, 30);
            await ctx.SaveChangesAsync();
        }
    }

    /// Records who was emailed; throws for <see cref="FailFor"/>. Only SendEmailAsync is expected.
    private sealed class RecordingEmailService : IEmailService
    {
        public string? FailFor { get; init; }
        public List<string?> Recipients { get; } = new();

        public Task SendEmailAsync(string? to, string subject, string htmlContent)
        {
            if (to == FailFor) throw new InvalidOperationException("simulated delivery failure");
            if (string.IsNullOrWhiteSpace(subject) || !htmlContent.Contains(" $"))
                throw new InvalidOperationException("reminder email is missing its amount");
            Recipients.Add(to);
            return Task.CompletedTask;
        }

        private static Exception Unexpected() => new NotSupportedException("Not used by outstanding balances");
        public Task SendEmailWithAttachmentAsync(string? to, string subject, string htmlContent, string attachmentFilename, byte[] attachmentContent) => throw Unexpected();
        public Task SendConfirmationEmailAsync(string? to, string confirmationLink) => throw Unexpected();
        public Task SendPasswordResetEmailAsync(string? to, string resetLink) => throw Unexpected();
        public Task SendAccountCreatedEmailAsync(string? to, string password, string loginLink) => throw Unexpected();
        public Task SendPaymentCreatedConfirmationAsync(Guid userId, decimal amount, string? reference, EmailLanguage language = EmailLanguage.French) => throw Unexpected();
        public Task SendPaymentConfirmationAsync(Guid userId, decimal amount, string? reference, EmailLanguage language = EmailLanguage.French) => throw Unexpected();
        public Task SendPaymentConfirmationAsync(ApplicationUser user, decimal amount, string? reference, EmailLanguage language = EmailLanguage.French) => throw Unexpected();
        public Task SendPaymentReminderEmailAsync(Guid userId, PaymentPlan paymentPlan, string? customMessage = null) => throw Unexpected();
        public Task SendPaymentReminderEmailAsync(ApplicationUser user, PaymentPlan paymentPlan, string? customMessage = null) => throw Unexpected();
        public Task SendPaymentFailedAsync(ApplicationUser user, decimal amount, string? reference = null, string? reason = null, EmailLanguage language = EmailLanguage.French) => throw Unexpected();
        public Task SendPaymentPlanUpdateEmailAsync(Guid userId, PaymentPlan newPaymentPlan) => throw Unexpected();
        public Task SendAttendanceConfirmationEmailAsync(SessionAttendance attendance) => throw Unexpected();
        public Task SendAttendanceUpdateEmailAsync(SessionAttendance attendance, bool previousStatus, string? reason = null) => throw Unexpected();
        public Task SendAttendanceReminderEmailAsync(Guid userId, string? customMessage = null, Guid? sessionId = null) => throw Unexpected();
        public Task SendLowAttendanceWarningEmailAsync(SessionDto session, List<UserDto> registeredUsers) => throw Unexpected();
        public Task SendSessionCancellationEmailAsync(Session session, List<ApplicationUser> registeredUsers, string? cancellationReason = null, Session? alternativeSession = null) => throw Unexpected();
        public Task SendSeasonRegistrationConfirmationEmailAsync(SeasonRegistration registration) => throw Unexpected();
        public Task SendSeasonRegistrationCancelledEmailAsync(string? userEmail, Season season) => throw Unexpected();
        public Task SendSeasonRegistrationReminderEmailAsync(string? userEmail, Season season, string? customMessage = null) => throw Unexpected();
        public Task SendSeasonStatusUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers) => throw Unexpected();
        public Task SendSeasonUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers, string[] changedProperties) => throw Unexpected();
        public Task SendSeasonPaymentReminderEmailAsync(SeasonRegistration registration) => throw Unexpected();
        public Task SendNewUserNotificationToAdminAsync(ApplicationUser newUser) => throw Unexpected();
        public Task SendAdminNotificationAsync(string subject, string message, string? actionLink = null, string? actionText = null) => throw Unexpected();
        public Task SendGeneralAnnouncementEmailAsync(string? userEmail, string userName, string message) => throw Unexpected();
        public Task SendScheduleChangeEmailAsync(Session session, List<ApplicationUser> affectedUsers, string details, DateTime? newDate = null, TimeSpan? newTime = null) => throw Unexpected();
        public Task SendFacilityUpdateEmailAsync(string? userEmail, string facilityName, string updateDetails, DateTime effectiveDate, string? alternativeFacility = null) => throw Unexpected();
        public Task SendDropInPaymentLinkEmailAsync(string? userEmail, string userName, Guid sessionId, decimal amount, DateTime sessionDate, string startTime, string endTime, EmailLanguage language = EmailLanguage.Bilingual) => throw Unexpected();
        public Task<EmailSendResult> SendTargetedEmailsAsync(EmailType emailType, List<string> emails, EmailLanguage language, string? customMessage, string? customMessageFr) => throw Unexpected();
        public Task<EmailSendResult> SendPaymentRemindersAsync(List<string?> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null) => throw Unexpected();
        public Task<EmailSendResult> SendAttendanceRemindersAsync(List<string?> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null) => throw Unexpected();
        public Task<EmailSendResult> SendSeasonRegistrationRemindersAsync(List<string?> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null) => throw Unexpected();
        public Task<EmailSendResult> SendGeneralAnnouncementsAsync(List<string?> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null) => throw Unexpected();
    }
}
