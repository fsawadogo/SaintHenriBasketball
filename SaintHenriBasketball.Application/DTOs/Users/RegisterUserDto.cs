using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Users;

public class RegisterUserDto
{
    public required string? Username { get; set; }
    public required string? Email { get; set; }
    public required string Password { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
    /// Optional; honoured only while the referrals flag is on.
    public string? ReferralCode { get; set; }

    /// <summary>
    /// A decoy. The form renders it hidden and no person ever fills it, so anything here means the
    /// request came from something filling in every field it found.
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Short-lived, single-use Cloudflare Turnstile token. Required when Turnstile:SecretKey is
    /// configured on the API.
    /// </summary>
    public string? TurnstileToken { get; set; }
}
