using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Whether an address belongs to a club member is not public.
///
/// Confirm-email and reset-password used to answer "Invalid email" for an address nobody has, and
/// "Invalid token" for one somebody does — which tells anyone who asks whether a given person plays
/// here. The forgot-password endpoint takes care never to leak this; these two undid it.
internal static class AuthEnumerationChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var member = new ApplicationUser($"en_{tag}", $"en-{tag}@example.test", "test-only", "Iris", $"Member{tag}", PaymentPlan.DropIn)
        {
            EmailConfirmed = false,
            EmailConfirmationToken = "the-real-token",
            PasswordResetToken = "the-real-reset",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1),
        };

        await using (var context = db())
        {
            context.Users.Add(member);
            await context.SaveChangesAsync();
        }

        UserService Service(ApplicationDbContext context)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppUrl"] = "https://sainthenribasketball.com",
                ["JwtSettings:Key"] = "enumeration-regression-key-not-used-by-the-running-app-0123456789",
                ["JwtSettings:Issuer"] = "shb-tests",
                ["JwtSettings:Audience"] = "shb-tests",
                ["JwtSettings:DurationInDays"] = "1",
            }).Build();

            var mapper = new AutoMapper.MapperConfiguration(
                cfg => cfg.AddProfile<SaintHenriBasketball.Application.Mapping.MappingProfile>(),
                NullLoggerFactory.Instance).CreateMapper();

            return new UserService(config, mapper,
                new UserRepository(context, NullLogger<UserRepository>.Instance),
                DispatchProxy.Create<IEmailService, EnumSilentEmail>(),
                NullLogger<UserService>.Instance,
                DispatchProxy.Create<IFeatureFlagService, EnumOffFlags>(),
                new ReferralRepository(context),
                new EmailSendBudgetRepository(context),
                new StubHttpClientFactory());
        }

        static async Task<string?> MessageAsync(Func<Task> action)
        {
            try { await action(); return null; }
            catch (Exception ex) { return ex.Message; }
        }

        // --- Confirming an email ---
        string? unknownAddress, wrongToken;
        await using (var context = db())
            unknownAddress = await MessageAsync(() => Service(context).ConfirmEmailAsync($"nobody-{tag}@example.test", "any-token"));
        await using (var context = db())
            wrongToken = await MessageAsync(() => Service(context).ConfirmEmailAsync(member.Email, "not-the-token"));

        assert(unknownAddress is not null && unknownAddress == wrongToken,
            "auth enumeration: confirm-email answers the same whether the address is a member's or nobody's");
        assert(unknownAddress == UserService.InvalidConfirmationMessage,
            "auth enumeration: and the answer says the link is bad, not which part of it");

        // The real token still works — closing the leak must not close the door.
        await using (var context = db())
        {
            var worked = await MessageAsync(() => Service(context).ConfirmEmailAsync(member.Email, "the-real-token"));
            assert(worked is null, "auth enumeration: the genuine confirmation link still confirms");
        }

        // --- Resetting a password ---
        var expired = new ApplicationUser($"en_exp_{tag}", $"en-exp-{tag}@example.test", "test-only", "Otto", $"Expired{tag}", PaymentPlan.DropIn)
        {
            EmailConfirmed = true,
            PasswordResetToken = "stale-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(-1),
        };
        await using (var context = db())
        {
            context.Users.Add(expired);
            await context.SaveChangesAsync();
        }

        static ResetPasswordDto Reset(string? email, string token) =>
            new() { Email = email!, Token = token, NewPassword = "N0t-A-Real-Password!" };

        string? unknownReset, badToken, staleToken;
        await using (var context = db())
            unknownReset = await MessageAsync(() => Service(context).ResetPasswordAsync(Reset($"nobody-{tag}@example.test", "any")));
        await using (var context = db())
            badToken = await MessageAsync(() => Service(context).ResetPasswordAsync(Reset(member.Email, "wrong")));
        await using (var context = db())
            staleToken = await MessageAsync(() => Service(context).ResetPasswordAsync(Reset(expired.Email, "stale-token")));

        assert(unknownReset is not null && unknownReset == badToken && badToken == staleToken,
            "auth enumeration: reset-password answers the same for an unknown address, a wrong token and an expired one");
        assert(unknownReset == UserService.InvalidResetLinkMessage,
            "auth enumeration: and it tells the reader to request a new link, which is what they need to do");

        await using (var context = db())
        {
            var worked = await MessageAsync(() => Service(context).ResetPasswordAsync(Reset(member.Email, "the-real-reset")));
            assert(worked is null, "auth enumeration: the genuine reset link still resets the password");
        }
    }

    public class EnumSilentEmail : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Task.CompletedTask;
    }

    public class EnumOffFlags : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IFeatureFlagService.IsEnabledAsync)) return Task.FromResult(false);
            throw new NotSupportedException($"Unexpected flag call: {targetMethod?.Name}");
        }
    }
}
