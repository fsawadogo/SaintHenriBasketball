using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SaintHenriBasketball.Application.Helpers;

public record UnsubscribeLinkData(Guid UserId, long ExpiresAt);

/// Signed, expiring links that turn off community updates without signing in.
/// CASL requires an unsubscribe mechanism to keep working for at least 60 days after sending.
public class UnsubscribeLinks(IConfiguration configuration)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(90);

    private byte[] Key => Encoding.UTF8.GetBytes(configuration["JwtSettings:Key"] ?? throw new InvalidOperationException("Signing key missing"));
    private byte[] Sign(string value) => HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes("shb-unsubscribe-v1:" + value));

    public string CreateToken(Guid userId, DateTimeOffset expiresAt)
    {
        var payload = $"{userId:N}.{expiresAt.ToUnixTimeSeconds()}";
        return payload + "." + Convert.ToHexString(Sign(payload));
    }

    public string CreateUrl(Guid userId) =>
        $"{(configuration["AppUrl"] ?? "https://sainthenribasketball.com").TrimEnd('/')}/unsubscribe?token={CreateToken(userId, DateTimeOffset.UtcNow.Add(Lifetime))}";

    public UnsubscribeLinkData? Validate(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 200) return null;
        var parts = token.Split('.');
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var user) || !long.TryParse(parts[1], out var expiry)
            || expiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(Sign($"{parts[0]}.{parts[1]}"), Convert.FromHexString(parts[2]))) return null;
            return new(user, expiry);
        }
        catch (FormatException) { return null; }
    }
}
