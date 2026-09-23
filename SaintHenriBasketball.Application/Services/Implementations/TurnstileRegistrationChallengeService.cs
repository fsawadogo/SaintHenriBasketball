using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.Application.Services.Implementations;

public sealed class TurnstileRegistrationChallengeService : IRegistrationChallengeService
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    private const string ExpectedAction = "register";
    private readonly HttpClient _http;
    private readonly ILogger<TurnstileRegistrationChallengeService> _logger;
    private readonly string? _secret;
    private readonly string? _expectedHostname;

    public TurnstileRegistrationChallengeService(HttpClient http, IConfiguration configuration, ILogger<TurnstileRegistrationChallengeService> logger)
    {
        _http = http;
        _logger = logger;
        _secret = configuration["Turnstile:SecretKey"]?.Trim();
        _expectedHostname = configuration["Turnstile:Hostname"]?.Trim();
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_secret);

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return true;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return false;

        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = _secret!,
                ["response"] = token,
                ["remoteip"] = remoteIp ?? string.Empty,
            });
            using var response = await _http.PostAsync(VerifyUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile verification returned HTTP {StatusCode}", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken: cancellationToken);
            return result?.Success == true
                && string.Equals(result.Action, ExpectedAction, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(_expectedHostname)
                    || string.Equals(result.Hostname, _expectedHostname, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Turnstile verification was unavailable");
            return false;
        }
    }

    private sealed class TurnstileResponse
    {
        [JsonPropertyName("success")] public bool Success { get; init; }
        [JsonPropertyName("hostname")] public string? Hostname { get; init; }
        [JsonPropertyName("action")] public string? Action { get; init; }
    }
}
