using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.Templates;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// The email a player gets back when they pick how they will pay for a season.
///
/// The two plans need opposite endings — a pass still has to be paid for, pay-per-session does not —
/// and neither may be sent twice for the same answer. Data sits in 2015.
internal static class PlanChoiceConfirmationEmailChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];

        // --- The template ---
        var model = new PlanChoiceConfirmationEmailModel
        {
            FirstName = "Ines",
            SeasonName = "Automne 2015",
            Plan = PaymentPlan.Season,
            StartDate = new DateTime(2015, 9, 1),
            EndDate = new DateTime(2015, 12, 31),
            PassPrice = 90m,
            DropInPrice = 10m,
            AlreadyPaid = false,
            SpotsLeft = 4,
            AppUrl = "https://sainthenribasketball.com",
        };

        var unpaid = EmailTemplates.Season.GetPlanChoiceConfirmationEmail(model, EmailLanguage.English);
        assert(unpaid.Contains("/season-subscription", StringComparison.Ordinal)
            && unpaid.Contains("Awaiting payment", StringComparison.OrdinalIgnoreCase),
            "plan confirmation: an unpaid pass says it is unpaid and offers a way to pay");
        assert(unpaid.Contains("held but not confirmed", StringComparison.OrdinalIgnoreCase),
            "plan confirmation: an unpaid pass says plainly that the spot is not secured yet");

        var paidModel = new PlanChoiceConfirmationEmailModel
        {
            FirstName = "Ines", SeasonName = "Automne 2015", Plan = PaymentPlan.Season,
            StartDate = new DateTime(2015, 9, 1), EndDate = new DateTime(2015, 12, 31),
            PassPrice = 90m, AlreadyPaid = true, SpotsLeft = 3, AppUrl = "https://sainthenribasketball.com",
        };
        var paidHtml = EmailTemplates.Season.GetPlanChoiceConfirmationEmail(paidModel, EmailLanguage.English);
        assert(!paidHtml.Contains("/season-subscription", StringComparison.Ordinal)
            && !paidHtml.Contains("held but not confirmed", StringComparison.OrdinalIgnoreCase),
            "plan confirmation: a player who already paid is never asked for money again");
        assert(paidHtml.Contains("covered", StringComparison.OrdinalIgnoreCase),
            "plan confirmation: a paid pass says the season is covered");

        var dropModel = new PlanChoiceConfirmationEmailModel
        {
            FirstName = "Ines", SeasonName = "Automne 2015", Plan = PaymentPlan.DropIn,
            StartDate = new DateTime(2015, 9, 1), EndDate = new DateTime(2015, 12, 31),
            PassPrice = 90m, DropInPrice = 10m, SpotsLeft = 4, AppUrl = "https://sainthenribasketball.com",
        };
        var dropHtml = EmailTemplates.Season.GetPlanChoiceConfirmationEmail(dropModel, EmailLanguage.English);
        assert(dropHtml.Contains("Nothing to pay up front", StringComparison.OrdinalIgnoreCase)
            && !dropHtml.Contains("Awaiting payment", StringComparison.OrdinalIgnoreCase),
            "plan confirmation: pay-per-session asks for nothing and says so");
        assert(dropHtml.Contains("4 are still available", StringComparison.OrdinalIgnoreCase),
            "plan confirmation: pay-per-session mentions the pass is still an option, with what is left");

        var soldOut = EmailTemplates.Season.GetPlanChoiceConfirmationEmail(
            new PlanChoiceConfirmationEmailModel
            {
                FirstName = "Ines", SeasonName = "Automne 2015", Plan = PaymentPlan.DropIn,
                StartDate = new DateTime(2015, 9, 1), EndDate = new DateTime(2015, 12, 31),
                PassPrice = 90m, SpotsLeft = 0, AppUrl = "https://sainthenribasketball.com",
            }, EmailLanguage.English);
        assert(soldOut.Contains("sold out", StringComparison.OrdinalIgnoreCase),
            "plan confirmation: with no passes left it does not dangle one");

        var french = EmailTemplates.Season.GetPlanChoiceConfirmationEmail(model, EmailLanguage.French);
        assert(french.Contains("laissez-passer", StringComparison.OrdinalIgnoreCase)
            && !french.Contains("Awaiting payment", StringComparison.OrdinalIgnoreCase),
            "plan confirmation: the French version is French");

        // --- The send ---
        // ChooseAsync always works on whichever season is Open, which this shared database already
        // has; borrow it rather than adding a second one that would fight it for "current".
        Season season;
        int originalCapacity;
        await using (var context = db())
        {
            season = await new SeasonRepository(context, NullLogger<SeasonRepository>.Instance).GetCurrentSeasonAsync()
                ?? throw new InvalidOperationException("No open season for the plan confirmation checks to use.");
            originalCapacity = season.SeasonPassCapacity;

            // Spot counting includes every profile still set to the season plan, and other checks
            // create plenty of those; without headroom this season reads as sold out.
            var tracked = await context.Seasons.FindAsync(season.Id);
            tracked!.SeasonPassCapacity = 100000;
            await context.SaveChangesAsync();
        }

        var player = new ApplicationUser($"pc_conf_{tag}", $"pc-conf-{tag}@example.test", "test-only", "Ines", $"Conf{tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        await using (var context = db())
        {
            context.Users.Add(player);
            await context.SaveChangesAsync();
        }

        var email = DispatchProxy.Create<IEmailService, ConfirmEmailRecorder>();
        var recorder = (ConfirmEmailRecorder)(object)email;
        var flags = DispatchProxy.Create<IFeatureFlagService, ConfirmFlags>();
        ((ConfirmFlags)(object)flags).Enabled = true;

        SeasonPlanService Service(ApplicationDbContext context) => new(
            new SeasonRepository(context, NullLogger<SeasonRepository>.Instance),
            new SeasonPlanChoiceRepository(context, NullLogger<SeasonPlanChoiceRepository>.Instance),
            new UserRepository(context, NullLogger<UserRepository>.Instance),
            new AuditLogService(new AuditLogRepository(context)),
            email,
            flags,
            new SessionRepository(context),
            NullLogger<SeasonPlanService>.Instance);

        await using (var context = db())
            await Service(context).ChooseAsync(player.Id, PaymentPlan.Season);
        assert(recorder.Sent.Count == 1 && recorder.Sent[0].Model.Plan == PaymentPlan.Season,
            "plan confirmation: choosing a plan emails the player who chose it");
        assert(recorder.Sent[0].Model.SeasonName == season.Name && !recorder.Sent[0].Model.AlreadyPaid,
            "plan confirmation: the email names the season and knows the pass is not paid for");

        await using (var context = db())
            await Service(context).ChooseAsync(player.Id, PaymentPlan.Season);
        assert(recorder.Sent.Count == 1,
            "plan confirmation: re-saving the same plan sends nothing — only a change is worth an email");

        await using (var context = db())
            await Service(context).ChooseAsync(player.Id, PaymentPlan.DropIn);
        assert(recorder.Sent.Count == 2 && recorder.Sent[1].Model.Plan == PaymentPlan.DropIn,
            "plan confirmation: changing plan is confirmed again, with the new plan");

        ((ConfirmFlags)(object)flags).Enabled = false;
        await using (var context = db())
            await Service(context).ChooseAsync(player.Id, PaymentPlan.Season);
        assert(recorder.Sent.Count == 2,
            "plan confirmation: the flag stops the email");

        // The choice must survive a mail failure: the plan is already saved when the email is sent.
        ((ConfirmFlags)(object)flags).Enabled = true;
        recorder.Throw = true;
        await using (var context = db())
            await Service(context).ChooseAsync(player.Id, PaymentPlan.DropIn);
        recorder.Throw = false;

        await using (var check = db())
        {
            var saved = await new SeasonPlanChoiceRepository(check, NullLogger<SeasonPlanChoiceRepository>.Instance)
                .GetAsync(season.Id, player.Id);
            assert(saved?.Plan == PaymentPlan.DropIn,
                "plan confirmation: a failing email never undoes the plan the player just chose");
        }

        // Put the borrowed season back exactly as it was, for every check that runs after this one.
        await using (var context = db())
        {
            var tracked = await context.Seasons.FindAsync(season.Id);
            if (tracked is not null)
            {
                tracked.SeasonPassCapacity = originalCapacity;
                await context.SaveChangesAsync();
            }
        }
    }

    public class ConfirmEmailRecorder : DispatchProxy
    {
        public List<(ApplicationUser User, PlanChoiceConfirmationEmailModel Model)> Sent { get; } = new();
        public bool Throw { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IEmailService.SendPlanChoiceConfirmationAsync))
            {
                if (Throw) throw new InvalidOperationException("Resend is down");
                Sent.Add(((ApplicationUser)args![0]!, (PlanChoiceConfirmationEmailModel)args[1]!));
                return Task.CompletedTask;
            }
            throw new NotSupportedException($"Unexpected email call: {targetMethod?.Name}");
        }
    }

    public class ConfirmFlags : DispatchProxy
    {
        public bool Enabled { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IFeatureFlagService.IsEnabledAsync)) return Task.FromResult(Enabled);
            throw new NotSupportedException($"Unexpected flag call: {targetMethod?.Name}");
        }
    }
}
