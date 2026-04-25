namespace SaintHenriBasketball.Application.DTOs.Sms;

public class SmsPreferenceDto
{
    public bool SmsOptIn { get; set; }
    public string? PhoneNumber { get; set; }
    public bool SmsAnnouncementDismissed { get; set; }
}

public class SmsTestResultDto
{
    public string Provider { get; set; } = string.Empty;
    public bool Configured { get; set; }
    public bool Sent { get; set; }
    public string PhoneTo { get; set; } = string.Empty;
}
