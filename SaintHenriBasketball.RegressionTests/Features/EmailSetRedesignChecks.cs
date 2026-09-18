using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Templates;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;

/// Checks for the redesigned player emails.
///
/// These pin the things that were actually wrong: a payment reminder that sent players to a fixed
/// Stripe link and never told them their reference, a session reminder whose "cancel" button booked
/// the place, a waitlist offer sent as raw two-language text, and auth emails that arrived in French
/// no matter who read them.
internal static class EmailSetRedesignChecks
{
    public static Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var date = new DateTime(2027, 3, 13, 0, 0, 0, DateTimeKind.Unspecified);

        // --- The shell every email flows through ---
        var shell = EmailTemplateHelper.BuildEmailLayout("Title", "Titre", "<p>body</p>", EmailLanguage.English,
            "The line the inbox shows.");
        assert(shell.Contains("The line the inbox shows.", StringComparison.Ordinal)
            && shell.IndexOf("The line the inbox shows.", StringComparison.Ordinal) < shell.IndexOf("SAINT-HENRI BASKETBALL", StringComparison.Ordinal),
            "email shell: the preheader is in the markup, ahead of the header the inbox would otherwise scrape");

        assert(shell.Contains("display:none", StringComparison.Ordinal),
            "email shell: the preheader is hidden from the rendered email");

        assert(!shell.Contains("438", StringComparison.Ordinal) && !shell.Contains("935-8129", StringComparison.Ordinal),
            "email shell: the club phone number is not published in the footer");

        assert(shell.Contains("/profile", StringComparison.Ordinal)
            && shell.Contains("Email preferences", StringComparison.Ordinal),
            "email shell: every email offers a way to change what you receive");

        var noPreheader = EmailTemplateHelper.BuildEmailLayout("Title", "Titre", "<p>body</p>", EmailLanguage.English);
        assert(!noPreheader.Contains("display:none;max-height:0", StringComparison.Ordinal),
            "email shell: an email with nothing worth previewing gets no empty preheader block");

        // --- Payment reminder ---
        var reminder = EmailTemplates.Payments.GetPaymentReminderEmail(
            "Paula", 90m, PaymentPlan.Season, null, "SEASON-abc123", EmailLanguage.English);

        assert(!reminder.Contains("buy.stripe.com", StringComparison.OrdinalIgnoreCase),
            "payment reminder: no fixed Stripe link — money paid there could never be traced to a player");
        assert(reminder.Contains("/season-subscription", StringComparison.Ordinal),
            "payment reminder: a season player is sent to the page that opens their own checkout");
        assert(EmailTemplates.Payments.GetPaymentReminderEmail("Paula", 10m, PaymentPlan.DropIn, null, "DROPIN-x", EmailLanguage.English)
            .Contains("/drop-in-payment", StringComparison.Ordinal),
            "payment reminder: a drop-in player is sent to the drop-in page");

        assert(reminder.Contains("SEASON-abc123", StringComparison.Ordinal),
            "payment reminder: the reference is shown, so an Interac transfer can be matched to the sender");
        assert(reminder.Contains("in the message", StringComparison.OrdinalIgnoreCase),
            "payment reminder: it says where to put the reference, not just what it is");

        var noRef = EmailTemplates.Payments.GetPaymentReminderEmail("Paula", 90m, PaymentPlan.Season, null, null, EmailLanguage.English);
        assert(!noRef.Contains("putting", StringComparison.OrdinalIgnoreCase) && noRef.Contains("pay@sainthenribasketball.com", StringComparison.Ordinal),
            "payment reminder: with no reference to quote, it does not tell the player to quote one");

        var reminderFr = EmailTemplates.Payments.GetPaymentReminderEmail("Paula", 90m, PaymentPlan.Season, null, "SEASON-abc123", EmailLanguage.French);
        assert(reminderFr.Contains("Payer en ligne", StringComparison.Ordinal) && !reminderFr.Contains("Pay online", StringComparison.Ordinal),
            "payment reminder: the French version is French");

        // --- Session reminder ---
        var confirm = "https://sainthenribasketball.com/attendance/confirm?t=yes";
        var decline = "https://sainthenribasketball.com/attendance/confirm?t=no";

        var withDecline = EmailTemplates.Attendance.GetAttendanceReminderEmail(
            Guid.NewGuid(), Guid.NewGuid(), date, "Paula", "10:00", "12:00", "Saint-Henri", null,
            EmailLanguage.English, confirm, decline);
        assert(withDecline.Contains(decline, StringComparison.Ordinal) && withDecline.Contains(confirm, StringComparison.Ordinal),
            "session reminder: both answers are offered when both links exist");
        assert(withDecline.Contains("goes to whoever is waiting", StringComparison.OrdinalIgnoreCase),
            "session reminder: says why answering matters");

        var withoutDecline = EmailTemplates.Attendance.GetAttendanceReminderEmail(
            Guid.NewGuid(), Guid.NewGuid(), date, "Paula", "10:00", "12:00", "Saint-Henri", null,
            EmailLanguage.English, confirm, null);
        var declineLabels = new[] { "I can't make it", "Cancel my place", "Annuler ma place" };
        assert(!declineLabels.Any(l => withoutDecline.Contains(l, StringComparison.OrdinalIgnoreCase)),
            "session reminder: with no way to decline, no decline button is shown — it used to point at the booking page");

        // --- Waitlist offer ---
        var expires = new DateTime(2027, 3, 12, 18, 30, 0, DateTimeKind.Unspecified);
        var offer = EmailTemplates.Sessions.GetWaitlistOfferEmail(
            "Paula", date, "10:00", "12:00", "717 Saint-Ferdinand", expires,
            "https://sainthenribasketball.com/sessions/abc/book", EmailLanguage.English);

        assert(offer.Contains("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) && offer.Contains("SAINT-HENRI BASKETBALL", StringComparison.Ordinal),
            "waitlist offer: it is a real branded email, not a line of raw text");
        assert(offer.Contains("18:30", StringComparison.Ordinal) && offer.Contains("Montreal", StringComparison.Ordinal),
            "waitlist offer: the deadline is stated, with the timezone it is in");
        assert(offer.Contains("/sessions/abc/book", StringComparison.Ordinal),
            "waitlist offer: there is one thing to press, and it claims the place");

        var offerFr = EmailTemplates.Sessions.GetWaitlistOfferEmail(
            "Paula", date, "10:00", "12:00", "717 Saint-Ferdinand", expires,
            "https://sainthenribasketball.com/sessions/abc/book", EmailLanguage.French);
        assert(offerFr.Contains("Réserver ma place", StringComparison.Ordinal) && !offerFr.Contains("Claim my place", StringComparison.Ordinal),
            "waitlist offer: the French version is French, not both languages in one message");

        // --- Auth emails carry a language at all ---
        var confirmEn = EmailTemplates.Authentication.GetConfirmationEmail("Paula", "https://x/confirm", EmailLanguage.English);
        var confirmFr = EmailTemplates.Authentication.GetConfirmationEmail("Paula", "https://x/confirm", EmailLanguage.French);
        assert(confirmEn.Contains("Confirm Your Email", StringComparison.Ordinal) && confirmFr.Contains("Confirmez votre courriel", StringComparison.Ordinal),
            "confirmation email: it renders in whichever language it is asked for");

        var created = EmailTemplates.Authentication.GetAccountCreatedEmail("Paula", "Sup3rSecret!", "https://x/login", EmailLanguage.English);
        var preheaderEnd = created.IndexOf("</div>", StringComparison.Ordinal);
        assert(preheaderEnd > 0 && !created[..preheaderEnd].Contains("Sup3rSecret!", StringComparison.Ordinal),
            "account created: the temporary password is not in the preheader, which the inbox shows in the list");

        return Task.CompletedTask;
    }
}
