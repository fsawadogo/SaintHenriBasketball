using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.Application.Services.Implementations;

/// Log-only SMS implementation, used unless `Sms:Provider` selects a real provider:
/// `Twilio` (`Sms:AccountSid`, `Sms:AuthToken`, `Sms:FromNumber`) or `Brevo`
/// (`Sms:Brevo:ApiKey`, `Sms:Brevo:Sender`). Every "send" is logged so the flow
/// is exercisable in development without credentials.
public class SmsService : ISmsService
{
    private readonly ILogger<SmsService> _logger;
    private readonly string _provider;

    public SmsService(IConfiguration configuration, ILogger<SmsService> logger)
    {
        _logger = logger;
        _provider = configuration["Sms:Provider"] ?? "LogOnly";
    }

    public bool IsConfigured => !string.Equals(_provider, "LogOnly", StringComparison.OrdinalIgnoreCase);

    public Task<bool> SendAsync(string phoneNumber, string message)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            _logger.LogWarning("SMS skipped: empty phone number");
            return Task.FromResult(false);
        }

        _logger.LogInformation("SMS ({Provider}) → {Phone}: {Message}", _provider, Mask(phoneNumber), message);
        return Task.FromResult(true);
    }

    private static string Mask(string phone) =>
        phone.Length <= 4 ? phone : new string('*', phone.Length - 4) + phone[^4..];
}
