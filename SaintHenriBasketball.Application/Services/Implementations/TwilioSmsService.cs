using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.Services.Interfaces;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class TwilioSmsService : ISmsService
{
    private readonly ILogger<TwilioSmsService> _logger;
    private readonly string? _accountSid;
    private readonly string? _authToken;
    private readonly string? _fromNumber;
    private readonly string? _messagingServiceSid;
    private bool _initialized;

    public TwilioSmsService(IConfiguration configuration, ILogger<TwilioSmsService> logger)
    {
        _logger = logger;
        _accountSid = configuration["Sms:AccountSid"];
        _authToken = configuration["Sms:AuthToken"];
        _fromNumber = configuration["Sms:FromNumber"];
        _messagingServiceSid = configuration["Sms:MessagingServiceSid"];
    }

    public bool IsConfigured =>
        !string.IsNullOrEmpty(_accountSid)
        && !string.IsNullOrEmpty(_authToken)
        && (!string.IsNullOrEmpty(_fromNumber) || !string.IsNullOrEmpty(_messagingServiceSid));

    public async Task<bool> SendAsync(string phoneNumber, string message)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            _logger.LogWarning("SMS skipped: empty phone number");
            return false;
        }

        if (!IsConfigured)
        {
            _logger.LogWarning("Twilio SMS skipped: credentials or sender not configured. Set Sms:AccountSid, Sms:AuthToken, and Sms:FromNumber (or Sms:MessagingServiceSid).");
            return false;
        }

        EnsureClientInitialized();

        try
        {
            var options = new CreateMessageOptions(new PhoneNumber(phoneNumber))
            {
                Body = message,
            };
            if (!string.IsNullOrEmpty(_messagingServiceSid))
                options.MessagingServiceSid = _messagingServiceSid;
            else
                options.From = new PhoneNumber(_fromNumber);

            var result = await MessageResource.CreateAsync(options);
            _logger.LogInformation("Twilio SMS queued sid={Sid} status={Status} to {Phone}", result.Sid, result.Status, Mask(phoneNumber));
            return result.Status != MessageResource.StatusEnum.Failed
                   && result.Status != MessageResource.StatusEnum.Undelivered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio SMS send failed for {Phone}", Mask(phoneNumber));
            return false;
        }
    }

    private void EnsureClientInitialized()
    {
        if (_initialized) return;
        TwilioClient.Init(_accountSid, _authToken);
        _initialized = true;
    }

    private static string Mask(string phone) =>
        phone.Length <= 4 ? phone : new string('*', phone.Length - 4) + phone[^4..];
}
