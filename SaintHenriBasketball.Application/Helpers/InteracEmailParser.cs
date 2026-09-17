using System.Globalization;
using System.Text.RegularExpressions;

namespace SaintHenriBasketball.Application.Helpers;

/// What a deposit confirmation email says. Anything the email leaves out is null.
public record ParsedInteracDeposit(
    decimal Amount,
    string SenderName,
    /// The note the sender typed. Players are told to put their payment reference here.
    string? Message,
    /// Interac's own reference for the transfer, e.g. CAEVDQPM.
    string? ReferenceNumber,
    /// The date on the email, as a local calendar day; null when it could not be read.
    DateTime? SentOn);

/// <summary>
/// Reads an Interac auto-deposit confirmation.
///
/// The emails are laid out as a table of labelled values — Message, Date, Reference Number,
/// Sent From, Amount — and the subject repeats the amount and the sender. This reads the labelled
/// values first, because they are unambiguous, and falls back to the subject when a label is
/// missing. The Message field is optional: a sender can leave it empty.
/// </summary>
public static class InteracEmailParser
{
    private static readonly Regex Tag = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"[ \t ]+", RegexOptions.Compiled);

    // "You've received $10.00 from benjamin nangbo-lipson and it has been automatically deposited."
    private static readonly Regex SubjectPattern = new(
        @"received\s+\$?\s*(?<amount>[\d,]+\.?\d*)\s+from\s+(?<sender>.+?)\s+and it has been",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // French: "Vous avez reçu 10,00 $ de Jean Tremblay et le montant a été déposé"
    private static readonly Regex SubjectPatternFr = new(
        @"re[cç]u\s+(?<amount>[\d\s,\.]+)\s*\$\s+de\s+(?<sender>.+?)\s+et",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string Money = @"\$?\s*(?<value>[\d,\s]+[\.,]?\d*)\s*(?:\$)?\s*(?:\(?CAD\)?)?";

    /// A label starts its own line and ends in a colon, so "Funds Deposited!" is never read as a sender.
    private static Regex Labelled(string labels, string value) =>
        new($@"^[ \t]*(?:{labels})[ \t]*:[ \t]*(?:\r?\n)?[ \t]*{value}",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex AmountLine = Labelled("Amount|Montant", Money);
    private static readonly Regex SenderLine = Labelled("Sent From|Envoy[ée] par|Exp[ée]diteur", @"(?<value>[^\r\n]+)");
    private static readonly Regex MessageLine = Labelled("Message", @"(?<value>[^\r\n]*)");
    private static readonly Regex ReferenceLine = Labelled(@"Reference Number|Num[ée]ro de r[ée]f[ée]rence", @"(?<value>[A-Za-z0-9\-]+)");
    private static readonly Regex DateLine = Labelled("Date", @"(?<value>[^\r\n]+)");

    /// Plain text from an email that may be HTML. Block tags become line breaks so labels keep their own lines.
    public static string ToPlainText(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        var text = Regex.Replace(body, @"<\s*(br|/p|/div|/tr|/td|/h\d|/li)\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Tag.Replace(text, " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Whitespace.Replace(text, " ");
        var lines = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0);
        return string.Join("\n", lines);
    }

    /// Null when the email is not a deposit confirmation, or carries no amount to act on.
    public static ParsedInteracDeposit? Parse(string subject, string body)
    {
        var text = ToPlainText(body);
        var subjectText = ToPlainText(subject ?? string.Empty);

        var amount = ReadMoney(AmountLine.Match(text)) ?? ReadMoney(SubjectPattern.Match(subjectText), "amount") ?? ReadMoney(SubjectPatternFr.Match(subjectText), "amount");
        if (amount is null or <= 0) return null;

        var sender = Value(SenderLine.Match(text));
        if (string.IsNullOrWhiteSpace(sender))
        {
            var fromSubject = SubjectPattern.Match(subjectText);
            if (!fromSubject.Success) fromSubject = SubjectPatternFr.Match(subjectText);
            sender = fromSubject.Success ? fromSubject.Groups["sender"].Value.Trim() : string.Empty;
        }

        // "Message:" is optional, and the label also appears in the subject of some notices; an empty
        // value means the sender left the note blank, which is not an error.
        var message = Value(MessageLine.Match(text));
        var reference = Value(ReferenceLine.Match(text));

        return new ParsedInteracDeposit(
            amount.Value,
            sender.Trim(),
            string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            ReadDate(Value(DateLine.Match(text))));
    }

    private static string Value(Match match) => match.Success ? match.Groups["value"].Value.Trim() : string.Empty;

    private static decimal? ReadMoney(Match match, string group = "value")
    {
        if (!match.Success) return null;
        var raw = match.Groups[group].Value.Replace(" ", string.Empty).Replace(" ", string.Empty);
        if (raw.Length == 0) return null;
        // Canadian French writes 1 234,56; English writes 1,234.56.
        var normalized = raw.Contains(',') && !raw.Contains('.') && raw.LastIndexOf(',') >= raw.Length - 3
            ? raw.Replace(",", ".")
            : raw.Replace(",", string.Empty);
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static readonly string[] DateFormats =
    {
        "MMMM d, yyyy", "MMM d, yyyy", "d MMMM yyyy", "d MMM yyyy", "yyyy-MM-dd", "MM/dd/yyyy",
    };

    private static DateTime? ReadDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        foreach (var culture in new[] { CultureInfo.GetCultureInfo("en-CA"), CultureInfo.GetCultureInfo("fr-CA"), CultureInfo.InvariantCulture })
        {
            if (DateTime.TryParseExact(raw, DateFormats, culture, DateTimeStyles.None, out var exact)) return exact.Date;
            if (DateTime.TryParse(raw, culture, DateTimeStyles.None, out var loose)) return loose.Date;
        }
        return null;
    }
}
