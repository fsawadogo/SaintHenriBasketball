using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Unsticking a signup that never followed its confirmation link.
///
/// Such a player cannot sign in, and until now an admin had no way to help: confirming was only
/// possible by the player clicking their own link. Two ways out — resend, or confirm on their
/// behalf — and neither may quietly leave a usable old link behind.
internal static class AdminConfirmEmailChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var email = System.Reflection.DispatchProxy.Create<IEmailService, ConfirmEmailRecorder>();
        var recorder = (ConfirmEmailRecorder)(object)email;
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AppUrl"] = "https://sainthenribasketball.com" }).Build();

        AccountLifecycleService Service(ApplicationDbContext context) => new(
            new UserRepository(context, NullLogger<UserRepository>.Instance),
            new SessionRegistrationRepository(context),
            new ParticipationRepository(context),
            new NoCache(),
            email,
            config,
            NullLogger<AccountLifecycleService>.Instance);

        var stuck = new ApplicationUser($"ce_stuck_{tag}", $"ce-stuck-{tag}@example.test", "test-only", "Ismael", $"Stuck{tag}", PaymentPlan.DropIn)
        { EmailConfirmed = false, EmailConfirmationToken = "original-token" };
        var already = new ApplicationUser($"ce_ok_{tag}", $"ce-ok-{tag}@example.test", "test-only", "Ada", $"Fine{tag}", PaymentPlan.DropIn)
        { EmailConfirmed = true };

        await using (var context = db())
        {
            context.Users.AddRange(stuck, already);
            await context.SaveChangesAsync();
        }

        // --- Resend ---
        bool resent;
        await using (var context = db()) resent = await Service(context).ResendConfirmationAsync(stuck.Id);
        assert(resent && recorder.Sent.Count == 1 && recorder.Sent[0].To == stuck.Email,
            "confirm email: resending sends a confirmation link to the player who never confirmed");

        string? issuedToken;
        await using (var context = db())
            issuedToken = (await new UserRepository(context, NullLogger<UserRepository>.Instance).GetByIdAsync(stuck.Id))?.EmailConfirmationToken;

        assert(!string.IsNullOrWhiteSpace(issuedToken) && issuedToken != "original-token",
            "confirm email: a resend issues a fresh token, so a link that leaked earlier stops working");
        assert(recorder.Sent[0].Link.Contains(issuedToken!, StringComparison.Ordinal)
            && recorder.Sent[0].Link.Contains("/confirm-email?", StringComparison.Ordinal),
            "confirm email: the link carries the token that was just issued");

        bool resentAgain;
        await using (var context = db()) resentAgain = await Service(context).ResendConfirmationAsync(already.Id);
        assert(!resentAgain && recorder.Sent.Count == 1,
            "confirm email: there is nothing to resend to someone already confirmed, so nothing is sent");

        // --- Confirm on their behalf ---
        bool confirmed;
        await using (var context = db()) confirmed = await Service(context).ConfirmEmailAsync(stuck.Id);
        assert(confirmed, "confirm email: an admin can confirm the address for a player who cannot");

        await using (var context = db())
        {
            var after = await new UserRepository(context, NullLogger<UserRepository>.Instance).GetByIdAsync(stuck.Id);
            assert(after?.EmailConfirmed == true,
                "confirm email: the player is confirmed afterwards, and can sign in");
            assert(string.IsNullOrEmpty(after?.EmailConfirmationToken),
                "confirm email: the outstanding token is spent, so the old link cannot confirm again later");
        }

        bool confirmedTwice;
        await using (var context = db()) confirmedTwice = await Service(context).ConfirmEmailAsync(stuck.Id);
        assert(!confirmedTwice,
            "confirm email: confirming an already-confirmed player reports that nothing changed");

        // --- Unknown player ---
        var missing = false;
        try
        {
            await using var context = db();
            await Service(context).ConfirmEmailAsync(Guid.NewGuid());
        }
        catch (NotFoundException) { missing = true; }
        assert(missing, "confirm email: confirming someone who does not exist is an error, not a silent success");
    }

    public class ConfirmEmailRecorder : System.Reflection.DispatchProxy
    {
        public List<(string To, string Link)> Sent { get; } = new();

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IEmailService.SendConfirmationEmailAsync))
            {
                Sent.Add(((string)args![0]!, (string)args[1]!));
                return Task.CompletedTask;
            }
            throw new NotSupportedException($"Unexpected email call: {targetMethod?.Name}");
        }
    }

    /// The lifecycle service only clears cache entries; nothing here reads them back.
    private sealed class NoCache : ICacheService
    {
        public Task<T?> GetAsync<T>(string key) => Task.FromResult(default(T));
        public Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null) => Task.CompletedTask;
        public Task RemoveAsync(string key) => Task.CompletedTask;
        public Task RemoveByPrefixAsync(string prefix) => Task.CompletedTask;
    }
}
