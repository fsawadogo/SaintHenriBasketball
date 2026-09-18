using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Domain.Enums;
using static SaintHenriBasketball.Application.Helpers.EmailTemplateHelper;

namespace SaintHenriBasketball.Application.Templates;

public static class EmailTemplates
{
    #region Authentication
    public static class Authentication
    {
        public static string GetConfirmationEmail(string userName, string confirmationLink, EmailLanguage lang = EmailLanguage.French) =>
            BuildEmailLayout("Confirm Your Email", "Confirmez votre courriel",
                Greeting(userName, lang) +
                P(L("We're excited to welcome you to our basketball community. Please confirm your email address to get started:",
                     "Nous sommes ravis de vous accueillir dans notre communauté de basketball. Veuillez confirmer votre adresse courriel:", lang)) +
                BuildButton("Confirm Email", "Confirmer le courriel", confirmationLink, lang) +
                P(L($"Or copy and paste this link in your browser:<br/><span style='font-size:12px;color:#637369;word-break:break-all;'>{confirmationLink}</span>",
                     $"Ou copiez et collez ce lien dans votre navigateur:<br/><span style='font-size:12px;color:#637369;word-break:break-all;'>{confirmationLink}</span>", lang)),
            lang,
            LSubject("One click and your account is ready.", "Un clic et votre compte est prêt.", lang));

        public static string GetPasswordResetEmail(string userName, string resetLink, EmailLanguage lang = EmailLanguage.French) =>
            BuildEmailLayout("Reset Your Password", "Réinitialisation du mot de passe",
                Greeting(userName, lang) +
                P(L("We received a request to reset your password:", "Nous avons reçu une demande de réinitialisation de votre mot de passe:", lang)) +
                BuildButton("Reset Password", "Réinitialiser le mot de passe", resetLink, lang) +
                BuildAlertBox(L("This link expires in 1 hour. If you didn't request this, you can safely ignore this email.",
                                 "Ce lien expire dans 1 heure. Si vous n'avez pas fait cette demande, vous pouvez ignorer ce courriel.", lang), "warning"),
            lang,
            LSubject("The link works for one hour. Ignore this if it wasn't you.",
                     "Le lien est valide une heure. Ignorez ce courriel si ce n'était pas vous.", lang));

        public static string GetAccountCreatedEmail(string userName, string password, string loginLink, EmailLanguage lang = EmailLanguage.French) =>
            BuildEmailLayout("Your Account Has Been Created", "Votre compte a été créé",
                Greeting(userName, lang) +
                P(L("An account has been created for you. Here are your login credentials:",
                     "Un compte a été créé pour vous. Voici vos identifiants:", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { L("Username", "Nom d'utilisateur", lang), userName },
                    { L("Temporary Password", "Mot de passe temporaire", lang), password }
                }) +
                BuildAlertBox(L("Please change your password after your first login.",
                                 "Veuillez changer votre mot de passe après votre première connexion.", lang), "info") +
                BuildButton("Log In", "Se connecter", loginLink, lang),
            lang,
            // Deliberately says nothing about the password — a preheader is visible in the inbox
            // list, over the shoulder of anyone nearby.
            LSubject("Sign in and set a password of your own.",
                     "Connectez-vous et choisissez votre propre mot de passe.", lang));
    }
    #endregion

    #region Payments
    public static class Payments
    {
        public static string GetPaymentCreatedEmail(string userName, decimal amount, string reference, EmailLanguage lang = EmailLanguage.French) =>
            BuildEmailLayout("Payment Request", "Demande de paiement",
                Greeting(userName, lang) +
                P(L("A payment has been requested for your account:", "Un paiement a été demandé pour votre compte:", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { L("Amount", "Montant", lang), $"${amount:F2}" },
                    { L("Reference", "Référence", lang), reference }
                }) +
                P(L("Please send payment via Interac e-Transfer to:", "Veuillez envoyer le paiement par virement Interac à:", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { "Email", "pay@sainthenribasketball.com" },
                    { L("Reference", "Référence", lang), reference }
                }),
            lang);

        public static string GetPaymentConfirmationEmail(string userName, decimal amount, string reference, DateTime date, EmailLanguage lang = EmailLanguage.French) =>
            BuildEmailLayout("Payment Confirmation", "Confirmation de paiement",
                Greeting(userName, lang) +
                P(L("Your payment has been confirmed. Thank you!", "Votre paiement a été confirmé. Merci!", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { L("Amount", "Montant", lang), $"${amount:F2}" },
                    { L("Reference", "Référence", lang), reference },
                    { "Date", date.ToString("dd MMMM yyyy", GetCulture(lang)) }
                }) +
                BuildAlertBox(L("A PDF invoice is attached to this email.", "Une facture PDF est jointe à ce courriel.", lang), "success"),
            lang);

        public static string GetPaymentReminderEmail(string userName, decimal amount, PaymentPlan plan, string? customMessage = null, string? reference = null, EmailLanguage lang = EmailLanguage.French)
        {
            var planName = plan == PaymentPlan.Season
                ? L("Season Pass", "Forfait de saison", lang)
                : L("Drop-in", "Forfait à la séance", lang);

            // L() embeds markup for Bilingual, which an inbox preview would show raw.
            var planNamePlain = plan == PaymentPlan.Season
                ? LSubject("Season Pass", "Forfait de saison", lang)
                : LSubject("Drop-in", "Forfait à la séance", lang);

            var content = Greeting(userName, lang) +
                P(L($"This is a reminder that your payment for the <strong>{planName}</strong> is due.",
                     $"Ceci est un rappel que votre paiement pour le <strong>{planName}</strong> est dû.", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { L("Plan", "Forfait", lang), planName },
                    { L("Amount", "Montant", lang), $"${amount:F2}" },
                    // Without the reference an Interac transfer arrives anonymous and sits in the
                    // admin queue until someone guesses who sent it.
                    { L("Reference", "Référence", lang), reference }
                });

            if (!string.IsNullOrEmpty(customMessage))
                content += BuildAlertBox(customMessage, "info");

            // The app's own payment page, which opens a checkout tied to this player's payment row.
            // It used to be a fixed buy.stripe.com link that knew nothing about who was paying or
            // how much, so anything paid through it landed in Stripe unattributable.
            var payUrl = plan == PaymentPlan.Season
                ? $"{SiteUrl}/season-subscription"
                : $"{SiteUrl}/drop-in-payment";

            content += BuildButton("Pay online", "Payer en ligne", payUrl, lang);

            content += P(string.IsNullOrWhiteSpace(reference)
                ? L("Or send an Interac e-Transfer to <strong>pay@sainthenribasketball.com</strong>.",
                    "Ou envoyez un virement Interac à <strong>pay@sainthenribasketball.com</strong>.", lang)
                : L($"Or send an Interac e-Transfer to <strong>pay@sainthenribasketball.com</strong>, putting <strong>{reference}</strong> in the message so we can match it to you.",
                    $"Ou envoyez un virement Interac à <strong>pay@sainthenribasketball.com</strong>, en inscrivant <strong>{reference}</strong> dans le message pour qu'on puisse l'associer à votre compte.", lang));

            return BuildEmailLayout("Payment Reminder", "Rappel de paiement", content, lang,
                LSubject($"${amount:F2} is outstanding for your {planNamePlain}.",
                         $"Il reste ${amount:F2} à payer pour votre {planNamePlain}.", lang));
        }

        public static string GetPaymentFailedEmail(string userName, decimal amount, string? reference = null, string? reason = null, EmailLanguage lang = EmailLanguage.French)
        {
            var content = Greeting(userName, lang) +
                P(L("Unfortunately, your recent payment could not be processed.",
                     "Malheureusement, votre paiement récent n'a pas pu être traité.", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { L("Amount", "Montant", lang), $"${amount:F2}" },
                    { L("Reference", "Référence", lang), reference ?? "—" }
                });

            if (!string.IsNullOrEmpty(reason))
                content += BuildAlertBox(reason, "warning");

            content += P(L("Please try again or contact us at <strong>pay@sainthenribasketball.com</strong> for assistance.",
                           "Veuillez réessayer ou nous contacter à <strong>pay@sainthenribasketball.com</strong> pour obtenir de l'aide.", lang));

            return BuildEmailLayout("Payment Failed", "Échec du paiement", content, lang);
        }

        public static string GetPaymentPlanUpdateEmail(string userName, PaymentPlan newPlan, decimal newAmount, DateTime effectiveDate, string? additionalInfo = null, EmailLanguage lang = EmailLanguage.French)
        {
            var planName = newPlan == PaymentPlan.Season
                ? L("Season Pass", "Forfait de saison", lang)
                : L("Drop-in", "Forfait à la séance", lang);

            var content = Greeting(userName, lang) +
                P(L("Your payment plan has been updated:", "Votre forfait a été mis à jour:", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { L("New Plan", "Nouveau forfait", lang), planName },
                    { L("Amount", "Montant", lang), $"${newAmount:F2}" },
                    { L("Effective Date", "Date d'effet", lang), effectiveDate.ToString("dd MMMM yyyy", GetCulture(lang)) }
                });

            if (!string.IsNullOrEmpty(additionalInfo))
                content += BuildAlertBox(additionalInfo, "info");

            return BuildEmailLayout("Plan Update", "Mise à jour du forfait", content, lang);
        }
    }
    #endregion

    #region Attendance
    public static class Attendance
    {
        public static string GetAttendanceConfirmationEmail(string userName, DateTime sessionDate, string startTime, string endTime, string? location, bool isAttending, string? notes = null, EmailLanguage lang = EmailLanguage.French)
        {
            var status = isAttending
                ? L("Confirmed", "Confirmé", lang)
                : L("Declined", "Décliné", lang);

            var content = Greeting(userName, lang) +
                P(L("Your attendance has been recorded:", "Votre présence a été enregistrée:", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { "Date", sessionDate.ToString("dddd dd MMMM yyyy", GetCulture(lang)) },
                    { L("Time", "Heure", lang), $"{startTime} - {endTime}" },
                    { L("Location", "Lieu", lang), location ?? "717 Saint-Ferdinand Street" },
                    { L("Status", "Statut", lang), status }
                });

            if (!string.IsNullOrEmpty(notes))
                content += BuildAlertBox($"{L("Notes", "Notes", lang)}: {notes}", "info");

            return BuildEmailLayout("Attendance Confirmation", "Confirmation de présence", content, lang);
        }

        public static string GetAttendanceReminderEmail(Guid userId, Guid sessionId, DateTime sessionDate, string userName, string startTime, string endTime, string? location = null, string? customMessage = null, EmailLanguage lang = EmailLanguage.French, string? confirmationUrl = null, string? cancellationUrl = null)
        {
            var confirmUrl = confirmationUrl ?? $"https://sainthenribasketball.com/sessions/{sessionId}/book";

            var content = Greeting(userName, lang) +
                P(L("A basketball session is coming up! Don't forget to confirm your attendance.",
                     "Une session de basketball approche! N'oubliez pas de confirmer votre présence.", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { "Date", sessionDate.ToString("dddd dd MMMM yyyy", GetCulture(lang)) },
                    { L("Time", "Heure", lang), $"{startTime} - {endTime}" },
                    { L("Location", "Lieu", lang), location ?? "717 Saint-Ferdinand Street" }
                });

            if (!string.IsNullOrEmpty(customMessage))
                content += BuildAlertBox(customMessage, "info");

            content += P(L("What to bring: water bottle, clean indoor shoes, towel",
                           "À apporter: bouteille d'eau, souliers d'intérieur propres, serviette", lang)) +
                BuildButton("I'll be there", "J'y serai", confirmUrl, lang);

            // Declining is offered only when there is a real link to decline with. It used to fall
            // back to the confirmation URL, so "Cancel my place" booked the place instead.
            if (!string.IsNullOrWhiteSpace(cancellationUrl))
                content += BuildSecondaryButton(
                    LSubject("I can't make it", "Je ne peux pas y être", lang), cancellationUrl);

            content += P(L("Telling us either way helps — a place you release goes to whoever is waiting for one.",
                           "Nous le dire dans un cas comme dans l'autre aide : une place que vous libérez revient à quelqu'un qui attend.", lang));

            return BuildEmailLayout("Session Reminder", "Rappel de session", content, lang,
                LSubject($"{sessionDate.ToString("dddd d MMMM", GetCulture(lang))}, {startTime}–{endTime}. Let us know if you're coming.",
                         $"{sessionDate.ToString("dddd d MMMM", GetCulture(lang))}, {startTime}–{endTime}. Dites-nous si vous venez.", lang));
        }

        public static string GetAttendanceUpdateEmail(string userName, DateTime sessionDate, string startTime, string endTime, string? location, bool previousStatus, bool newStatus, string? reason = null, EmailLanguage lang = EmailLanguage.French)
        {
            var prevLabel = previousStatus ? L("Confirmed", "Confirmé", lang) : L("Declined", "Décliné", lang);
            var newLabel = newStatus ? L("Confirmed", "Confirmé", lang) : L("Declined", "Décliné", lang);

            var content = Greeting(userName, lang) +
                P(L("Your attendance status has been updated:", "Votre statut de présence a été mis à jour:", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { "Date", sessionDate.ToString("dddd dd MMMM yyyy", GetCulture(lang)) },
                    { L("Time", "Heure", lang), $"{startTime} - {endTime}" },
                    { L("Previous Status", "Statut précédent", lang), prevLabel },
                    { L("New Status", "Nouveau statut", lang), newLabel }
                });

            if (!string.IsNullOrEmpty(reason))
                content += BuildAlertBox($"{L("Reason", "Raison", lang)}: {reason}", "info");

            return BuildEmailLayout("Attendance Update", "Mise à jour de présence", content, lang);
        }
    }
    #endregion

    #region Season
    public static class Season
    {
        public static string GetSeasonRegistrationConfirmationEmail(string userName, DateTime startDate, DateTime endDate, decimal price, EmailLanguage lang = EmailLanguage.French) =>
            BuildEmailLayout("Season Registration Confirmed", "Inscription à la saison confirmée",
                Greeting(userName, lang) +
                P(L("Your season registration has been confirmed!", "Votre inscription à la saison a été confirmée!", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { L("Period", "Période", lang), $"{startDate.ToString("dd MMMM yyyy", GetCulture(lang))} - {endDate.ToString("dd MMMM yyyy", GetCulture(lang))}" },
                    { L("Price", "Prix", lang), $"${price:F2}" }
                }) +
                P(L("Please send payment via Interac e-Transfer to <strong>pay@sainthenribasketball.com</strong>",
                     "Veuillez envoyer le paiement par virement Interac à <strong>pay@sainthenribasketball.com</strong>", lang)),
            lang);

        public static string GetSeasonCancellationEmail(string userName, DateTime startDate, DateTime endDate, EmailLanguage lang = EmailLanguage.French) =>
            BuildEmailLayout("Season Registration Cancelled", "Inscription à la saison annulée",
                Greeting(userName, lang) +
                P(L("Your season registration has been cancelled:", "Votre inscription à la saison a été annulée:", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { L("Period", "Période", lang), $"{startDate.ToString("dd MMMM yyyy", GetCulture(lang))} - {endDate.ToString("dd MMMM yyyy", GetCulture(lang))}" }
                }) +
                P(L("If this was done in error, please contact us.", "Si c'est une erreur, veuillez nous contacter.", lang)),
            lang);

        public static string GetSeasonRegistrationReminderEmail(string userName, string seasonName, DateTime startDate, DateTime endDate, decimal price, string registrationLink, string? customMessage = null, EmailLanguage lang = EmailLanguage.French)
        {
            var content = Greeting(userName, lang) +
                P(L($"Don't forget to register for the <strong>{seasonName}</strong> season!",
                     $"N'oubliez pas de vous inscrire pour la saison <strong>{seasonName}</strong>!", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { L("Season", "Saison", lang), seasonName },
                    { L("Period", "Période", lang), $"{startDate.ToString("dd MMMM yyyy", GetCulture(lang))} - {endDate.ToString("dd MMMM yyyy", GetCulture(lang))}" },
                    { L("Price", "Prix", lang), $"${price:F2}" }
                });

            if (!string.IsNullOrEmpty(customMessage))
                content += BuildAlertBox(customMessage, "info");

            content += BuildButton("Register Now", "S'inscrire maintenant", registrationLink, lang);

            return BuildEmailLayout("Season Registration Reminder", "Rappel d'inscription à la saison", content, lang);
        }

        /// <summary>
        /// A week before a season starts: pick a season pass, or pay per session.
        ///
        /// Prices and the number of passes left are read from the season, never typed in here, so the
        /// email cannot promise a price the club has since changed. It says what happens if the player
        /// does nothing, because an email that threatens nothing is easier to trust.
        /// </summary>
        public static string GetSeasonPlanChoiceEmail(SeasonPlanChoiceEmailModel model, EmailLanguage lang = EmailLanguage.French)
        {
            var culture = GetCulture(lang);
            string Money(decimal amount) => amount.ToString("C", culture);
            var app = model.AppUrl.TrimEnd('/');

            var days = model.DaysUntilStart;
            var intro = days <= 0
                ? L("The season starts today. Tell us how you would like to pay, and your place is ready.",
                    "La saison commence aujourd’hui. Dites-nous comment vous souhaitez payer et votre place est prête.", lang)
                : days == 1
                    ? L("The season starts tomorrow. Tell us how you would like to pay, so your place is ready for the first session.",
                        "La saison commence demain. Dites-nous comment vous souhaitez payer, pour que votre place soit prête dès la première séance.", lang)
                    : L($"The season starts in {days} days. Tell us how you would like to pay, so your place is ready for the first session.",
                        $"La saison commence dans {days} jours. Dites-nous comment vous souhaitez payer, pour que votre place soit prête dès la première séance.", lang);

            var content = Greeting(model.FirstName, lang) + P(intro);

            // The two plans, side by side, priced from the season.
            var passesLine = model.PassCapacity <= 0
                ? L("Unlimited", "Illimité", lang)
                : model.PassesLeft <= 0
                    ? L("Sold out", "Complet", lang)
                    : L($"{model.PassesLeft} of {model.PassCapacity} left", $"{model.PassesLeft} sur {model.PassCapacity}", lang);

            var dropInPrice = model.DropInPrice is decimal drop
                ? Money(drop)
                : L("set per session", "fixé par séance", lang);

            content += BuildInfoBox(new Dictionary<string, string?>
            {
                { L("Season pass", "Laissez-passer", lang), $"{Money(model.PassPrice)} — {L("every session, paid once", "toutes les séances, payées une fois", lang)} ({passesLine})" },
                { L("Pay per session", "À la séance", lang), $"{dropInPrice} — {L("pay only the days you play", "payez seulement les jours où vous jouez", lang)}" },
            });

            content += BuildButton("Choose my plan", "Choisir ma formule", $"{app}/plan-selection", lang);

            content += P(L(
                "Nothing chosen by the first session? You stay on pay-per-session, and can still switch while passes last.",
                "Rien de choisi avant la première séance ? Vous restez à la séance, et pouvez changer tant qu’il reste des laissez-passer.",
                lang));

            // What the season actually is, so nobody has to open the site to decide.
            var facts = new Dictionary<string, string?>
            {
                { L("Season", "Saison", lang), $"{model.SeasonName} · {model.StartDate.ToString("d MMM", culture)} – {model.EndDate.ToString("d MMM yyyy", culture)}" },
            };
            if (model.FirstSessionDate is DateTime first)
            {
                var time = string.IsNullOrWhiteSpace(model.FirstSessionEnd)
                    ? model.FirstSessionStart
                    : $"{model.FirstSessionStart}–{model.FirstSessionEnd}";
                facts[L("First session", "Première séance", lang)] = $"{first.ToString("dddd d MMMM", culture)} · {time}";
            }
            if (!string.IsNullOrWhiteSpace(model.Location)) facts[L("Where", "Où", lang)] = model.Location;
            if (model.SessionCount > 0) facts[L("Sessions this season", "Séances cette saison", lang)] = model.SessionCount.ToString(culture);
            content += BuildInfoBox(facts);

            content += P(L(
                "Passes are limited, and the count above is live. Reply to this email if you have a question.",
                "Les laissez-passer sont limités, et le compte ci-dessus est à jour. Répondez à ce courriel si vous avez une question.",
                lang));

            return BuildEmailLayout("Choose how you will play this season", "Choisissez votre formule pour la saison", content, lang);
        }

        /// <summary>
        /// Confirms the plan a player just picked for a season.
        ///
        /// The two plans need opposite endings: a season pass is not a spot until it is paid for, so
        /// that email asks for money and says what happens if it does not arrive; pay-per-session
        /// needs nothing, so that one asks for nothing and says so plainly.
        /// </summary>
        public static string GetPlanChoiceConfirmationEmail(PlanChoiceConfirmationEmailModel model, EmailLanguage lang = EmailLanguage.French)
        {
            var culture = GetCulture(lang);
            string Money(decimal amount) => amount.ToString("C", culture);
            var app = model.AppUrl.TrimEnd('/');
            var season = $"{model.SeasonName} · {model.StartDate.ToString("d MMM", culture)} – {model.EndDate.ToString("d MMM yyyy", culture)}";
            var isSeason = model.Plan == PaymentPlan.Season;

            var content = Greeting(model.FirstName, lang);

            if (!isSeason)
            {
                content += P(L("You are paying per session this season. Nothing to pay up front — you are billed for the sessions you play, and only those.",
                               "Vous payez à la séance cette saison. Rien à payer d'avance : vous êtes facturé pour les séances que vous jouez, et seulement celles-là.", lang));

                content += BuildInfoBox(new Dictionary<string, string?>
                {
                    { L("Your plan", "Votre formule", lang), L("Pay per session", "Paiement à la séance", lang) },
                    { L("Per session", "Par séance", lang), model.DropInPrice is decimal drop ? Money(drop) : null },
                    { L("Season", "Saison", lang), season },
                });

                content += P(model.SpotsLeft > 0
                    ? L($"Changed your mind? The season pass is {Money(model.PassPrice)} and {model.SpotsLeft} are still available.",
                        $"Vous changez d'avis ? Le laissez-passer est à {Money(model.PassPrice)} et il en reste {model.SpotsLeft}.", lang)
                    : L("The season pass is sold out, so pay-per-session is the way in for now.",
                        "Le laissez-passer est complet : le paiement à la séance est donc la façon de jouer pour l'instant.", lang));

                content += BuildButton("See my plan", "Voir ma formule", $"{app}/plan-selection", lang);

                return BuildEmailLayout("You are paying per session", "Vous payez à la séance", content, lang,
                    LSubject($"Pay-per-session confirmed for {model.SeasonName}. Nothing to pay now.",
                             $"Paiement à la séance confirmé pour {model.SeasonName}. Rien à payer maintenant.", lang));
            }

            // Season pass.
            content += P(model.AlreadyPaid
                ? L("You are on the season pass, and it is paid for. Every session this season is covered — just turn up.",
                    "Vous avez le laissez-passer de saison, et il est payé. Toutes les séances de la saison sont couvertes : venez jouer.", lang)
                : L("You have chosen the season pass. Your spot is held once the payment reaches us.",
                    "Vous avez choisi le laissez-passer de saison. Votre place est retenue dès que le paiement nous parvient.", lang));

            content += BuildInfoBox(new Dictionary<string, string?>
            {
                { L("Your plan", "Votre formule", lang), L("Season pass", "Laissez-passer de saison", lang) },
                { L("Price", "Prix", lang), Money(model.PassPrice) },
                { L("Season", "Saison", lang), season },
                { L("Status", "Statut", lang), model.AlreadyPaid
                    ? L("Paid", "Payé", lang)
                    : L("Awaiting payment", "En attente de paiement", lang) },
            });

            if (!model.AlreadyPaid)
            {
                content += BuildButton("Pay for my pass", "Payer mon laissez-passer", $"{app}/season-subscription", lang);
                content += BuildAlertBox(L(
                    "Until it is paid, the spot is held but not confirmed. If the season fills up, paid passes come first.",
                    "Tant qu'il n'est pas payé, la place est retenue mais non confirmée. Si la saison se remplit, les laissez-passer payés passent en premier.", lang), "warning");
            }

            content += P(L("Reply to this email if anything looks wrong.",
                           "Répondez à ce courriel si quelque chose ne va pas.", lang));

            return BuildEmailLayout("Your season pass", "Votre laissez-passer de saison", content, lang,
                LSubject(
                    model.AlreadyPaid
                        ? $"Season pass confirmed for {model.SeasonName}."
                        : $"Season pass chosen for {model.SeasonName} — {Money(model.PassPrice)} to pay.",
                    model.AlreadyPaid
                        ? $"Laissez-passer confirmé pour {model.SeasonName}."
                        : $"Laissez-passer choisi pour {model.SeasonName} — {Money(model.PassPrice)} à payer.",
                    lang));
        }

        public static string GetSeasonStatusUpdateEmail(string userName, string seasonName, string newStatus, string? reasonForChange = null, string? additionalInfo = null, EmailLanguage lang = EmailLanguage.French)
        {
            var content = Greeting(userName, lang) +
                P(L($"The status of the <strong>{seasonName}</strong> season has been updated:",
                     $"Le statut de la saison <strong>{seasonName}</strong> a été mis à jour:", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { L("Season", "Saison", lang), seasonName },
                    { L("New Status", "Nouveau statut", lang), newStatus }
                });

            if (!string.IsNullOrEmpty(reasonForChange))
                content += BuildAlertBox($"{L("Reason", "Raison", lang)}: {reasonForChange}", "info");
            if (!string.IsNullOrEmpty(additionalInfo))
                content += P(additionalInfo);

            return BuildEmailLayout("Season Status Update", "Mise à jour du statut de la saison", content, lang);
        }

        public static string GetSeasonUpdateEmail(string userName, string seasonName, string updateSubject, string updateDetails, string? actionLink = null, string? actionText = null, EmailLanguage lang = EmailLanguage.French)
        {
            var content = Greeting(userName, lang) +
                P(updateSubject) +
                BuildAlertBox(updateDetails, "info");

            if (!string.IsNullOrEmpty(actionLink) && !string.IsNullOrEmpty(actionText))
                content += BuildButton(actionText, actionText, actionLink, lang);

            return BuildEmailLayout("Season Update", "Mise à jour de la saison", content, lang);
        }

        public static string GetSeasonPaymentReminderEmail(string userName, string seasonName, decimal amountDue, string? paymentLink = null, string? reference = null, string? customMessage = null, EmailLanguage lang = EmailLanguage.French)
        {
            var content = Greeting(userName, lang) +
                P(L($"This is a reminder about your payment for the <strong>{seasonName}</strong> season.",
                     $"Ceci est un rappel concernant votre paiement pour la saison <strong>{seasonName}</strong>.", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { L("Season", "Saison", lang), seasonName },
                    { L("Amount Due", "Montant dû", lang), $"${amountDue:F2}" },
                    { L("Reference", "Référence", lang), reference }
                });

            if (!string.IsNullOrEmpty(customMessage))
                content += BuildAlertBox(customMessage, "info");

            content += P(L("Send an Interac e-Transfer to <strong>pay@sainthenribasketball.com</strong>",
                           "Envoyez un virement Interac à <strong>pay@sainthenribasketball.com</strong>", lang));

            if (!string.IsNullOrEmpty(paymentLink))
                content += BuildButton("Pay Online", "Payer en ligne", paymentLink, lang);

            return BuildEmailLayout("Season Payment Reminder", "Rappel de paiement pour la saison", content, lang);
        }
    }
    #endregion

    #region General
    public static class General
    {
        public static string GetAnnouncementEmail(string userName, string message, string? customMessage = null, EmailLanguage lang = EmailLanguage.French)
        {
            var content = Greeting(userName, lang) + P(message);
            if (!string.IsNullOrEmpty(customMessage))
                content += BuildDivider() + P(customMessage);
            return BuildEmailLayout("Announcement", "Annonce", content, lang);
        }

        public static string GetScheduleChangeEmail(string userName, string details, DateTime? newDate = null, TimeSpan? newTime = null, EmailLanguage lang = EmailLanguage.French)
        {
            var content = Greeting(userName, lang) +
                P(L("There has been a change to the schedule:", "Il y a eu un changement à l'horaire:", lang)) +
                P(details);

            if (newDate.HasValue || newTime.HasValue)
            {
                content += BuildInfoBox(new Dictionary<string, string?> {
                    { L("New Date", "Nouvelle date", lang), newDate?.ToString("dddd dd MMMM yyyy", GetCulture(lang)) },
                    { L("New Time", "Nouvelle heure", lang), newTime?.ToString(@"hh\:mm") }
                });
            }

            return BuildEmailLayout("Schedule Change", "Changement d'horaire", content, lang);
        }

        public static string GetFacilityUpdateEmail(string userName, string facilityName, string updateDetails, DateTime effectiveDate, string? alternativeFacility = null, EmailLanguage lang = EmailLanguage.French)
        {
            var content = Greeting(userName, lang) +
                P(L($"There is an update regarding <strong>{facilityName}</strong>:",
                     $"Il y a une mise à jour concernant <strong>{facilityName}</strong>:", lang)) +
                BuildAlertBox(updateDetails, "warning") +
                BuildInfoBox(new Dictionary<string, string?> {
                    { L("Effective Date", "Date d'effet", lang), effectiveDate.ToString("dd MMMM yyyy", GetCulture(lang)) }
                });

            if (!string.IsNullOrEmpty(alternativeFacility))
                content += BuildAlertBox($"{L("Alternative", "Alternative", lang)}: {alternativeFacility}", "info");

            return BuildEmailLayout("Facility Update", "Mise à jour des installations", content, lang);
        }

        public static string GetLowAttendanceWarningEmail(string userName, DateTime sessionDate, string startTime, string location, EmailLanguage lang = EmailLanguage.French) =>
            BuildEmailLayout("Low Attendance Warning", "Avertissement de faible présence",
                Greeting(userName, lang) +
                BuildAlertBox(L("Low attendance detected! The session may be cancelled if more players don't confirm.",
                                 "Faible présence détectée! La session pourrait être annulée si plus de joueurs ne confirment pas.", lang), "danger") +
                BuildInfoBox(new Dictionary<string, string?> {
                    { "Date", sessionDate.ToString("dddd dd MMMM yyyy", GetCulture(lang)) },
                    { L("Time", "Heure", lang), startTime },
                    { L("Location", "Lieu", lang), location }
                }) +
                BuildButton("Confirm Attendance", "Confirmer ma présence", "https://sainthenribasketball.com/attendance-confirmation", lang),
            lang);
    }
    #endregion

    #region Sessions
    public static class Sessions
    {
        /// <summary>
        /// Tells a player their session is off.
        ///
        /// The old version promised that a paid drop-in "will be applied to a future session", which is
        /// not always what happens: an unpaid one is cancelled, a paid one becomes account credit only
        /// when the admin asked for that, and otherwise the club still holds the money. Money is now
        /// described per player, from what actually happened, and the email offers the next session
        /// rather than ending on an apology.
        /// </summary>
        public static string GetSessionCancellationEmail(SessionCancellationEmailModel model, EmailLanguage lang = EmailLanguage.French)
        {
            var culture = GetCulture(lang);
            var money = culture.NumberFormat;
            string Money(decimal amount) => amount.ToString("C", culture);

            var when = model.SessionDate.ToString("dddd d MMMM yyyy", culture);
            var time = string.IsNullOrWhiteSpace(model.EndTime) ? model.StartTime : $"{model.StartTime}–{model.EndTime}";

            var content = Greeting(model.FirstName, lang) +
                P(model.WasWaiting
                    ? L("You were waiting for a place at this session, so here is the news first: it has been cancelled, and no place will come free.",
                        "Vous attendiez une place à cette séance : elle est annulée, et aucune place ne se libérera.",
                        lang)
                    : L("This session will not go ahead. Nothing is expected of you — here is where that leaves your place and your money.",
                        "Cette séance n’aura pas lieu. Rien n’est attendu de vous — voici ce qu’il advient de votre place et de votre argent.",
                        lang)) +
                BuildAlertBox($"<strong>{L("Cancelled", "Annulée", lang)}:</strong> {when} · {time}" +
                    (string.IsNullOrWhiteSpace(model.Location) ? "" : $" · {model.Location}"), "danger");

            if (!string.IsNullOrWhiteSpace(model.Reason))
                content += P($"<strong>{L("Reason", "Raison", lang)}:</strong> {model.Reason}");

            // Say exactly what happened to this player's money, or say nothing was owed.
            content += model.Money switch
            {
                CancellationMoney.Cancelled => BuildAlertBox(
                    L($"The {Money(model.Amount)} owed for this session has been cancelled. There is nothing to pay.",
                      $"Le montant de {Money(model.Amount)} dû pour cette séance a été annulé. Il n’y a rien à payer.", lang), "info"),
                CancellationMoney.Credited => BuildAlertBox(
                    L($"The {Money(model.Amount)} you paid is now credit on your account, and comes off your next session automatically.",
                      $"Les {Money(model.Amount)} que vous avez payés sont maintenant un crédit à votre compte, appliqué automatiquement à votre prochaine séance.", lang), "success"),
                CancellationMoney.StillHeld => BuildAlertBox(
                    L($"You paid {Money(model.Amount)} for this session and it has not been returned yet. Reply to this email and the club will sort it out with you.",
                      $"Vous avez payé {Money(model.Amount)} pour cette séance et ce montant n’a pas encore été remis. Répondez à ce courriel et le club s’en occupera avec vous.", lang), "warning"),
                CancellationMoney.CoveredByPass => P(
                    L("Your season pass covers every session, so this one costs you nothing.",
                      "Votre laissez-passer couvre toutes les séances : celle-ci ne vous coûte rien.", lang)),
                _ => P(L("You had not paid for this session, so there is nothing to settle.",
                         "Vous n’aviez pas payé cette séance : il n’y a rien à régler.", lang)),
            };

            // Somewhere to go next, rather than an apology and a dead end.
            if (model.NextSessionId is Guid nextId && model.NextSessionDate is DateTime nextDate)
            {
                var nextTime = string.IsNullOrWhiteSpace(model.NextEndTime) ? model.NextStartTime : $"{model.NextStartTime}–{model.NextEndTime}";
                content += BuildInfoBox(new Dictionary<string, string?> {
                    { L("Next session", "Prochaine séance", lang), $"{nextDate.ToString("dddd d MMMM", culture)} · {nextTime}" },
                    { L("Places left", "Places restantes", lang), model.NextSpotsLeft?.ToString(culture) },
                }) + BuildButton("Take a place at the next session", "Prendre une place à la prochaine séance",
                    $"{model.AppUrl.TrimEnd('/')}/sessions/{nextId}/book", lang);
            }
            else
            {
                content += BuildButton("See the schedule", "Voir le calendrier", $"{model.AppUrl.TrimEnd('/')}/schedule", lang);
            }

            content += P(L("Sorry for the change of plan. Reply to this email if anything looks wrong.",
                           "Désolé pour ce changement. Répondez à ce courriel si quelque chose ne va pas.", lang));

            return BuildEmailLayout("Session cancelled", "Séance annulée", content, lang);
        }

        /// <summary>
        /// A place has come free and is held for this player until a deadline.
        ///
        /// This one is read in a hurry, on a phone, against a clock — so the deadline is stated in
        /// plain words and again as a date, and there is exactly one thing to press.
        /// </summary>
        public static string GetWaitlistOfferEmail(
            string firstName, DateTime sessionDate, string startTime, string? endTime, string? location,
            DateTime offerExpiresLocal, string claimUrl, EmailLanguage lang = EmailLanguage.French)
        {
            var culture = GetCulture(lang);
            var deadline = offerExpiresLocal.ToString("dddd d MMMM, HH:mm", culture);
            var time = string.IsNullOrWhiteSpace(endTime) ? startTime : $"{startTime}–{endTime}";

            var content = Greeting(firstName, lang) +
                P(L("A place has come free at the session you were waiting for, and it is yours if you want it.",
                    "Une place s'est libérée pour la séance que vous attendiez, et elle est à vous si vous la voulez.", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { "Date", sessionDate.ToString("dddd d MMMM yyyy", culture) },
                    { L("Time", "Heure", lang), time },
                    { L("Where", "Où", lang), location ?? "717 Saint-Ferdinand" },
                }) +
                BuildAlertBox(L($"The place is held for you until <strong>{deadline}</strong> (Montreal). After that it goes to the next person waiting.",
                                $"La place vous est réservée jusqu'au <strong>{deadline}</strong> (Montréal). Passé ce délai, elle ira à la personne suivante.", lang), "warning") +
                BuildButton("Claim my place", "Réserver ma place", claimUrl, lang) +
                P(L("Nothing to do if you would rather not — the place simply passes on when the time runs out.",
                    "Rien à faire si vous préférez la laisser : la place passera simplement à quelqu'un d'autre à l'échéance.", lang));

            return BuildEmailLayout("A place is waiting for you", "Une place vous attend", content, lang,
                LSubject($"Held until {deadline} (Montreal).", $"Réservée jusqu'au {deadline} (Montréal).", lang));
        }
    }
    #endregion

    #region Admin
    public static class Admin
    {
        public static string GetAdminNotificationEmail(string adminName, string subject, string message, string? actionLink = null, string? actionText = null)
        {
            var content = Greeting(adminName, EmailLanguage.French) + P(message);
            if (!string.IsNullOrEmpty(actionLink) && !string.IsNullOrEmpty(actionText))
                content += BuildButton(actionText, actionText, actionLink);
            return BuildEmailLayout(subject, subject, content);
        }

        public static string GetNewUserNotificationEmail(string adminName, string newUserName, string newUserEmail, DateTime registrationDate, string? userPlan = null) =>
            BuildEmailLayout("Nouvel utilisateur inscrit", "Nouvel utilisateur inscrit",
                Greeting(adminName, EmailLanguage.French) +
                P("Un nouvel utilisateur s'est inscrit sur la plateforme.") +
                BuildInfoBox(new Dictionary<string, string?> {
                    { "Nom", newUserName },
                    { "Email", newUserEmail },
                    { "Date d'inscription", registrationDate.ToString("dd MMMM yyyy à HH:mm", new System.Globalization.CultureInfo("fr-CA")) },
                    { "Forfait", userPlan }
                }) +
                BuildButton("Voir les utilisateurs", "Voir les utilisateurs", "https://sainthenribasketball.com/admin/users"));
    }
    #endregion
}
