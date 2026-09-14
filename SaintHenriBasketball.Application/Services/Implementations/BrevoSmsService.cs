using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.Application.Services.Implementations;

/// Brevo transactional SMS. Select with `Sms:Provider = Brevo` and set `Sms:Brevo:ApiKey`
/// and `Sms:Brevo:Sender`. Delivery to Canada requires an approved Brevo toll-free number
/// registration; until then Brevo rejects or holds the message.
public class BrevoSmsService : ISmsService
{
    private const string SendUrl = "https://api.brevo.com/v3/transactionalSMS/sms";

    private readonly HttpClient _httpClient;
    private readonly ILogger<BrevoSmsService> _logger;
    private readonly string? _apiKey;
    private readonly string? _sender;

    public BrevoSmsService(HttpClient httpClient, IConfiguration configuration, ILogger<BrevoSmsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Sms:Brevo:ApiKey"];
        _sender = configuration["Sms:Brevo:Sender"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_sender);

    public async Task<bool> SendAsync(string phoneNumber, string message)
    {
        var recipient = ToRecipient(phoneNumber);
        if (recipient is null)
        {
            _logger.LogWarning("Brevo SMS skipped: phone number is missing or invalid");
            return false;
        }

        if (!IsConfigured)
        {
            _logger.LogWarning("Brevo SMS skipped: set Sms:Brevo:ApiKey and Sms:Brevo:Sender.");
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, SendUrl)
            {
                Content = JsonContent.Create(new
                {
                    sender = _sender,
                    recipient,
                    content = message,
                    type = "transactional",
                    tag = "shb-reminder",
                    unicodeEnabled = SmsEncoding.RequiresUnicode(message),
                }),
            };
            request.Headers.Add("api-key", _apiKey);

            using var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<BrevoSendResult>();
                _logger.LogInformation("Brevo SMS accepted messageId={MessageId} credits={Credits} to {Phone}",
                    result?.MessageId, result?.UsedCredits, Mask(recipient));
                return true;
            }

            // Body can explain the rejection (e.g. unregistered sender, insufficient credits); it holds no message content.
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Brevo SMS rejected status={Status} to {Phone}: {Error}",
                (int)response.StatusCode, Mask(recipient), error.Length > 500 ? error[..500] : error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Brevo SMS send failed for {Phone}", Mask(recipient));
            return false;
        }
    }

    /// Brevo expects digits with the country code. Ten-digit North American numbers get a leading 1.
    public static string? ToRecipient(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return null;
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) digits = "1" + digits;
        return digits.Length is >= 11 and <= 15 ? digits : null;
    }

    private static string Mask(string phone) =>
        phone.Length <= 4 ? phone : new string('*', phone.Length - 4) + phone[^4..];

    private sealed record BrevoSendResult(long? MessageId, double? UsedCredits);
}
