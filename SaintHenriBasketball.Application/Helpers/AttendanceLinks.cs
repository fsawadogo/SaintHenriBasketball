using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SaintHenriBasketball.Application.Helpers;

public record AttendanceLinkData(Guid SessionId, Guid UserId, bool Attending, long ExpiresAt);

public class AttendanceLinks(IConfiguration configuration)
{
    private byte[] Key => Encoding.UTF8.GetBytes(configuration["JwtSettings:Key"] ?? throw new InvalidOperationException("Signing key missing"));
    private byte[] Sign(string value) => HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes("shb-attendance-v1:" + value));
    public string Create(Guid sessionId, Guid userId, bool attending, DateTime expiresUtc)
    {
        var payload = $"{sessionId:N}.{userId:N}.{(attending ? 1 : 0)}.{new DateTimeOffset(DateTime.SpecifyKind(expiresUtc, DateTimeKind.Utc)).ToUnixTimeSeconds()}";
        var token = payload + "." + Convert.ToHexString(Sign(payload));
        return $"{(configuration["AppUrl"] ?? "https://sainthenribasketball.com").TrimEnd('/')}/attendance/confirm?token={token}";
    }
    public AttendanceLinkData? Validate(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 300) return null;
        var parts = token.Split('.');
        if (parts.Length != 5 || !Guid.TryParseExact(parts[0], "N", out var session) || !Guid.TryParseExact(parts[1], "N", out var user)
            || (parts[2] != "0" && parts[2] != "1") || !long.TryParse(parts[3], out var expiry)
            || expiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(Sign(string.Join('.', parts.Take(4))), Convert.FromHexString(parts[4]))) return null;
            return new(session, user, parts[2] == "1", expiry);
        }
        catch (FormatException) { return null; }
    }
}
