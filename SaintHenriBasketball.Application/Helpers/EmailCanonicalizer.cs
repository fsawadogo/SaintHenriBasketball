namespace SaintHenriBasketball.Application.Helpers;

/// <summary>
/// The mailbox an address actually reaches, rather than the way it happens to be spelled.
///
/// September's flood leaned on this: q.ui.ckco.n.s.i.gn.me.nt8.02@gmail.com and
/// qui.ckcon.si.gn..men.t8.02@gmail.com are one Gmail inbox, and so are the two osmanidard
/// spellings. Gmail ignores dots in the local part entirely, so an operator with one mailbox has
/// as many distinct-looking addresses as it has letters to put dots between.
///
/// Only documented behaviour is undone here. Dots are stripped for Gmail alone, because Gmail is
/// the provider that says it ignores them; plus-addressing is stripped for the providers that
/// document it. Everywhere else the local part is left exactly as written — an address is opaque
/// to everyone but its own mail server, and guessing costs a real person their account.
/// </summary>
public static class EmailCanonicalizer
{
    private static readonly HashSet<string> DotsAreNoise =
        new(StringComparer.OrdinalIgnoreCase) { "gmail.com", "googlemail.com" };

    private static readonly HashSet<string> PlusIsATag =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "gmail.com", "googlemail.com",
            "outlook.com", "hotmail.com", "live.com", "msn.com",
            "icloud.com", "me.com", "mac.com",
            "protonmail.com", "proton.me",
            "yahoo.com", "fastmail.com",
        };

    /// <summary>
    /// The address reduced to the mailbox it reaches. Returns the trimmed, lowercased input
    /// unchanged when it is not an address this can reason about.
    /// </summary>
    public static string Canonicalize(string? email)
    {
        var trimmed = (email ?? string.Empty).Trim().ToLowerInvariant();

        var at = trimmed.LastIndexOf('@');
        if (at <= 0 || at == trimmed.Length - 1) return trimmed;

        var local = trimmed[..at];
        var domain = trimmed[(at + 1)..];

        if (PlusIsATag.Contains(domain))
        {
            var plus = local.IndexOf('+');
            if (plus >= 0) local = local[..plus];
        }

        if (DotsAreNoise.Contains(domain))
        {
            local = local.Replace(".", string.Empty);
            // Gmail treats the two names as one service, so both spellings land in one inbox.
            domain = "gmail.com";
        }

        // Everything was punctuation: keep the original rather than invent an empty mailbox.
        return local.Length == 0 ? trimmed : $"{local}@{domain}";
    }
}
