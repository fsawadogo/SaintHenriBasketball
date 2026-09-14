namespace SaintHenriBasketball.Application.Settings;

public class StripeSettings
{
    public const string SectionName = "Stripe";
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = "https://sainthenribasketball.com/drop-in-payment?status=success&sessionId={SESSION_ID}";
    public string CancelUrl { get; set; } = "https://sainthenribasketball.com/drop-in-payment?status=cancelled&sessionId={SESSION_ID}";
    public string SeasonSuccessUrl { get; set; } = "https://sainthenribasketball.com/season-subscription?status=success&seasonId={SEASON_ID}";
    public string SeasonCancelUrl { get; set; } = "https://sainthenribasketball.com/season-subscription?status=cancelled&seasonId={SEASON_ID}";
}
