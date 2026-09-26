using System.Reflection;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Mapping;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

internal static class SeasonCoveredDropInChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var season = new Season(new DateTime(2051, 1, 1), new DateTime(2051, 4, 30), 95m)
        {
            Name = $"Coverage {tag}",
            Status = SeasonStatus.Closed
        };
        var coveredSession = new Session(new DateTime(2051, 2, 8), 20, 10m, "10:00", "12:00", "Coverage court");
        var outsideSession = new Session(new DateTime(2051, 6, 7), 20, 10m, "10:00", "12:00", "Coverage court");
        var player = new ApplicationUser($"coverage_{tag}", $"coverage-{tag}@example.test", "test-only", "Season", "Player", PaymentPlan.Season)
        {
            EmailConfirmed = true
        };
        var coveredPending = new Payment(player.Id, 10m, PaymentPlan.DropIn, coveredSession.Id)
        {
            Reference = $"DROPIN-COVERED-{tag}",
            CreatedAt = DateTime.UtcNow
        };
        var completedHistory = new Payment(player.Id, 10m, PaymentPlan.DropIn, coveredSession.Id)
        {
            Status = PaymentStatus.Completed,
            Reference = $"DROPIN-PAID-{tag}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        var outsidePending = new Payment(player.Id, 10m, PaymentPlan.DropIn, outsideSession.Id)
        {
            Reference = $"DROPIN-OUTSIDE-{tag}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        await using var context = db();
        context.Seasons.Add(season);
        context.Sessions.AddRange(coveredSession, outsideSession);
        context.Users.Add(player);
        context.Payments.AddRange(coveredPending, completedHistory, outsidePending);
        await context.SaveChangesAsync();
        var choices = new SeasonPlanChoiceRepository(context, NullLogger<SeasonPlanChoiceRepository>.Instance);
        await choices.UpsertAsync(season.Id, player.Id, PaymentPlan.Season);

        var flags = DispatchProxy.Create<IFeatureFlagService, CoverageFlags>();
        ((CoverageFlags)(object)flags).Enabled = true;
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var service = new PaymentService(
            new PaymentRepository(context),
            new UserRepository(context, NullLogger<UserRepository>.Instance),
            new SessionRepository(context),
            null!,
            mapper,
            NullLogger<PaymentService>.Instance,
            null!,
            null!,
            new SeasonRepository(context, NullLogger<SeasonRepository>.Instance),
            null!,
            null!,
            null!,
            flags,
            choices);

        var visible = (await service.GetUserPaymentsAsync(player.Id)).ToList();
        assert(!visible.Any(p => p.Id == coveredPending.Id),
            "season coverage: a pending drop-in inside the chosen season is not presented as money owed");
        assert(visible.Any(p => p.Id == completedHistory.Id),
            "season coverage: completed drop-in history is preserved for receipts and audit");
        assert(visible.Any(p => p.Id == outsidePending.Id),
            "season coverage: a drop-in outside the chosen season remains payable");

        await choices.UpsertAsync(season.Id, player.Id, PaymentPlan.DropIn);
        visible = (await service.GetUserPaymentsAsync(player.Id)).ToList();
        assert(visible.Any(p => p.Id == coveredPending.Id),
            "season coverage: the same pending charge remains payable for a Drop-in choice");
    }

    public class CoverageFlags : DispatchProxy
    {
        public bool Enabled { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IFeatureFlagService.IsEnabledAsync))
                return Task.FromResult(Enabled);
            throw new InvalidOperationException($"Unexpected feature flag call: {targetMethod?.Name}");
        }
    }
}