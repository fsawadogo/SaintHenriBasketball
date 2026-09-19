using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.Session;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Cancelling a session must not write off money that has already been taken.
///
/// A card payment sits Pending until Stripe's webhook completes it, and an async method (bank
/// debit) can land days after the checkout itself completes. Cancelling used to shut the checkout —
/// which does nothing once it is complete — and void the payment regardless. The club kept the
/// money, its own record said Failed, and a failed payment cannot be refunded.
/// Sessions sit in 2021, a year no other check writes to.
internal static class CancelledSessionPaymentChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];

        var paidSession = new Session(new DateTime(2021, 3, 6), 20, 11m, "10:00", "12:00", "Saint-Henri");
        var unpaidSession = new Session(new DateTime(2021, 3, 13), 20, 11m, "10:00", "12:00", "Saint-Henri");

        var payer = new ApplicationUser($"cs_pay_{tag}", $"cs-pay-{tag}@example.test", "test-only", "Paid", $"Payer{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        var browser = new ApplicationUser($"cs_brw_{tag}", $"cs-brw-{tag}@example.test", "test-only", "Never", $"Paid{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };

        // Both are Pending with a card reference; only the first one's checkout actually completed.
        var moneyInFlight = new Payment(payer.Id, 11m, PaymentPlan.DropIn, paidSession.Id)
        { Status = PaymentStatus.Pending, Reference = $"cs_paid_{tag}", CreatedAt = DateTime.UtcNow };
        var abandoned = new Payment(browser.Id, 11m, PaymentPlan.DropIn, unpaidSession.Id)
        { Status = PaymentStatus.Pending, Reference = $"cs_open_{tag}", CreatedAt = DateTime.UtcNow };

        await using (var context = db())
        {
            context.Sessions.AddRange(paidSession, unpaidSession);
            context.Users.AddRange(payer, browser);
            context.SessionRegistrations.AddRange(
                new SessionRegistration(payer.Id, paidSession.Id, PaymentPlan.DropIn),
                new SessionRegistration(browser.Id, unpaidSession.Id, PaymentPlan.DropIn));
            context.Payments.AddRange(moneyInFlight, abandoned);
            await context.SaveChangesAsync();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AppUrl"] = "https://sainthenribasketball.com" }).Build();

        async Task<SessionCancellationResultDto> CancelAsync(Guid sessionId, string checkoutStatus)
        {
            await using var context = db();
            var payments = DispatchProxy.Create<IPaymentService, CancelFakePayments>();
            ((CancelFakePayments)(object)payments).Db = db;
            var sessionService = DispatchProxy.Create<ISessionService, CancelFakeSessions>();
            ((CancelFakeSessions)(object)sessionService).Db = db;
            var stripe = DispatchProxy.Create<IStripeService, CancelFakeStripe>();
            ((CancelFakeStripe)(object)stripe).CheckoutStatus = checkoutStatus;

            var service = new SessionCancellationService(
                new SessionRepository(context),
                new SessionRegistrationRepository(context),
                new PaymentRepository(context),
                sessionService,
                payments,
                DispatchProxy.Create<IPaymentRefundService, CancelFakeRefunds>(),
                stripe,
                DispatchProxy.Create<IEmailService, CancelSilentEmail>(),
                DispatchProxy.Create<INotificationService, CancelNoNotices>(),
                new WaitlistRepository(context),
                configuration,
                NullLogger<SessionCancellationService>.Instance);

            return await service.CancelAsync(sessionId, new CancelSessionRequest { Reason = "Gym closed" });
        }

        // --- The checkout already completed: the money is on its way ---
        var paidResult = await CancelAsync(paidSession.Id, "complete");

        await using (var context = db())
        {
            var after = await context.Payments.AsNoTracking().FirstAsync(p => p.Id == moneyInFlight.Id);
            assert(after.Status == PaymentStatus.Pending,
                "cancelled session: a payment whose checkout already completed is left pending, not written off");
        }
        assert(paidResult.PaymentsVoided == 0,
            "cancelled session: nothing is reported as voided when the money is already on its way");
        assert(paidResult.PaidNotRefunded == 1,
            "cancelled session: it is counted as money the club is holding, which is what the player is told");

        // --- The checkout was still open: nothing was ever taken ---
        var unpaidResult = await CancelAsync(unpaidSession.Id, "open");

        await using (var context = db())
        {
            var after = await context.Payments.AsNoTracking().FirstAsync(p => p.Id == abandoned.Id);
            assert(after.Status == PaymentStatus.Failed,
                "cancelled session: an abandoned checkout is still written off, so nobody is chased for it");
        }
        assert(unpaidResult.PaymentsVoided == 1,
            "cancelled session: voiding an unpaid charge is still reported");
    }

    public class CancelFakeStripe : DispatchProxy
    {
        public string CheckoutStatus { get; set; } = "open";

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IStripeService.ExpireCheckoutAsync))
                return Task.FromResult(CheckoutStatus != "complete");
            throw new NotSupportedException($"Unexpected Stripe call: {targetMethod?.Name}");
        }
    }

    public class CancelFakePayments : DispatchProxy
    {
        public Func<ApplicationDbContext>? Db { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IPaymentService.VoidForCancelledSessionAsync))
            {
                var paymentId = (Guid)args![0]!;
                using var context = Db!();
                var payment = context.Payments.First(p => p.Id == paymentId);
                if (payment.Status != PaymentStatus.Pending) return Task.FromResult(false);
                payment.Status = PaymentStatus.Failed;
                context.SaveChanges();
                return Task.FromResult(true);
            }
            throw new NotSupportedException($"Unexpected payment call: {targetMethod?.Name}");
        }
    }

    public class CancelFakeSessions : DispatchProxy
    {
        public Func<ApplicationDbContext>? Db { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ISessionService.CancelSessionAsync))
            {
                var sessionId = (Guid)args![0]!;
                using var context = Db!();
                var session = context.Sessions.First(s => s.Id == sessionId);
                session.Status = SessionStatus.Cancelled;
                context.SaveChanges();
                return Task.CompletedTask;
            }
            throw new NotSupportedException($"Unexpected session call: {targetMethod?.Name}");
        }
    }

    public class CancelFakeRefunds : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException("No refund should be attempted here.");
    }

    public class CancelSilentEmail : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Task.CompletedTask;
    }

    /// In-app notices are not what these checks are about.
    public class CancelNoNotices : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Task.CompletedTask;
    }
}
