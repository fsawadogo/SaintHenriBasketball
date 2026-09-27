using System.Reflection;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Mapping;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

internal static class SeasonDropInBillingChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var billingDay = new DateTime(2052, 2, 10);
        var season = new Season(billingDay.AddDays(-7), billingDay.AddDays(7), 95m)
        {
            Name = $"Billing choice {tag}",
            Status = SeasonStatus.Open
        };
        var session = new Session(billingDay, 20, 10m, "10:00", "12:00", $"Billing choice court {tag}");
        var seasonProfile = new ApplicationUser($"season-billing-{tag}", $"season-billing-{tag}@example.test", "test-only", "Season", "Player", PaymentPlan.Season)
        {
            EmailConfirmed = true
        };
        var explicitDropIn = new ApplicationUser($"dropin-billing-{tag}", $"dropin-billing-{tag}@example.test", "test-only", "Drop-in", "Player", PaymentPlan.Season)
        {
            EmailConfirmed = true
        };

        await using (var context = db())
        {
            context.Seasons.Add(season);
            context.Sessions.Add(session);
            context.Users.AddRange(seasonProfile, explicitDropIn);
            context.SessionRegistrations.AddRange(
                new SessionRegistration(seasonProfile.Id, session.Id, PaymentPlan.Season),
                new SessionRegistration(explicitDropIn.Id, session.Id, PaymentPlan.DropIn));
            context.SeasonPlanChoices.Add(new SeasonPlanChoice(season.Id, explicitDropIn.Id, PaymentPlan.DropIn));
            await context.SaveChangesAsync();
        }

        var flags = DispatchProxy.Create<IFeatureFlagService, EnabledFlags>();
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppUrl"] = "http://localhost",
            ["Referrals:RewardAmount"] = "10.00"
        }).Build();

        await using (var context = db())
        {
            var users = new UserRepository(context, NullLogger<UserRepository>.Instance);
            var notifications = new NotificationService(new NotificationRepository(context), users, NullLogger<NotificationService>.Instance);
            var referrals = new ReferralService(new ReferralRepository(context), users, NullLogger<ReferralService>.Instance,
                new PaymentRepository(context), flags, notifications, new AuditLogRepository(context), config);
            var service = new PaymentService(
                new PaymentRepository(context), users, new SessionRepository(context), new SessionRegistrationRepository(context), mapper,
                NullLogger<PaymentService>.Instance, null!, notifications,
                new SeasonRepository(context, NullLogger<SeasonRepository>.Instance), new PromoCodeRepository(context),
                new AccountCreditRepository(context), referrals, flags,
                new SeasonPlanChoiceRepository(context, NullLogger<SeasonPlanChoiceRepository>.Instance));

            await service.RunDropInBillingAsync(SessionTimeHelper.ToUtc(billingDay.AddHours(11)));
        }

        await using (var context = db())
        {
            assert(!context.Payments.Any(p => p.UserId == seasonProfile.Id && p.SessionId == session.Id),
                "billing plan: a Season profile without a per-season choice is not charged a drop-in fee");
            assert(context.Payments.Any(p => p.UserId == explicitDropIn.Id && p.SessionId == session.Id && p.Plan == PaymentPlan.DropIn),
                "billing plan: an explicit Drop-in choice is charged even when the profile plan is Season");
        }
    }

    public class EnabledFlags : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IFeatureFlagService.IsEnabledAsync))
                return Task.FromResult(true);
            throw new InvalidOperationException($"Unexpected feature flag call: {targetMethod?.Name}");
        }
    }
}