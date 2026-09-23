using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.Services.Implementations;

internal static class RegistrationChallengeChecks
{
    public static async Task RunAsync(Action<bool, string> assert)
    {
        static TurnstileRegistrationChallengeService Service(
            string? secret,
            HttpMessageHandler handler,
            string hostname = "sainthenribasketball.com")
        {
            var values = new Dictionary<string, string?>
            {
                ["Turnstile:SecretKey"] = secret,
                ["Turnstile:Hostname"] = hostname,
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            return new TurnstileRegistrationChallengeService(
                new HttpClient(handler),
                configuration,
                NullLogger<TurnstileRegistrationChallengeService>.Instance);
        }

        var unused = new StubHandler(HttpStatusCode.InternalServerError, "{}");
        var disabled = Service(null, unused);
        assert(await disabled.VerifyAsync(null, "127.0.0.1"),
            "registration challenge: local development works when no secret is configured");
        assert(unused.Calls == 0,
            "registration challenge: disabled verification makes no external request");

        var success = new StubHandler(
            HttpStatusCode.OK,
            """{"success":true,"hostname":"sainthenribasketball.com","action":"register"}""");
        assert(await Service("test-secret", success).VerifyAsync("valid-token", "203.0.113.10"),
            "registration challenge: a valid register token for the production hostname passes");
        assert(success.Calls == 1 && success.LastBody?.Contains("response=valid-token", StringComparison.Ordinal) == true,
            "registration challenge: the token is verified server-side");

        var wrongAction = new StubHandler(
            HttpStatusCode.OK,
            """{"success":true,"hostname":"sainthenribasketball.com","action":"login"}""");
        assert(!await Service("test-secret", wrongAction).VerifyAsync("valid-token", null),
            "registration challenge: a token minted for another action cannot register");

        var wrongHost = new StubHandler(
            HttpStatusCode.OK,
            """{"success":true,"hostname":"attacker.example","action":"register"}""");
        assert(!await Service("test-secret", wrongHost).VerifyAsync("valid-token", null),
            "registration challenge: a token minted on another hostname cannot register");

        var missing = new StubHandler(HttpStatusCode.OK, """{"success":true}""");
        assert(!await Service("test-secret", missing).VerifyAsync("", null) && missing.Calls == 0,
            "registration challenge: a configured production service rejects a missing token before calling out");
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
