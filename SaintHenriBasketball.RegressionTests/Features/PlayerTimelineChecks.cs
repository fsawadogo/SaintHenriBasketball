using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Application.DTOs.PlayerTimeline;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the `player-timeline` feature. Uses its own data; other checks share the database.
internal static class PlayerTimelineChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..12];
        var now = DateTime.UtcNow;
        var t0 = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc).AddDays(-30);
        DateTime At(int minutes) => t0.AddMinutes(minutes);

        // Deactivated from the start: the timeline must still work for a player who can't sign in.
        var player = new ApplicationUser($"timeline-{tag}", $"timeline-{tag}@example.test", "test-only", "Timeline", "Player", PaymentPlan.DropIn)
        { EmailConfirmed = true, IsDeactivated = true, DeactivatedOn = At(100), AdminNotes = "Prefers evening sessions" };
        var admin = new ApplicationUser($"timeline-admin-{tag}", $"timeline-admin-{tag}@example.test", "test-only", "Timeline", "Admin", PaymentPlan.DropIn)
        { EmailConfirmed = true, IsAdmin = true };
        var session = new Session(t0.Date, 10, 10m, "10:00", "12:00", "Timeline court");

        var sessionPayment = new Payment(player.Id, 10m, PaymentPlan.DropIn, session.Id) { Status = PaymentStatus.Completed, CreatedAt = At(0), PaymentDate = At(10) };
        var refundedPayment = new Payment(player.Id, 12m, PaymentPlan.DropIn)
        { Status = PaymentStatus.Refunded, CreatedAt = At(20), PaymentDate = At(25), RefundedOn = At(90), RefundMethod = RefundMethod.AccountCredit, RefundReason = "Rained out" };
        var legacyPayment = new Payment(player.Id, 15m, PaymentPlan.DropIn) { Status = PaymentStatus.Pending, PaymentDate = At(40) }; // CreatedAt never set
        var refundCredit = new AccountCredit(player.Id, 12m, AccountCreditKind.Refund, paymentId: refundedPayment.Id, note: "Rained out");
        var manualCredit = new AccountCredit(player.Id, -5m, AccountCreditKind.ManualAdjustment, note: "Duplicate reward", createdByUserId: admin.Id);
        var email = new EmailLog(player.Email!, "Your payment failed", EmailType.PaymentConfirmation);
        email.MarkFailed("Mailbox full");
        var olderNotes = new AuditLog("UpdateNotes", "User", player.Id, "Notes updated", admin.Id, "Timeline Admin (old)");
        var newerNotes = new AuditLog("UpdateNotes", "User", player.Id, "Notes updated", admin.Id, "Timeline Admin");
        var answer = new SessionAttendance
        {
            Id = Guid.NewGuid(), SessionId = session.Id, UserId = player.Id, IsAttending = false, UpdateReason = "Injured",
            CreatedOn = At(35), LastUpdated = At(45), CheckInTime = At(70),
        };
        var waiver = new WaiverAcceptance(player.Id, 1, null);
        // Four notifications at the same instant: paging must not duplicate or skip any of them.
        var notifications = Enumerable.Range(1, 4).Select(i => new Notification(player.Id, NotificationType.Generic, $"Timeline notice {i}", "Body")).ToList();
        notifications[0].ReadAt = At(85);

        await using (var context = db())
        {
            context.Users.AddRange(player, admin);
            context.Sessions.Add(session);
            context.Payments.AddRange(sessionPayment, refundedPayment, legacyPayment);
            await context.SaveChangesAsync();
            context.AccountCredits.AddRange(refundCredit, manualCredit);
            context.EmailLogs.Add(email);
            context.AuditLogs.AddRange(olderNotes, newerNotes);
            context.SessionAttendances.Add(answer);
            context.WaiverAcceptances.Add(waiver);
            context.Notifications.AddRange(notifications);
            await context.SaveChangesAsync();

            // Timestamps with private setters are pinned after insert so the expected order is exact.
            DateTime refundCreditAt = At(91), manualCreditAt = At(50), emailAt = At(30), olderNotesAt = At(5), newerNotesAt = At(60), waiverAt = At(15), noticesAt = At(80);
            await context.AccountCredits.Where(c => c.Id == refundCredit.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, refundCreditAt));
            await context.AccountCredits.Where(c => c.Id == manualCredit.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, manualCreditAt));
            await context.EmailLogs.Where(l => l.Id == email.Id).ExecuteUpdateAsync(s => s.SetProperty(l => l.SentAt, emailAt));
            await context.AuditLogs.Where(l => l.Id == olderNotes.Id).ExecuteUpdateAsync(s => s.SetProperty(l => l.CreatedAt, olderNotesAt));
            await context.AuditLogs.Where(l => l.Id == newerNotes.Id).ExecuteUpdateAsync(s => s.SetProperty(l => l.CreatedAt, newerNotesAt));
            await context.WaiverAcceptances.Where(w => w.Id == waiver.Id).ExecuteUpdateAsync(s => s.SetProperty(w => w.AcceptedAt, waiverAt));
            var notificationIds = notifications.Select(n => n.Id).ToList();
            await context.Notifications.Where(n => notificationIds.Contains(n.Id)).ExecuteUpdateAsync(s => s.SetProperty(n => n.CreatedOn, noticesAt));
        }

        async Task<PlayerTimelineDto> Timeline(int page, int pageSize, params string[] types)
        {
            await using var context = db();
            return await new PlayerTimelineService(new PlayerTimelineRepository(context))
                .GetTimelineAsync(player.Id, new PlayerTimelineQuery { Page = page, PageSize = pageSize, Types = types });
        }

        var expected = new List<string> { $"credit:{refundCredit.Id}", $"payment:{refundedPayment.Id}:refunded" };
        expected.AddRange(notifications.Select(n => $"notification:{n.Id}").OrderBy(id => id, StringComparer.Ordinal));
        expected.AddRange(new[]
        {
            $"attendance:{answer.Id}:checkin", $"admin:{newerNotes.Id}", $"credit:{manualCredit.Id}", $"attendance:{answer.Id}:answer",
            $"payment:{legacyPayment.Id}:created", $"message:{email.Id}", $"payment:{refundedPayment.Id}:completed",
            $"payment:{refundedPayment.Id}:created", $"waiver:{waiver.Id}", $"payment:{sessionPayment.Id}:completed",
            $"admin:{olderNotes.Id}", $"payment:{sessionPayment.Id}:created",
        });

        var full = await Timeline(1, 100);
        var manual = full.Events.Single(e => e.Id == $"credit:{manualCredit.Id}");
        var refunded = full.Events.Single(e => e.Id == $"payment:{refundedPayment.Id}:refunded");
        var changedAnswer = full.Events.Single(e => e.Id == $"attendance:{answer.Id}:answer");
        var failedEmail = full.Events.Single(e => e.Id == $"message:{email.Id}");
        assert(full.Events.Select(e => e.Id).SequenceEqual(expected) && full.Total == expected.Count && !full.HasMore
            && full.Events.All(e => e.OccurredAt.Kind == DateTimeKind.Utc)
            && full.Events.Single(e => e.Id == $"payment:{legacyPayment.Id}:created").OccurredAt == At(40)
            && refunded.OccurredAt == At(90) && refunded.Detail.Contains("Rained out") && refunded.Detail.Contains("account credit") && refunded.RelatedId == refundedPayment.Id
            && full.Events.Single(e => e.Id == $"payment:{sessionPayment.Id}:completed").Detail.Contains("Timeline court")
            && manual.Amount == -5m && manual.ActorName == "Timeline Admin" && manual.Detail.Contains("Duplicate reward")
            && changedAnswer.Subtype == "answerUpdated" && changedAnswer.OccurredAt == At(45) && changedAnswer.RelatedId == session.Id && changedAnswer.Detail.Contains("Injured")
            && failedEmail.Title == "Your payment failed" && failedEmail.Detail.Contains("Mailbox full")
            && full.Events.Single(e => e.Id == $"admin:{newerNotes.Id}").ActorName == "Timeline Admin"
            && full.IsDeactivated && !full.IsAnonymized,
            "the player timeline merges payments, credits, emails, audit entries, attendance, waivers and notifications newest first, deactivated or not");

        var pagingHolds = true;
        foreach (var size in new[] { 1, 3, 4, 7 })
        {
            var collected = new List<string>();
            PlayerTimelineDto current;
            var page = 1;
            do
            {
                current = await Timeline(page++, size);
                collected.AddRange(current.Events.Select(e => e.Id));
            } while (current.HasMore);
            pagingHolds &= collected.SequenceEqual(expected) && collected.Distinct().Count() == collected.Count;
        }
        var pastTheEnd = await Timeline(10, 10);
        assert(pagingHolds && pastTheEnd.Events.Count == 0 && pastTheEnd.Total == expected.Count,
            "paging the timeline at any page size returns every event once, even events with the same timestamp");

        var filtered = await Timeline(1, 100, "payment,ADMIN");
        var filteredRepeated = await Timeline(1, 3, "admin", "payment");
        int Count(PlayerTimelineDto d, string type) => d.Summary.Single(s => s.Type == type).Count;
        var unknownType = false;
        try { await Timeline(1, 10, "payments"); } catch (ValidationException) { unknownType = true; }
        var tooDeep = false;
        try { await Timeline(PlayerTimelineService.MaxWindow / PlayerTimelineService.MaxPageSize + 1, PlayerTimelineService.MaxPageSize); } catch (ValidationException) { tooDeep = true; }
        assert(filtered.Events.Select(e => e.Id).SequenceEqual(expected.Where(id => id.StartsWith("payment:") || id.StartsWith("admin:")))
            && filtered.Total == 8 && filteredRepeated.Total == 8 && filteredRepeated.HasMore
            && filtered.Types.SequenceEqual(new[] { "payment", "admin" })
            && Count(filtered, "payment") == 6 && Count(filtered, "credit") == 2 && Count(filtered, "referral") == 0 && Count(filtered, "waiver") == 1
            && Count(filtered, "attendance") == 2 && Count(filtered, "message") == 1 && Count(filtered, "notification") == 4 && Count(filtered, "admin") == 2
            && filtered.Summary.Select(s => s.Type).SequenceEqual(PlayerTimelineEventTypes.All)
            && unknownType && tooDeep,
            "the timeline filters by event type and reports every type's count for the filter choices");

        assert(full.Notes.Text == "Prefers evening sessions" && full.Notes.LastChangedAt == At(60)
            && full.Notes.LastChangedAt?.Kind == DateTimeKind.Utc && full.Notes.LastChangedBy == "Timeline Admin"
            && !full.Events.Any(e => e.Type == "notes"),
            "admin notes come with the timeline, dated by the most recent notes change");

        var unknownPlayer = false;
        try
        {
            await using var context = db();
            await new PlayerTimelineService(new PlayerTimelineRepository(context)).GetTimelineAsync(Guid.NewGuid(), new PlayerTimelineQuery());
        }
        catch (NotFoundException) { unknownPlayer = true; }
        assert(unknownPlayer, "the timeline of an unknown player is not found");

        assert(!full.MessagesUnavailable, "a deactivated player's emails are still matched");
        await using (var context = db())
        {
            // What AccountLifecycleService does to a player who asked to be erased.
            var erased = await context.Users.SingleAsync(u => u.Id == player.Id);
            erased.FirstName = "Former";
            erased.LastName = "player";
            erased.Username = $"deleted-{player.Id:N}";
            erased.Email = $"deleted-{player.Id:N}@deleted.invalid";
            erased.AdminNotes = null;
            erased.AnonymizedOn = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
        var anonymized = await Timeline(1, 100);
        assert(anonymized.MessagesUnavailable && anonymized.IsAnonymized && Count(anonymized, "message") == 0
            && !anonymized.Events.Any(e => e.Type == "message") && anonymized.Total == expected.Count - 1
            && Count(anonymized, "payment") == 6 && anonymized.Notes.Text == null && anonymized.PlayerName == "Former player",
            "an anonymized player's timeline keeps its history and says emails can no longer be matched");
    }
}
