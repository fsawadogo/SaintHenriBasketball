using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.Templates;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Booking confirmations: the email a player gets when they reserve a place, and the backfill for
/// places reserved before that email existed. Sessions sit far in the future so the shared database
/// cannot drag other checks' registrations into the backfill.
internal static class BookingConfirmationChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var soon = DateTime.UtcNow.Date.AddDays(400);

        // --- The template ---
        var dropIn = EmailTemplates.Sessions.GetBookingConfirmationEmail(
            "Nadia", soon, "10:00", "12:00", "717 Saint-Ferdinand",
            PaymentPlan.DropIn, 11m, "https://sainthenribasketball.com", EmailLanguage.English);
        assert(dropIn.Contains("$11", StringComparison.Ordinal) && dropIn.Contains("/my-sessions", StringComparison.Ordinal),
            "booking confirmation: a drop-in player is told what the session costs, and where their places are");
        assert(dropIn.Contains("Release your place", StringComparison.OrdinalIgnoreCase),
            "booking confirmation: it says how to give the place back, so a place nobody wants is not wasted");

        var seasonHolder = EmailTemplates.Sessions.GetBookingConfirmationEmail(
            "Nadia", soon, "10:00", "12:00", null,
            PaymentPlan.Season, 11m, "https://sainthenribasketball.com", EmailLanguage.English);
        assert(seasonHolder.Contains("Covered by your season pass", StringComparison.OrdinalIgnoreCase)
            && !seasonHolder.Contains("$11", StringComparison.Ordinal),
            "booking confirmation: a pass holder is not quoted a price their pass already covers");

        var french = EmailTemplates.Sessions.GetBookingConfirmationEmail(
            "Nadia", soon, "10:00", "12:00", null,
            PaymentPlan.DropIn, 11m, "https://sainthenribasketball.com", EmailLanguage.French);
        assert(french.Contains("Votre place est réservée", StringComparison.Ordinal)
            && !french.Contains("Your place is booked", StringComparison.Ordinal),
            "booking confirmation: the French version is French");

        // --- The backfill ---
        var session = new Session(soon, 20, 11m, "10:00", "12:00", "Saint-Henri");
        var past = new Session(DateTime.UtcNow.Date.AddDays(-400), 20, 11m, "10:00", "12:00", "Saint-Henri");

        var missed = new ApplicationUser($"bc_missed_{tag}", $"bc-missed-{tag}@example.test", "test-only", "Nadia", $"Missed{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        var alreadyHad = new ApplicationUser($"bc_had_{tag}", $"bc-had-{tag}@example.test", "test-only", "Omar", $"Had{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        var longAgo = new ApplicationUser($"bc_past_{tag}", $"bc-past-{tag}@example.test", "test-only", "Pia", $"Past{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };

        await using (var context = db())
        {
            context.Sessions.AddRange(session, past);
            context.Users.AddRange(missed, alreadyHad, longAgo);
            context.SessionRegistrations.Add(new SessionRegistration(missed.Id, session.Id, PaymentPlan.DropIn));
            context.SessionRegistrations.Add(new SessionRegistration(alreadyHad.Id, session.Id, PaymentPlan.DropIn)
            { ConfirmationSentOn = DateTime.UtcNow.AddDays(-1) });
            context.SessionRegistrations.Add(new SessionRegistration(longAgo.Id, past.Id, PaymentPlan.DropIn));
            await context.SaveChangesAsync();
        }

        var email = DispatchProxy.Create<IEmailService, BookingEmailRecorder>();
        var recorder = (BookingEmailRecorder)(object)email;
        var flags = DispatchProxy.Create<IFeatureFlagService, BookingFlags>();
        ((BookingFlags)(object)flags).Enabled = true;

        BookingConfirmationBackfillService Service(ApplicationDbContext context) => new(
            new SessionRegistrationRepository(context),
            email,
            flags,
            NullLogger<BookingConfirmationBackfillService>.Instance);

        BookingConfirmationBackfillResultDto dry;
        await using (var context = db())
            dry = await Service(context).RunAsync(session.Id, dryRun: true);
        assert(dry.DryRun && dry.Sent == 1 && recorder.Sent.Count == 0,
            "booking backfill: a dry run names who is owed a confirmation and sends nothing");
        assert(dry.Recipients.Any(r => r.Contains($"Missed{tag}", StringComparison.Ordinal)),
            "booking backfill: the player who never got one is the player named");

        BookingConfirmationBackfillResultDto sent;
        await using (var context = db())
            sent = await Service(context).RunAsync(session.Id);
        assert(sent.Sent == 1 && recorder.Sent.Count == 1 && recorder.Sent[0].User.Id == missed.Id,
            "booking backfill: only the player who never got one is emailed");
        assert(!recorder.Sent.Any(s => s.User.Id == alreadyHad.Id),
            "booking backfill: a player who already had their confirmation is left alone");

        BookingConfirmationBackfillResultDto again;
        await using (var context = db())
            again = await Service(context).RunAsync(session.Id);
        assert(recorder.Sent.Count == 1 && again.Sent == 0 && again.Outcome == BookingConfirmationBackfillService.NobodyOutcome,
            "booking backfill: running it twice sends nobody a second copy");

        // A session that has already happened needs no confirming.
        await using (var context = db())
        {
            var forPast = await Service(context).RunAsync(past.Id, dryRun: true);
            assert(forPast.Sent == 0 && forPast.Outcome == BookingConfirmationBackfillService.NobodyOutcome,
                "booking backfill: a session that is already over is left out");
        }

        ((BookingFlags)(object)flags).Enabled = false;
        await using (var context = db())
        {
            var off = await Service(context).RunAsync(null, dryRun: true);
            assert(off.Outcome == BookingConfirmationBackfillService.FlagOffOutcome && off.Sent == 0,
                "booking backfill: the flag stops it before it looks at anything");
        }

        // A failing send leaves the registration unstamped, so the next run retries exactly it.
        ((BookingFlags)(object)flags).Enabled = true;
        var retry = new ApplicationUser($"bc_retry_{tag}", $"bc-retry-{tag}@example.test", "test-only", "Remy", $"Retry{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        await using (var context = db())
        {
            context.Users.Add(retry);
            context.SessionRegistrations.Add(new SessionRegistration(retry.Id, session.Id, PaymentPlan.DropIn));
            await context.SaveChangesAsync();
        }

        recorder.Throw = true;
        await using (var context = db())
        {
            var failed = await Service(context).RunAsync(session.Id);
            assert(failed.Failed == 1 && failed.Sent == 0,
                "booking backfill: a send that fails is reported as failed, not as sent");
        }

        recorder.Throw = false;
        await using (var context = db())
        {
            var retried = await Service(context).RunAsync(session.Id);
            assert(retried.Sent == 1,
                "booking backfill: the one that failed is retried next time, because it was never stamped");
        }
    }

    public class BookingEmailRecorder : DispatchProxy
    {
        public List<(ApplicationUser User, Session Session)> Sent { get; } = new();
        public bool Throw { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IEmailService.SendBookingConfirmationAsync))
            {
                if (Throw) throw new InvalidOperationException("Resend is down");
                Sent.Add(((ApplicationUser)args![0]!, (Session)args[1]!));
                return Task.CompletedTask;
            }
            throw new NotSupportedException($"Unexpected email call: {targetMethod?.Name}");
        }
    }

    public class BookingFlags : DispatchProxy
    {
        public bool Enabled { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IFeatureFlagService.IsEnabledAsync)) return Task.FromResult(Enabled);
            throw new NotSupportedException($"Unexpected flag call: {targetMethod?.Name}");
        }
    }
}
