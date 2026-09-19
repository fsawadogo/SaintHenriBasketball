namespace SaintHenriBasketball.Application.Helpers;

/// <summary>
/// Whether an address is well formed at all.
///
/// FluentValidation's EmailAddress() is deliberately permissive — it asks little more than
/// whether there is an @ in there — and several of September's signups took advantage:
/// ava..b.l.ake5@gmail.com and p...s.ie.benm.o.rgen@gmail.com both carry runs of dots that
/// RFC 5322 does not allow in a local part, and a dot may not open or close one either.
///
/// This stays on the rules no real address breaks. It is not a spam filter: every address in the
/// flood that was merely scraped rather than malformed passes this, and should — the defence
/// against those is the send budget, not a guess about which strangers are real.
/// </summary>
public static class EmailAddressRules
{
    private const int MaxLocalLength = 64;    // RFC 5321
    private const int MaxTotalLength = 254;

    public static bool IsWellFormed(string? email)
    {
        var address = (email ?? string.Empty).Trim();
        if (address.Length == 0 || address.Length > MaxTotalLength) return false;

        var at = address.LastIndexOf('@');
        if (at <= 0 || at == address.Length - 1) return false;
        if (address.IndexOf('@') != at) return false;   // exactly one @, unquoted

        var local = address[..at];
        var domain = address[(at + 1)..];

        if (local.Length > MaxLocalLength) return false;
        if (!IsWellFormedPart(local)) return false;

        // A domain has to have a dot and a label after it, so "bob@localhost" does not register.
        if (!IsWellFormedPart(domain) || !domain.Contains('.')) return false;
        if (domain.Contains(' ')) return false;

        var tld = domain[(domain.LastIndexOf('.') + 1)..];
        return tld.Length >= 2 && tld.All(char.IsLetter);
    }

    /// No leading dot, no trailing dot, and never two in a row — on either side of the @.
    private static bool IsWellFormedPart(string part) =>
        part.Length > 0
        && part[0] != '.'
        && part[^1] != '.'
        && !part.Contains("..", StringComparison.Ordinal);
}
