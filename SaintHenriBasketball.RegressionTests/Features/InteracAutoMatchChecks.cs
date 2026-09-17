using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.InteracDeposits;
using SaintHenriBasketball.Application.DTOs.Payment;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the `interac-auto-match` feature. Uses its own data; other checks share the database.
/// The emails below copy the layout of a real Tangerine auto-deposit confirmation; the names, amounts
/// and references are invented.
internal static class InteracAutoMatchChecks
{
    /// The confirmation as the bank writes it: a labelled table, with the sender's note under "Message".
    private static string Email(string sender, string amount, string? message, string reference, string date) =>
        $"""
         <div>Hi Club Admin,</div>
         <div>Funds Deposited!</div>
         <div>${amount}</div>
         <div>Your funds have been automatically deposited into your account at <b>Tangerine Bank</b>.</div>
         <div>Tangerine Bank</div><div>Account ending in 1352</div>
         <div>Transfer Details</div>
         {(message == null ? string.Empty : $"<div>Message:</div><div>{message}</div>")}
         <div>Date:</div><div>{date}</div>
         <div>Reference Number:</div><div>{reference}</div>
         <div>Sent From:</div><div>{sender}</div>
         <div>Amount:</div><div>${amount} (CAD)</div>
         """;

    private static string Subject(string sender, string amount) =>
        $"Interac e-Transfer: You've received ${amount} from {sender} and it has been automatically deposited.";

    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..6];

        // --- The parser, on its own ---
        var parsed = InteracEmailParser.Parse(Subject("Jeanne Tremblay", "10.00"), Email("Jeanne Tremblay", "10.00", $"DROPIN-2605-1591", "CAEVDQPM", "May 25, 2026"));
        assert(parsed != null && parsed.Amount == 10m && parsed.SenderName == "Jeanne Tremblay"
            && parsed.Message == "DROPIN-2605-1591" && parsed.ReferenceNumber == "CAEVDQPM"
            && parsed.SentOn == new DateTime(2026, 5, 25),
            "interac parser: reads the amount, sender, message, reference and date from a confirmation");

        var noMessage = InteracEmailParser.Parse(Subject("Paul-Henri Côté", "20.00"), Email("Paul-Henri Côté", "20.00", null, "C1AjqSWNPeDY", "May 30, 2026"));
        assert(noMessage != null && noMessage.Amount == 20m && noMessage.Message == null && noMessage.SenderName == "Paul-Henri Côté",
            "interac parser: a transfer with no message is still read");

        assert(InteracEmailParser.Parse("Your statement is ready", "<div>Nothing to do with a transfer</div>") == null,
            "interac parser: an email that is not a deposit is refused, not guessed at");

        assert(InteracDepositService.NamesMatch("Jean-Claude Koffi", "koffi jean claude")
            && InteracDepositService.NamesMatch("Élodie Gagnon", "elodie gagnon")
            && !InteracDepositService.NamesMatch("Jeanne Tremblay", "Marc Tremblay"),
            "interac matching: names compare without accents, case or word order, and two different people never match");

        // --- Proving the caller is really forwarding an Interac email ---
        assert(InteracWebhookVerification.SecretMatches("s3cret", "s3cret")
            && !InteracWebhookVerification.SecretMatches("s3cret ", "s3cret")
            && !InteracWebhookVerification.SecretMatches(null, "s3cret")
            && !InteracWebhookVerification.SecretMatches("anything", null),
            "interac webhook: the shared token must match exactly, and no configured token refuses everything");

        const string signingSecret = "signing-secret";
        const string rawBody = "{\"subject\":\"x\"}";
        var signature = System.Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(signingSecret), System.Text.Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        assert(InteracWebhookVerification.SignatureMatches(signature, rawBody, signingSecret)
            && InteracWebhookVerification.SignatureMatches($"sha256={signature}", rawBody, signingSecret)
            && !InteracWebhookVerification.SignatureMatches(signature, rawBody + " ", signingSecret)
            && !InteracWebhookVerification.SignatureMatches(null, rawBody, signingSecret),
            "interac webhook: the signature covers the body as sent, in either header shape");

        var forwarded = """
            ---------- Forwarded message ---------
            From: Interac <notify@payments.interac.ca>
            Date: Mon, 25 May 2026
            Subject: Interac e-Transfer
            """;
        assert(InteracWebhookVerification.OriginalSender(null, forwarded) == "notify@payments.interac.ca"
            && InteracWebhookVerification.OriginalSender("Interac <notify@payments.interac.ca>", null) == "notify@payments.interac.ca",
            "interac webhook: the original sender is read from the forwarded text when the service reports the forwarder instead");

        assert(InteracWebhookVerification.SenderAllowed("notify@payments.interac.ca", null)
            && InteracWebhookVerification.SenderAllowed("x@mail.interac.ca", null)
            && !InteracWebhookVerification.SenderAllowed("attacker@interac.ca.evil.test", null)
            && !InteracWebhookVerification.SenderAllowed("someone@gmail.com", null)
            && !InteracWebhookVerification.SenderAllowed(null, null),
            "interac webhook: only Interac's domains pass, and a lookalike domain does not");

        // --- Ingesting, against real payments ---
        var payer = new ApplicationUser($"ia_payer_{tag}", $"ia-payer-{tag}@example.test", "test-only", "Jeanne", $"Tremblay{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        var other = new ApplicationUser($"ia_other_{tag}", $"ia-other-{tag}@example.test", "test-only", "Marc", $"Autre{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        var session = new Session(DateTime.UtcNow.Date.AddDays(3), 10, 10m, "10:00", "12:00", "Saint-Henri");

        // Four digits derived from the tag: stable across runs, and unique to this run.
        var exactReference = $"DROPIN-2605-{int.Parse(tag[..4], System.Globalization.NumberStyles.HexNumber) % 9000 + 1000}";
        var matching = new Payment(payer.Id, 10m, PaymentPlan.DropIn, session.Id) { Reference = exactReference, CreatedAt = DateTime.UtcNow.AddDays(-2), PaymentDate = DateTime.UtcNow.AddDays(-2) };
        var wrongAmount = new Payment(other.Id, 25m, PaymentPlan.DropIn, session.Id) { Reference = $"DROPIN-2605-7777", CreatedAt = DateTime.UtcNow.AddDays(-2), PaymentDate = DateTime.UtcNow.AddDays(-2) };

        await using (var context = db())
        {
            context.Users.AddRange(payer, other);
            context.Sessions.Add(session);
            context.Payments.AddRange(matching, wrongAmount);
            await context.SaveChangesAsync();
        }

        var payments = DispatchProxy.Create<IPaymentService, InteracFakePayments>();
        ((InteracFakePayments)(object)payments).Db = db;

        InteracDepositService Service(ApplicationDbContext context) => new(
            new InteracDepositRepository(context),
            payments,
            new AuditLogService(new AuditLogRepository(context)),
            NullLogger<InteracDepositService>.Instance);

        IngestInteracEmailResultDto result;
        await using (var context = db())
            result = await Service(context).IngestAsync(new IngestInteracEmailDto
            {
                MessageId = $"msg-{tag}-1",
                Subject = Subject($"Jeanne Tremblay{tag}", "10.00"),
                Body = Email($"Jeanne Tremblay{tag}", "10.00", exactReference, $"CAREF{tag}", "May 25, 2026"),
                ReceivedAt = DateTimeOffset.UtcNow,
            });

        assert(result.Outcome == "parsed" && result.Status == InteracDepositStatus.Matched
            && result.Confidence == InteracMatchConfidence.Exact && result.MatchedPaymentId == matching.Id,
            "interac auto-match: a reference and an amount that both agree settle the payment without an admin");

        await using (var context = db())
        {
            var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.Id == matching.Id);
            assert(payment.Status == PaymentStatus.Completed, "interac auto-match: the matched payment is marked paid");
        }

        // The same email forwarded twice.
        IngestInteracEmailResultDto repeat;
        await using (var context = db())
            repeat = await Service(context).IngestAsync(new IngestInteracEmailDto
            {
                MessageId = $"msg-{tag}-1-again",
                Subject = Subject($"Jeanne Tremblay{tag}", "10.00"),
                Body = Email($"Jeanne Tremblay{tag}", "10.00", exactReference, $"CAREF{tag}", "May 25, 2026"),
                ReceivedAt = DateTimeOffset.UtcNow,
            });
        assert(repeat.Outcome == "duplicate" && repeat.DepositId == result.DepositId,
            "interac auto-match: the same confirmation forwarded twice is stored once, so money is never counted twice");

        // Right reference, wrong money: an admin decides.
        IngestInteracEmailResultDto mismatch;
        await using (var context = db())
            mismatch = await Service(context).IngestAsync(new IngestInteracEmailDto
            {
                MessageId = $"msg-{tag}-2",
                Subject = Subject($"Marc Autre{tag}", "10.00"),
                Body = Email($"Marc Autre{tag}", "10.00", "DROPIN-2605-7777", $"CAREF{tag}2", "May 26, 2026"),
                ReceivedAt = DateTimeOffset.UtcNow,
            });
        assert(mismatch.Outcome == "parsed" && mismatch.Status == InteracDepositStatus.Unmatched
            && mismatch.Confidence == InteracMatchConfidence.Likely,
            "interac auto-match: a deposit whose amount differs from the payment waits for an admin");

        await using (var context = db())
        {
            var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.Id == wrongAmount.Id);
            assert(payment.Status == PaymentStatus.Pending, "interac auto-match: a doubtful deposit never marks a payment paid on its own");
        }

        // A stranger's deposit: nothing to match.
        IngestInteracEmailResultDto stranger;
        await using (var context = db())
            stranger = await Service(context).IngestAsync(new IngestInteracEmailDto
            {
                MessageId = $"msg-{tag}-3",
                Subject = Subject($"Inconnu Personne{tag}", "55.00"),
                Body = Email($"Inconnu Personne{tag}", "55.00", null, $"CAREF{tag}3", "May 27, 2026"),
                ReceivedAt = DateTimeOffset.UtcNow,
            });
        assert(stranger.Status == InteracDepositStatus.Unmatched && stranger.Confidence == InteracMatchConfidence.None,
            "interac auto-match: a deposit nobody is expecting is kept, unmatched, for an admin to read");

        // An admin ties the doubtful one to its payment by hand.
        await using (var context = db())
        {
            var matched = await Service(context).MatchAsync(mismatch.DepositId!.Value, wrongAmount.Id, null, "Admin Test");
            assert(matched.Status == nameof(InteracDepositStatus.Matched) && matched.MatchedPaymentId == wrongAmount.Id,
                "interac auto-match: an admin can tie a deposit to a payment, which is then marked paid");
        }

        await using (var context = db())
        {
            var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.Id == wrongAmount.Id);
            assert(payment.Status == PaymentStatus.Completed, "interac auto-match: the payment an admin matched is paid");
        }

        // The reference the app hands out today is a bare GUID, not the older short form.
        var guidPayer = new ApplicationUser($"ia_guid_{tag}", $"ia-guid-{tag}@example.test", "test-only", "Guy", $"Guid{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        var guidReference = $"DROPIN-{Guid.NewGuid():N}";
        var guidPayment = new Payment(guidPayer.Id, 12m, PaymentPlan.DropIn, session.Id) { Reference = guidReference, CreatedAt = DateTime.UtcNow.AddDays(-1), PaymentDate = DateTime.UtcNow.AddDays(-1) };

        // And before a payment row exists the page shows SHB-<player>-<session>.
        var fallbackPayer = new ApplicationUser($"ia_fb_{tag}", $"ia-fb-{tag}@example.test", "test-only", "Fabi", $"Back{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        var fallbackPayment = new Payment(fallbackPayer.Id, 14m, PaymentPlan.DropIn, session.Id) { Reference = $"DROPIN-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow.AddDays(-1), PaymentDate = DateTime.UtcNow.AddDays(-1) };

        await using (var context = db())
        {
            context.Users.AddRange(guidPayer, fallbackPayer);
            context.Payments.AddRange(guidPayment, fallbackPayment);
            await context.SaveChangesAsync();
        }

        IngestInteracEmailResultDto guidResult;
        await using (var context = db())
            guidResult = await Service(context).IngestAsync(new IngestInteracEmailDto
            {
                MessageId = $"msg-{tag}-guid",
                Subject = Subject($"Guy Guid{tag}", "12.00"),
                Body = Email($"Guy Guid{tag}", "12.00", guidReference, $"CAREF{tag}G", "May 28, 2026"),
                ReceivedAt = DateTimeOffset.UtcNow,
            });
        assert(guidResult.Status == InteracDepositStatus.Matched && guidResult.MatchedPaymentId == guidPayment.Id,
            "interac auto-match: the GUID reference the app hands out today is matched");

        var shb = $"SHB-{fallbackPayer.Id.ToString("N")[..8]}-{session.Id.ToString("N")[..8]}";
        IngestInteracEmailResultDto fallbackResult;
        await using (var context = db())
            fallbackResult = await Service(context).IngestAsync(new IngestInteracEmailDto
            {
                MessageId = $"msg-{tag}-fb",
                Subject = Subject($"Fabi Back{tag}", "14.00"),
                Body = Email($"Fabi Back{tag}", "14.00", shb, $"CAREF{tag}F", "May 29, 2026"),
                ReceivedAt = DateTimeOffset.UtcNow,
            });
        assert(fallbackResult.Status == InteracDepositStatus.Matched && fallbackResult.MatchedPaymentId == fallbackPayment.Id,
            "interac auto-match: the SHB-player-session reference shown before a payment exists is matched");

        // The bank's own reference identifies one transfer, whatever else changes.
        IngestInteracEmailResultDto sameReference;
        await using (var context = db())
            sameReference = await Service(context).IngestAsync(new IngestInteracEmailDto
            {
                MessageId = $"msg-{tag}-guid-resent",
                Subject = Subject($"Guy Guid{tag}", "12.00"),
                // Same Interac reference, different day and message: still the same money.
                Body = Email($"Guy Guid{tag}", "12.00", null, $"CAREF{tag}G", "May 29, 2026"),
                ReceivedAt = DateTimeOffset.UtcNow.AddHours(2),
            });
        assert(sameReference.Outcome == "duplicate" && sameReference.DepositId == guidResult.DepositId,
            "interac auto-match: the bank reference alone identifies a repeat, even when the rest of the email differs");

        // Setting one aside.
        await using (var context = db())
        {
            await Service(context).IgnoreAsync(stranger.DepositId!.Value, "Not a session payment", null, "Admin Test");
        }
        await using (var context = db())
        {
            var deposit = await context.InteracDeposits.AsNoTracking().SingleAsync(d => d.Id == stranger.DepositId!.Value);
            assert(deposit.Status == InteracDepositStatus.Ignored && deposit.Note == "Not a session payment",
                "interac auto-match: an admin can set a deposit aside with a reason");
        }
    }

    /// The deposit service only ever changes a payment's status; this stands in for the rest of IPaymentService.
    public class InteracFakePayments : DispatchProxy
    {
        public Func<ApplicationDbContext>? Db { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IPaymentService.UpdatePaymentStatusAsync))
            {
                var paymentId = (Guid)args![0]!;
                var status = (PaymentStatus)args[1]!;
                using var context = Db!();
                var payment = context.Payments.Single(p => p.Id == paymentId);
                payment.Status = status;
                context.SaveChanges();
                return Task.FromResult<PaymentDto>(null!);
            }
            throw new NotSupportedException($"Unexpected payment call: {targetMethod?.Name}");
        }
    }
}
