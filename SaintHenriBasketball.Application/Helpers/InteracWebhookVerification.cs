using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SaintHenriBasketball.Application.Helpers;

/// <summary>
/// Decides whether a call to the deposit webhook really carries a bank email.
///
/// A deposit marks money as received, so the endpoint is worth attacking: anyone who could post to
/// it could clear their own payment. The shared token is the lock on the door; the signature proves
/// the forwarding service sent the body unchanged; and the sender check means a leaked token alone
/// is not enough, because the body must still be an email Interac sent.
/// </summary>
public static class InteracWebhookVerification
{
    public const string TokenHeader = "X-SHB-Webhook-Token";
    public const string SignatureHeader = "X-SHB-Signature";

    /// Domains the original email may come from, when the configuration names none.
    public static readonly string[] DefaultSenderDomains = { "payments.interac.ca", "interac.ca" };

    /// Compares in constant time, so a wrong value cannot be guessed one character at a time.
    public static bool SecretMatches(string? provided, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected)) return false;
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided ?? string.Empty)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }

    /// <summary>
    /// HMAC-SHA256 of the raw request body, as "sha256=&lt;hex&gt;" or bare hex. Mailgun, Postmark and
    /// friends all sign this way; the header name differs, and the controller maps it.
    /// </summary>
    public static bool SignatureMatches(string? header, string rawBody, string? signingSecret)
    {
        if (string.IsNullOrWhiteSpace(signingSecret)) return false;
        if (string.IsNullOrWhiteSpace(header)) return false;

        var provided = header.Trim();
        var equals = provided.IndexOf('=');
        if (equals > 0 && provided[..equals].Trim().Equals("sha256", StringComparison.OrdinalIgnoreCase))
            provided = provided[(equals + 1)..].Trim();

        var expected = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingSecret), Encoding.UTF8.GetBytes(rawBody ?? string.Empty)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(provided.ToLowerInvariant()));
    }

    private static readonly Regex Address = new(@"[A-Za-z0-9._%+\-]+@([A-Za-z0-9.\-]+\.[A-Za-z]{2,})", RegexOptions.Compiled);
    // Gmail's inline forward keeps the original headers as text: "From: Interac <notify@payments.interac.ca>".
    private static readonly Regex ForwardedFrom = new(@"^\s*(?:From|De)\s*:\s*(?<line>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Who really sent the email. A forwarding rule rewrites the envelope sender to the mailbox that
    /// forwarded it, so the field the service reports is checked first, then the original From line
    /// the forward carries in its text.
    /// </summary>
    public static string? OriginalSender(string? reportedFrom, string? body)
    {
        var reported = FirstAddress(reportedFrom);
        if (reported != null) return reported;

        // Decode entities but keep the angle brackets: an address written "Interac <notify@…>" looks
        // like an HTML tag, and stripping tags first would throw the sender away.
        var decoded = System.Net.WebUtility.HtmlDecode(body ?? string.Empty);
        foreach (var text in new[] { decoded, InteracEmailParser.ToPlainText(body ?? string.Empty) })
        {
            foreach (Match match in ForwardedFrom.Matches(text))
            {
                var address = FirstAddress(match.Groups["line"].Value);
                if (address != null) return address;
            }
        }
        return null;
    }

    private static string? FirstAddress(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = Address.Match(text);
        return match.Success ? match.Value.ToLowerInvariant() : null;
    }

    /// True when the address belongs to one of the allowed domains, or a subdomain of one.
    public static bool SenderAllowed(string? address, IReadOnlyCollection<string>? allowedDomains)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        var domains = allowedDomains is { Count: > 0 } ? allowedDomains : DefaultSenderDomains;
        var at = address.LastIndexOf('@');
        if (at < 0) return false;
        var domain = address[(at + 1)..].Trim().ToLowerInvariant();

        return domains
            .Select(d => d.Trim().TrimStart('@', '.').ToLowerInvariant())
            .Where(d => d.Length > 0)
            .Any(d => domain == d || domain.EndsWith("." + d, StringComparison.Ordinal));
    }

    /// Reads a comma or semicolon separated configuration value into domains.
    public static string[] ParseDomains(string? configured) =>
        string.IsNullOrWhiteSpace(configured)
            ? DefaultSenderDomains
            : configured.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
