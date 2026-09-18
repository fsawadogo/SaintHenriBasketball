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

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IFeatureFlagService.IsEnabledAsync)) return Task.FromResult(Enabled);
            throw new NotSupportedException($"Unexpected flag call: {targetMethod?.Name}");
        }
    }
}
