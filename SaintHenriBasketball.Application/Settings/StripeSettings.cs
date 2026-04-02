namespace SaintHenriBasketball.Application.Settings;

public class StripeSettings
{
    public const string SectionName = "Stripe";
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = "https://sainthenribasketball.com/drop-in-payment?status=success&sessionId={SESSION_ID}";
    public string CancelUrl { get; set; } = "https://sainthenribasketball.com/drop-in-payment?status=cancelled&sessionId={SESSION_ID}";
}
