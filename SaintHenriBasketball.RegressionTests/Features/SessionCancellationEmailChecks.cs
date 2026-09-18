using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.DTOs.Payment;
using SaintHenriBasketball.Application.DTOs.Session;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.Templates;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the session cancellation email. Uses its own data; other checks share the database.
///
/// The email used to promise every player that a paid drop-in "will be applied to a future session".
/// That was true for one of the four things that can actually happen, so these checks pin what each
/// player is told, and that nobody is left out.
internal static class SessionCancellationEmailChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..6];

        // --- The template, on its own: what each outcome says, in both languages ---
        SessionCancellationEmailModel Model(CancellationMoney money, decimal amount = 10m) => new()
        {
            FirstName = "Jeanne",
            SessionDate = new DateTime(2003, 6, 14),
            StartTime = "10:00",
            EndTime = "12:00",
            Location = "Saint-Henri",
            Money = money,
            Amount = amount,
            AppUrl = "https://sainthenribasketball.com",
        };

        var cancelledMoney = EmailTemplates.Sessions.GetSessionCancellationEmail(Model(CancellationMoney.Cancelled), EmailLanguage.English);
        assert(cancelledMoney.Contains("has been cancelled. There is nothing to pay", StringComparison.OrdinalIgnoreCase)
            && !cancelledMoney.Contains("applied to a future session", StringComparison.OrdinalIgnoreCase),
            "cancellation email: an unpaid session says there is nothing to pay, not that a payment was carried over");

        var credited = EmailTemplates.Sessions.GetSessionCancellationEmail(Model(CancellationMoney.Credited), EmailLanguage.English);
        assert(credited.Contains("credit on your account", StringComparison.OrdinalIgnoreCase),
            "cancellation email: a refunded payment is described as account credit");

        var held = EmailTemplates.Sessions.GetSessionCancellationEmail(Model(CancellationMoney.StillHeld), EmailLanguage.English);
        assert(held.Contains("has not been returned yet", StringComparison.OrdinalIgnoreCase),
            "cancellation email: money the club still holds is admitted, never dressed up as a refund");

        var pass = EmailTemplates.Sessions.GetSessionCancellationEmail(Model(CancellationMoney.CoveredByPass), EmailLanguage.English);
        assert(pass.Contains("season pass covers every session", StringComparison.OrdinalIgnoreCase),
            "cancellation email: a season player is told the cancelled session costs them nothing");

        var french = EmailTemplates.Sessions.GetSessionCancellationEmail(Model(CancellationMoney.Credited), EmailLanguage.French);
        assert(french.Contains("crédit à votre compte", StringComparison.OrdinalIgnoreCase) && !french.Contains("credit on your account", StringComparison.OrdinalIgnoreCase),
            "cancellation email: the French version is French, not a translation stapled to the English one");

        var waiting = Model(CancellationMoney.Nothing);
        waiting.WasWaiting = true;
        var waitingHtml = EmailTemplates.Sessions.GetSessionCancellationEmail(waiting, EmailLanguage.English);
        assert(waitingHtml.Contains("no place will come free", StringComparison.OrdinalIgnoreCase),
            "cancellation email: someone waiting for a place is told the place will never come");

        var withNext = Model(CancellationMoney.Nothing);
        var nextId = Guid.NewGuid();
        withNext.NextSessionId = nextId;
        withNext.NextSessionDate = new DateTime(2003, 6, 21);
        withNext.NextStartTime = "10:00";
        withNext.NextSpotsLeft = 9;
        var nextHtml = EmailTemplates.Sessions.GetSessionCancellationEmail(withNext, EmailLanguage.English);
        assert(nextHtml.Contains($"/sessions/{nextId}/book") && !nextHtml.Contains($"/session/{nextId}/register"),
            "cancellation email: the next-session button points at a route that exists");

        // --- The service: who hears about it, and what their money did ---
        var booked = new ApplicationUser($"sc_book_{tag}", $"sc-book-{tag}@example.test", "test-only", "Bea", $"Booked{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        var quiet = new ApplicationUser($"sc_quiet_{tag}", $"sc-quiet-{tag}@example.test", "test-only", "Quinn", $"Quiet{tag}", PaymentPlan.DropIn) { EmailConfirmed = true, EmailNotificationsEnabled = false };
        var waiter = new ApplicationUser($"sc_wait_{tag}", $"sc-wait-{tag}@example.test", "test-only", "Wes", $"Waiting{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        var seasonPlayer = new ApplicationUser($"sc_pass_{tag}", $"sc-pass-{tag}@example.test", "test-only", "Sam", $"Pass{tag}", PaymentPlan.Season) { EmailConfirmed = true };

        var today = SaintHenriBasketball.Application.Helpers.SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;
        // Far enough out that no other check's session sits between these two: the database is shared,
        // and this asserts exactly which session the email offers next.
        var doomed = new Session(today.AddDays(300), 12, 10m, "10:00", "12:00", "Saint-Henri");
        var following = new Session(today.AddDays(307), 12, 10m, "10:00", "12:00", "Saint-Henri");

        // Bea owes for the session; the money is cancelled with it.
        var beaPayment = new Payment(booked.Id, 10m, PaymentPlan.DropIn, doomed.Id) { Reference = $"DROPIN-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };

        await using (var context = db())
        {
            context.Users.AddRange(booked, quiet, waiter, seasonPlayer);
            context.Sessions.AddRange(doomed, following);
            context.SessionRegistrations.AddRange(
                new SessionRegistration(booked.Id, doomed.Id, PaymentPlan.DropIn),
                new SessionRegistration(quiet.Id, doomed.Id, PaymentPlan.DropIn),
                new SessionRegistration(seasonPlayer.Id, doomed.Id, PaymentPlan.Season));
            context.Payments.Add(beaPayment);
            context.Waitlists.Add(new Waitlist(waiter.Id, doomed.Id, 1));
            await context.SaveChangesAsync();
        }

        var emailProxy = DispatchProxy.Create<IEmailService, CancellationEmailRecorder>();
        var email = (CancellationEmailRecorder)(object)emailProxy;
        var notices = new CancellationNoticeRecorder();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AppUrl"] = "https://sainthenribasketball.com" }).Build();

        SessionCancellationResultDto result;
        await using (var context = db())
        {
            var sessions = new SessionRepository(context);
            var payments = DispatchProxy.Create<IPaymentService, CancellationFakePayments>();
            ((CancellationFakePayments)(object)payments).Db = db;
            var sessionService = DispatchProxy.Create<ISessionService, CancellationFakeSessions>();
            ((CancellationFakeSessions)(object)sessionService).Db = db;
            var refunds = DispatchProxy.Create<IPaymentRefundService, CancellationFakeRefunds>();
            var stripe = DispatchProxy.Create<IStripeService, CancellationFakeStripe>();

            var service = new SessionCancellationService(
                sessions,
                new SessionRegistrationRepository(context),
                new PaymentRepository(context),
                sessionService,
                payments,
                refunds,
                stripe,
                emailProxy,
                notices,
                new WaitlistRepository(context),
                configuration,
                NullLogger<SessionCancellationService>.Instance);

            result = await service.CancelAsync(doomed.Id, new CancelSessionRequest { Reason = "Gym closed", RefundPaidToCredit = false });
        }

        assert(result.PlayersNotified == 3 && result.WaitingPlayersNotified == 1,
            "cancellation: everyone holding a place is told, and so is everyone waiting for one");

        assert(email.Sent.Any(r => r.User.Id == quiet.Id),
            "cancellation: a player who turned email notifications off is still told the gym is shut");

        var beaModel = email.Sent.Single(r => r.User.Id == booked.Id).Model;
        assert(beaModel.Money == CancellationMoney.Cancelled && beaModel.Amount == 10m,
            "cancellation: an unpaid drop-in is reported as cancelled, with its amount");

        var passModel = email.Sent.Single(r => r.User.Id == seasonPlayer.Id).Model;
        assert(passModel.Money == CancellationMoney.CoveredByPass,
            "cancellation: a season player is told their pass covers it");

        var waiterModel = email.Sent.Single(r => r.User.Id == waiter.Id).Model;
        assert(waiterModel.WasWaiting && waiterModel.Money == CancellationMoney.Nothing,
            "cancellation: the waitlist email says nothing about money, because none changed hands");

        assert(email.Sent.All(r => r.Model.NextSessionId == following.Id && r.Model.NextSpotsLeft == 12),
            "cancellation: every email offers the next session that is still going ahead");

        await using (var context = db())
        {
            var entry = await context.Waitlists.AsNoTracking().SingleAsync(w => w.SessionId == doomed.Id && w.UserId == waiter.Id);
            assert(entry.Status == WaitlistStatus.Cancelled,
                "cancellation: waitlist entries are closed, so nobody queues for a session that will never run");
        }

        assert(notices.Sent.Count == 4 && notices.Sent.Any(n => n.UserId == waiter.Id && n.Body.Contains("waiting for", StringComparison.OrdinalIgnoreCase)),
            "cancellation: the in-app notice reaches both groups, and reads differently for someone who was waiting");
    }

    /// Records the cancellation emails. Anything else a cancellation tried to send would throw,
    /// which is the point: that would be a change worth noticing.
    public class CancellationEmailRecorder : DispatchProxy
    {
        public List<(ApplicationUser User, SessionCancellationEmailModel Model)> Sent { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IEmailService.SendSessionCancellationEmailsAsync))
            {
                Sent.AddRange((IReadOnlyList<(ApplicationUser, SessionCancellationEmailModel)>)args![0]!);
                return Task.CompletedTask;
            }
            throw new NotSupportedException($"Unexpected email: {targetMethod?.Name}");
        }
    }

    internal sealed class CancellationNoticeRecorder : INotificationService
    {
        public List<(Guid UserId, string Body)> Sent { get; } = new();

        public Task CreateAsync(Guid userId, NotificationType type, string title, string body, string? url = null)
        {
            Sent.Add((userId, body));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SaintHenriBasketball.Application.DTOs.Notifications.NotificationDto>> GetRecentAsync(Guid userId, int take = 20) =>
            Task.FromResult<IReadOnlyList<SaintHenriBasketball.Application.DTOs.Notifications.NotificationDto>>(Array.Empty<SaintHenriBasketball.Application.DTOs.Notifications.NotificationDto>());
        public Task<int> GetUnreadCountAsync(Guid userId) => Task.FromResult(0);
        public Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId) => Task.FromResult(false);
        public Task<int> MarkAllAsReadAsync(Guid userId) => Task.FromResult(0);
    }

    public class CancellationFakePayments : DispatchProxy
    {
        public Func<ApplicationDbContext>? Db { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IPaymentService.VoidForCancelledSessionAsync))
            {
                using var context = Db!();
                var payment = context.Payments.Single(p => p.Id == (Guid)args![0]!);
                payment.Status = PaymentStatus.Failed;
                context.SaveChanges();
                return Task.FromResult(true);
            }
            throw new NotSupportedException($"Unexpected payment call: {targetMethod?.Name}");
        }
    }

    public class CancellationFakeSessions : DispatchProxy
    {
        public Func<ApplicationDbContext>? Db { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ISessionService.CancelSessionAsync))
            {
                using var context = Db!();
                var session = context.Sessions.Single(s => s.Id == (Guid)args![0]!);
                session.Status = SessionStatus.Cancelled;
                context.SaveChanges();
                return Task.CompletedTask;
            }
            throw new NotSupportedException($"Unexpected session call: {targetMethod?.Name}");
        }
    }

    public class CancellationFakeRefunds : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException($"No refund was expected: {targetMethod?.Name}");
    }

    public class CancellationFakeStripe : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Task.CompletedTask;
    }
}
