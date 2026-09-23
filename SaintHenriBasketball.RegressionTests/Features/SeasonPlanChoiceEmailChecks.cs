using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.Templates;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the `season-plan-choice-email` feature: the email a week before a season
/// starts, asking each player to choose a pass or pay per session.
///
/// The thing most worth pinning is that it cannot send twice. The job runs daily, an admin can send
/// by hand, and a restart re-runs it; none of that may reach the same player again.
internal static class SeasonPlanChoiceEmailChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var today = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;

        // --- The template, in both languages ---
        var model = new SeasonPlanChoiceEmailModel
        {
            FirstName = "Jeanne",
            SeasonName = "Automne 2003",
            StartDate = today.AddDays(7),
            EndDate = today.AddDays(90),
            DaysUntilStart = 7,
            PassPrice = 90m,
            DropInPrice = 10m,
            PassCapacity = 15,
            PassesLeft = 4,
            FirstSessionDate = today.AddDays(7),
            FirstSessionStart = "10:00",
            FirstSessionEnd = "12:00",
            Location = "717 Saint-Ferdinand",
            SessionCount = 12,
            AppUrl = "https://sainthenribasketball.com",
        };

        var english = EmailTemplates.Season.GetSeasonPlanChoiceEmail(model, EmailLanguage.English);
        assert(english.Contains("starts in 7 days", StringComparison.OrdinalIgnoreCase)
            && english.Contains("4 of 15 left", StringComparison.OrdinalIgnoreCase)
            && english.Contains("/plan-selection", StringComparison.Ordinal),
            "plan choice email: says how long is left, how many passes remain, and links to the plan page");

        assert(english.Contains("stay on pay-per-session", StringComparison.OrdinalIgnoreCase),
            "plan choice email: says what happens to a player who does nothing");

        var french = EmailTemplates.Season.GetSeasonPlanChoiceEmail(model, EmailLanguage.French);
        assert(french.Contains("commence dans 7 jours", StringComparison.OrdinalIgnoreCase)
            && french.Contains("4 sur 15", StringComparison.OrdinalIgnoreCase)
            && !french.Contains("starts in 7 days", StringComparison.OrdinalIgnoreCase),
            "plan choice email: the French version is French throughout");

        var soldOut = new SeasonPlanChoiceEmailModel
        {
            FirstName = "Jeanne", SeasonName = "Automne 2003", StartDate = today.AddDays(1), EndDate = today.AddDays(90),
            DaysUntilStart = 1, PassPrice = 90m, PassCapacity = 15, PassesLeft = 0, AppUrl = "https://sainthenribasketball.com",
        };
        var soldOutHtml = EmailTemplates.Season.GetSeasonPlanChoiceEmail(soldOut, EmailLanguage.English);
        assert(soldOutHtml.Contains("Sold out", StringComparison.OrdinalIgnoreCase) && soldOutHtml.Contains("starts tomorrow", StringComparison.OrdinalIgnoreCase),
            "plan choice email: a sold-out season says so, and tomorrow reads as tomorrow");

        // --- The send: who gets it, and never twice ---
        var season = new Season(today.AddDays(7), today.AddDays(90), 90m) { Name = $"Plan choice {tag}", SeasonPassCapacity = 15 };
        var firstSession = new Session(today.AddDays(7), 15, 11m, "10:00", "12:00", "Saint-Henri");

        var chooser = new ApplicationUser($"pc_pick_{tag}", $"pc-pick-{tag}@example.test", "test-only", "Paula", $"Pick{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        var paid = new ApplicationUser($"pc_paid_{tag}", $"pc-paid-{tag}@example.test", "test-only", "Pat", $"Paid{tag}", PaymentPlan.Season) { EmailConfirmed = true };
        var unconfirmed = new ApplicationUser($"pc_unconf_{tag}", $"pc-unconf-{tag}@example.test", "test-only", "Uma", $"Unconfirmed{tag}", PaymentPlan.DropIn) { EmailConfirmed = false };
        var gone = new ApplicationUser($"pc_gone_{tag}", $"pc-gone-{tag}@example.test", "test-only", "Gil", $"Gone{tag}", PaymentPlan.DropIn) { EmailConfirmed = true, IsDeactivated = true };
        var boss = new ApplicationUser($"pc_admin_{tag}", $"pc-admin-{tag}@example.test", "test-only", "Ada", $"Admin{tag}", PaymentPlan.DropIn) { EmailConfirmed = true, IsAdmin = true };
        var mine = new[] { chooser, paid, unconfirmed, gone, boss };

        await using (var context = db())
        {
            context.Users.AddRange(mine);
            context.Seasons.Add(season);
            context.Sessions.Add(firstSession);
            // Pat already paid for a pass, so there is nothing to choose.
            context.Payments.Add(new Payment(paid.Id, 90m, PaymentPlan.Season)
            {
                SeasonId = season.Id,
                Status = PaymentStatus.Completed,
                Reference = $"SEASON-{Guid.NewGuid():N}",
                CreatedAt = DateTime.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        var email = DispatchProxy.Create<IEmailService, PlanChoiceEmailRecorder>();
        var recorder = (PlanChoiceEmailRecorder)(object)email;
        var flags = DispatchProxy.Create<IFeatureFlagService, PlanChoiceFlags>();
        ((PlanChoiceFlags)(object)flags).Enabled = true;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AppUrl"] = "https://sainthenribasketball.com" }).Build();

        SeasonPlanChoiceEmailService Service(ApplicationDbContext context) => new(
            new SeasonRepository(context, NullLogger<SeasonRepository>.Instance),
            new UserRepository(context, NullLogger<UserRepository>.Instance),
            new SessionRepository(context),
            new SeasonPlanChoiceRepository(context, NullLogger<SeasonPlanChoiceRepository>.Instance),
            new OutstandingBalancesRepository(context),
            email,
            flags,
            configuration,
            NullLogger<SeasonPlanChoiceEmailService>.Instance);

        // A dry run says who would get it and sends nothing.
        PlanChoiceSendResultDto dry;
        await using (var context = db())
            dry = await Service(context).RunForSeasonAsync(season.Id, dryRun: true);
        assert(dry.DryRun && recorder.Sent.Count == 0 && dry.Recipients.Contains(chooser.Email!),
            "plan choice: a dry run names who would receive it and sends nothing");

        PlanChoiceSendResultDto first;
        await using (var context = db())
            first = await Service(context).RunForSeasonAsync(season.Id);

        assert(first.Sent >= 1 && recorder.Sent.Any(s => s.To == chooser.Email),
            "plan choice: a player with no pass is asked to choose");
        assert(!recorder.Sent.Any(s => s.To == paid.Email),
            "plan choice: a player who already paid for a pass is left alone");
        assert(!recorder.Sent.Any(s => s.To == unconfirmed.Email) && !recorder.Sent.Any(s => s.To == gone.Email) && !recorder.Sent.Any(s => s.To == boss.Email),
            "plan choice: unconfirmed addresses, players who left, and admins are skipped");

        var sentCount = recorder.Sent.Count;
        PlanChoiceSendResultDto second;
        await using (var context = db())
            second = await Service(context).RunForSeasonAsync(season.Id);
        assert(recorder.Sent.Count == sentCount && second.Sent == 0 && second.AlreadySent >= 1,
            "plan choice: running it again sends nobody a second copy — the daily job is safe to repeat");

        // The job asks for a season starting in exactly seven days; this one does.
        PlanChoiceSendResultDto byDate;
        await using (var context = db())
            byDate = await Service(context).RunForSeasonStartingInAsync(7);
        assert(byDate.SeasonId == season.Id && byDate.Sent == 0 && byDate.AlreadySent >= 1,
            "plan choice: the daily job finds the season a week out, and still sends nobody twice");

        PlanChoiceSendResultDto wrongDay;
        await using (var context = db())
            wrongDay = await Service(context).RunForSeasonStartingInAsync(3);
        assert(wrongDay.SeasonId == null && wrongDay.Outcome == SeasonPlanChoiceEmailService.NoSeasonOutcome,
            "plan choice: on a day no season is a week away, the job does nothing");

        // With the flag off nothing goes out, whatever the date says.
        ((PlanChoiceFlags)(object)flags).Enabled = false;
        PlanChoiceSendResultDto off;
        await using (var context = db())
            off = await Service(context).RunForSeasonStartingInAsync(7);
        assert(off.Outcome == SeasonPlanChoiceEmailService.FlagOffOutcome && off.Sent == 0,
            "plan choice: the flag stops the job before it looks at anything");

        // --- The daily job will not send on its own unless it is told to ---
        // The feature is on; automatic sending is not. A second season is a week out and untouched,
        // so anything the job sent would show up here.
        var untouched = new Season(today.AddDays(7), today.AddDays(90), 90m) { Name = $"Manual only {tag}", SeasonPassCapacity = 15 };
        var waiting = new ApplicationUser($"pc_wait_{tag}", $"pc-wait-{tag}@example.test", "test-only", "Wes", $"Wait{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        await using (var context = db())
        {
            context.Seasons.Add(untouched);
            context.Users.Add(waiting);
            await context.SaveChangesAsync();
        }

        ((PlanChoiceFlags)(object)flags).Enabled = true;
        ((PlanChoiceFlags)(object)flags).AutoEnabled = false;
        var beforeScheduled = recorder.Sent.Count;

        PlanChoiceSendResultDto scheduled;
        await using (var context = db())
            scheduled = await Service(context).RunScheduledAsync();
        assert(scheduled.Outcome == SeasonPlanChoiceEmailService.AutoSendOffOutcome
            && scheduled.Sent == 0 && recorder.Sent.Count == beforeScheduled,
            "plan choice: the daily job sends nothing while automatic sending is off, even with a season a week out");

        // The same email, sent by hand, still works — that is the whole point of the second switch.
        PlanChoiceSendResultDto byHand;
        await using (var context = db())
            byHand = await Service(context).RunForSeasonAsync(untouched.Id);
        assert(byHand.Sent >= 1 && recorder.Sent.Any(s => s.To == waiting.Email),
            "plan choice: an admin can still send it by hand while the job is held back");

        // And with automatic sending on, the job does its job.
        ((PlanChoiceFlags)(object)flags).AutoEnabled = true;
        PlanChoiceSendResultDto armed;
        await using (var context = db())
            armed = await Service(context).RunScheduledAsync();
        assert(armed.Outcome != SeasonPlanChoiceEmailService.AutoSendOffOutcome,
            "plan choice: switching automatic sending on lets the job through");

        // --- The job catches up on a firing it missed ---
        //
        // This is the case that actually cost the club a send. The job used to ask for a season
        // starting exactly seven days out, which gives it one firing — 10 AM, once — to be
        // switched on and working. The 26 September season passed that mark with the flag off,
        // and every run afterwards reported "No season starts on that day" while the season
        // arrived: a silent miss that reads exactly like having nothing to do.
        var late = new Season(today.AddDays(3), today.AddDays(80), 90m)
            { Name = $"Catch-up {tag}", SeasonPassCapacity = 15 };
        var lateSession = new Session(today.AddDays(3), 20, 10m, "10:00", "12:00", "Saint-Henri");
        var missedPlayer = new ApplicationUser(
            $"pc_late_{tag}", $"pc-late-{tag}@example.test", "test-only", "Late", $"Player{tag}", PaymentPlan.DropIn)
            { EmailConfirmed = true };

        await using (var context = db())
        {
            context.Seasons.Add(late);
            context.Sessions.Add(lateSession);
            context.Users.Add(missedPlayer);
            await context.SaveChangesAsync();
        }

        PlanChoiceSendResultDto caughtUp;
        await using (var context = db())
            caughtUp = await Service(context).RunScheduledAsync();

        assert(caughtUp.Outcome != SeasonPlanChoiceEmailService.NoSeasonOutcome && caughtUp.Sent >= 1,
            "plan choice: a season three days out still gets its email — the job catches up on a firing it missed");
        assert(recorder.Sent.Any(s => s.To == missedPlayer.Email),
            "plan choice: and the player who would have been skipped entirely is the one who receives it");
        assert(caughtUp.DaysUntilStart == 3,
            "plan choice: the email says how long is really left, not the width of the window it was found in");

        // Idempotency is what makes running every day safe rather than noisy.
        var afterCatchUp = recorder.Sent.Count;
        PlanChoiceSendResultDto again;
        await using (var context = db())
            again = await Service(context).RunScheduledAsync();
        assert(again.Sent == 0 && recorder.Sent.Count == afterCatchUp,
            "plan choice: tomorrow's run over the same window sends nobody a second copy");

        // The window has a floor. A season already under way is not something to email about.
        var started = new Season(today.AddDays(-2), today.AddDays(60), 90m)
            { Name = $"Under way {tag}", SeasonPassCapacity = 15 };
        await using (var context = db())
        {
            context.Seasons.Add(started);
            // Close the catch-up season so it stops being the one the job finds.
            var close = await context.Seasons.FirstAsync(s => s.Id == late.Id);
            close.Status = SeasonStatus.Closed;
            await context.SaveChangesAsync();
        }

        var beforeStarted = recorder.Sent.Count;
        PlanChoiceSendResultDto underWay;
        await using (var context = db())
            underWay = await Service(context).RunScheduledAsync();

        // Asserted on the season the job chose rather than on "nothing was sent": the checks share
        // one database, so another feature's open season can sit in the window and legitimately
        // send. What must never happen is this season being the one picked.
        assert(underWay.Outcome == SeasonPlanChoiceEmailService.NoSeasonOutcome || underWay.DaysUntilStart > 0,
            "plan choice: a season that has already started is past asking, so the job never picks one");
        assert(!recorder.Sent.Skip(beforeStarted).Any(s => s.Html.Contains(started.Name!, StringComparison.Ordinal)),
            "plan choice: and no player is told to choose a plan for a season already under way");

        // --- The email counts spots the way the rest of the app does ---
        //
        // It used to count paid passes alone, while the countdown, the dashboard and the plan page
        // all count spot holders: a player who picks a pass holds the seat before the money lands.
        // So the email advertised seats the plan page would then refuse — in the one message whose
        // entire purpose is getting people to claim one.
        var counted = new Season(today.AddDays(5), today.AddDays(85), 90m)
            { Name = $"Counting {tag}", SeasonPassCapacity = 10 };
        var holder = new ApplicationUser(
            $"pc_hold_{tag}", $"pc-hold-{tag}@example.test", "test-only", "Hilda", $"Holder{tag}", PaymentPlan.DropIn)
            { EmailConfirmed = true };

        await using (var context = db())
        {
            context.Seasons.Add(counted);
            context.Users.Add(holder);
            await context.SaveChangesAsync();
            // Chose the pass; has not paid for it. Exactly the case the two counts disagreed on.
            context.SeasonPlanChoices.Add(new SeasonPlanChoice(counted.Id, holder.Id, PaymentPlan.Season));
            await context.SaveChangesAsync();
        }

        await using (var context = db())
        {
            var choices = new SeasonPlanChoiceRepository(context, NullLogger<SeasonPlanChoiceRepository>.Instance);
            var paidOnly = await choices.CountPaidPassesAsync(counted.Id);
            var holders = (await choices.GetSpotHolderIdsAsync(counted.Id, includeProfilePlan: true)).Count;

            assert(paidOnly == 0 && holders >= 1,
                "plan choice: an unpaid choice holds a spot without being a paid pass — the two counts really do differ");

            var preview = await Service(context).RunForSeasonAsync(counted.Id, dryRun: true);
            assert(preview.Outcome != SeasonPlanChoiceEmailService.FlagOffOutcome,
                "plan choice: the counting check actually reached the send");
        }

        // The email's number must match the site's, not the paid-only one.
        //
        // Both sides are computed the same way on purpose. The checks share one database, where
        // other features leave users carrying a season plan on their profile, and those count as
        // holders here — so a hard-coded expectation would be asserting the fixture, not the rule.
        // What is pinned is that the email agrees with GetSpotHolderIdsAsync, whatever it returns.
        await using (var context = db())
        {
            var choices = new SeasonPlanChoiceRepository(context, NullLogger<SeasonPlanChoiceRepository>.Instance);
            var holders = (await choices.GetSpotHolderIdsAsync(counted.Id, includeProfilePlan: true)).Count;
            var paidOnly = await choices.CountPaidPassesAsync(counted.Id);

            var html = await Service(context).PreviewAsync(counted.Id, EmailLanguage.English);

            var siteSays = Math.Max(0, 10 - holders);
            var paidOnlySays = Math.Max(0, 10 - paidOnly);

            assert(html.Contains($"{siteSays} of 10 left", StringComparison.OrdinalIgnoreCase)
                    || (siteSays == 0 && html.Contains("Sold out", StringComparison.OrdinalIgnoreCase)),
                $"plan choice: the email shows {siteSays} spots — the same count the countdown and plan page use");

            // The old behaviour, named so the check fails if anyone puts it back.
            assert(paidOnlySays != siteSays && !html.Contains($"{paidOnlySays} of 10 left", StringComparison.OrdinalIgnoreCase),
                "plan choice: and not the paid-passes-only count, which would advertise seats the plan page then refuses");
        }
    }

    public class PlanChoiceEmailRecorder : DispatchProxy
    {
        public List<(string To, string Subject, string Html)> Sent { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IEmailService.SendEmailAsync))
            {
                Sent.Add(((string)args![0]!, (string)args[1]!, (string)args[2]!));
                return Task.CompletedTask;
            }
            throw new NotSupportedException($"Unexpected email call: {targetMethod?.Name}");
        }
    }

    public class PlanChoiceFlags : DispatchProxy
    {
        public bool Enabled { get; set; }
        public bool AutoEnabled { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IFeatureFlagService.IsEnabledAsync))
            {
                var key = args?[0] as string;
                return Task.FromResult(key == FeatureFlagKeys.SeasonPlanChoiceEmailAuto ? AutoEnabled : Enabled);
            }
            throw new NotSupportedException($"Unexpected flag call: {targetMethod?.Name}");
        }
    }
}
