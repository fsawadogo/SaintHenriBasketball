using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Who a Google token was issued to.
///
/// A Google access token is a bearer credential that is not bound to whoever receives it, so asking
/// userinfo "who is this?" proves nothing about who is asking: a token minted for any other OAuth
/// client, for the same person, answers identically. Only the audience settles it. Sign-in used to
/// skip that check entirely, which made any such token a way in.
internal static class GoogleLoginAudienceChecks
{
    private const string OurClientId = "shb-test-client.apps.googleusercontent.com";
    private const string SomeoneElse = "a-completely-different-app.apps.googleusercontent.com";

    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var member = new ApplicationUser($"gl_{tag}", $"gl-{tag}@example.test", "test-only", "Ana", $"Goog{tag}", PaymentPlan.DropIn)
        { EmailConfirmed = true };

        await using (var context = db())
        {
            context.Users.Add(member);
            await context.SaveChangesAsync();
        }

        static string TokenInfo(string aud, string email, string verified = "true") =>
            $$"""{"aud":"{{aud}}","email":"{{email}}","email_verified":"{{verified}}","expires_in":3599}""";

        const string UserInfo = """{"email":"ignored@example.test","given_name":"Ana","family_name":"Goog"}""";

        UserService Service(ApplicationDbContext context, StubHttpClientFactory http, string? clientId = OurClientId)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:ClientId"] = clientId,
                ["JwtSettings:Key"] = "google-audience-regression-key-not-used-by-the-running-app-0123456789",
                ["JwtSettings:Issuer"] = "shb-tests",
                ["JwtSettings:Audience"] = "shb-tests",
                ["JwtSettings:DurationInDays"] = "1",
            }).Build();

            var mapper = new AutoMapper.MapperConfiguration(
                cfg => cfg.AddProfile<SaintHenriBasketball.Application.Mapping.MappingProfile>(),
                NullLoggerFactory.Instance).CreateMapper();

            return new UserService(config, mapper,
                new UserRepository(context, NullLogger<UserRepository>.Instance), null!,
                NullLogger<UserService>.Instance, System.Reflection.DispatchProxy.Create<SaintHenriBasketball.Application.Services.Interfaces.IFeatureFlagService, AlwaysOffFlags>(), new ReferralRepository(context), http);
        }

        static async Task<Exception?> FailureAsync(Func<Task> action)
        {
            try { await action(); return null; }
            catch (Exception ex) { return ex; }
        }

        // --- A token issued to somebody else ---
        var foreign = new StubHttpClientFactory()
            .When("tokeninfo", TokenInfo(SomeoneElse, member.Email!))
            .When("userinfo", UserInfo);

        Exception? refused;
        await using (var context = db())
            refused = await FailureAsync(() => Service(context, foreign).GoogleLoginAsync("a-token-for-another-app"));

        assert(refused is ValidationException && refused.Message == UserService.InvalidGoogleTokenMessage,
            "google login: a token issued to a different app is refused — this used to sign you in as that member");

        // --- An address Google itself has not verified ---
        var unverified = new StubHttpClientFactory()
            .When("tokeninfo", TokenInfo(OurClientId, member.Email!, verified: "false"))
            .When("userinfo", UserInfo);

        Exception? unverifiedRefused;
        await using (var context = db())
            unverifiedRefused = await FailureAsync(() => Service(context, unverified).GoogleLoginAsync("token"));

        assert(unverifiedRefused is ValidationException,
            "google login: an address Google has not verified cannot be used to claim a member's account");

        // --- Nothing configured to check against ---
        var whatever = new StubHttpClientFactory()
            .When("tokeninfo", TokenInfo(OurClientId, member.Email!))
            .When("userinfo", UserInfo);

        Exception? unconfigured;
        await using (var context = db())
            unconfigured = await FailureAsync(() => Service(context, whatever, clientId: null).GoogleLoginAsync("token"));

        assert(unconfigured is ValidationException && unconfigured.Message == UserService.GoogleNotConfiguredMessage,
            "google login: with no client id configured it fails closed rather than accepting anything");
        assert(whatever.Requested.Count == 0,
            "google login: an unconfigured app does not even ask Google — there would be nothing to compare");

        // --- Our own token, for a verified address ---
        var ours = new StubHttpClientFactory()
            .When("tokeninfo", TokenInfo(OurClientId, member.Email!))
            .When("userinfo", UserInfo);

        Exception? accepted;
        await using (var context = db())
            accepted = await FailureAsync(() => Service(context, ours).GoogleLoginAsync("our-token"));

        assert(accepted is null,
            "google login: a token actually issued to this app, for a verified address, still signs in");
        assert(ours.Requested.Any(u => u.Contains("tokeninfo", StringComparison.OrdinalIgnoreCase)),
            "google login: the audience is checked before anything is trusted");
    }

    /// Feature flags are irrelevant to sign-in; nothing on this path reads one.
    public class AlwaysOffFlags : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(SaintHenriBasketball.Application.Services.Interfaces.IFeatureFlagService.IsEnabledAsync))
                return Task.FromResult(false);
            throw new NotSupportedException($"Unexpected flag call: {targetMethod?.Name}");
        }
    }
}
