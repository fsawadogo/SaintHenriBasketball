using SaintHenriBasketball.Application.Helpers;

/// Making a signup cost more than a spelling.
///
/// September's flood did not forge addresses — it collected real ones, and multiplied the few it
/// controlled by exploiting Gmail's indifference to dots. Each account sent a confirmation email
/// from the club's domain to somebody who never asked for it, which is the whole payload: the
/// club's standing with the receiving mail servers, spent a hundred times over.
///
/// These checks cover the two halves of that. The canonicalizer decides whether two spellings are
/// one mailbox; the address rules decide whether a spelling is an address at all.
internal static class RegistrationHardeningChecks
{
    public static void Run(Action<bool, string> assert)
    {
        // --- One mailbox, however it is spelled ---
        string Same(string a, string b) =>
            EmailCanonicalizer.Canonicalize(a) == EmailCanonicalizer.Canonicalize(b) ? "same" : "different";

        // The two spellings the flood actually used, taken from the purge list.
        assert(Same("q.ui.ckco.n.s.i.gn.me.nt8.02@gmail.com", "qui.ckcon.si.gn..men.t8.02@gmail.com") == "same",
            "registration hardening: two dotted spellings of one Gmail address are one mailbox");
        assert(Same("os.ma.n.i..d.ard@gmail.com", "o.s.m.ani.da.r.d@gmail.com") == "same",
            "registration hardening: and so are the two osmanidard spellings");

        assert(Same("player@gmail.com", "player+season2026@gmail.com") == "same",
            "registration hardening: a plus tag does not make a second mailbox");
        assert(Same("player@gmail.com", "player@googlemail.com") == "same",
            "registration hardening: Gmail's two domain names are one inbox");
        assert(Same("Player@Gmail.com", "player@gmail.com") == "same",
            "registration hardening: capitals are not a second mailbox either");

        // The other half: not inventing collisions between people who are genuinely different.
        assert(Same("a.b@example.com", "ab@example.com") == "different",
            "registration hardening: dots are only noise at Gmail — elsewhere the local part is opaque");
        assert(Same("franck@hotmail.com", "philip@hotmail.com") == "different",
            "registration hardening: two different people at one provider stay two people");
        assert(Same("guiraud.franck@hotmail.com", "guiraudfranck@hotmail.com") == "different",
            "registration hardening: Hotmail keeps its dots, so a real member is not merged away");

        assert(EmailCanonicalizer.Canonicalize("....@gmail.com") == "....@gmail.com",
            "registration hardening: an address that is all punctuation is left alone rather than reduced to nothing");
        assert(EmailCanonicalizer.Canonicalize("not-an-address") == "not-an-address",
            "registration hardening: something that is not an address comes back unchanged");

        // --- Whether a spelling is an address at all ---
        // Both of these registered in September. Neither is a valid address under RFC 5322.
        assert(!EmailAddressRules.IsWellFormed("ava..b.l.ake5@gmail.com"),
            "registration hardening: two dots in a row is not an address, whatever EmailAddress() says");
        assert(!EmailAddressRules.IsWellFormed("p...s.ie.benm.o.rgen@gmail.com"),
            "registration hardening: nor is three");

        assert(!EmailAddressRules.IsWellFormed(".player@gmail.com"),
            "registration hardening: a local part may not open with a dot");
        assert(!EmailAddressRules.IsWellFormed("player.@gmail.com"),
            "registration hardening: nor close with one");
        assert(!EmailAddressRules.IsWellFormed("player@gmail..com"),
            "registration hardening: the domain may not double its dots either");
        assert(!EmailAddressRules.IsWellFormed("player@localhost"),
            "registration hardening: a domain needs a public suffix");
        assert(!EmailAddressRules.IsWellFormed("player@@gmail.com"),
            "registration hardening: one @ only");
        assert(!EmailAddressRules.IsWellFormed(""),
            "registration hardening: an empty address is not an address");
        assert(!EmailAddressRules.IsWellFormed(new string('a', 65) + "@gmail.com"),
            "registration hardening: a local part longer than RFC 5321 allows is refused");

        // The addresses that must keep working. Every one of these is from the real roster or the
        // purge list: the rules must not reject a scraped-but-valid address on a hunch, because
        // the defence against those is the send budget, not a guess about who is real.
        foreach (var valid in new[]
        {
            "ismael.bozari@gmail.com",
            "foko.herve@gmail.com",
            "guiraud.franck@hotmail.com",
            "philipmarkhamwheeler@gmail.com",
            "ashley.kennedy@morson-projects.co.uk",
            "m.mueller@waldner.de",
            "stevo_com@yahoo.com",
            "dominicsalcedo@students.camdencc.edu",
            "nicholas.piezonka@colorado-opg.org",
            "player+season2026@gmail.com",
        })
        {
            assert(EmailAddressRules.IsWellFormed(valid),
                $"registration hardening: {valid} is a real address and stays accepted");
        }
    }
}
