using SaintHenriBasketball.Application.Helpers;
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
                P(L($"Or copy and paste this link in your browser:<br/><span style='font-size:12px;color:#6b7280;word-break:break-all;'>{confirmationLink}</span>",
                     $"Ou copiez et collez ce lien dans votre navigateur:<br/><span style='font-size:12px;color:#6b7280;word-break:break-all;'>{confirmationLink}</span>", lang)),
            lang);

        public static string GetPasswordResetEmail(string userName, string resetLink, EmailLanguage lang = EmailLanguage.French) =>
            BuildEmailLayout("Reset Your Password", "Réinitialisation du mot de passe",
                Greeting(userName, lang) +
                P(L("We received a request to reset your password:", "Nous avons reçu une demande de réinitialisation de votre mot de passe:", lang)) +
                BuildButton("Reset Password", "Réinitialiser le mot de passe", resetLink, lang) +
                BuildAlertBox(L("This link expires in 1 hour. If you didn't request this, you can safely ignore this email.",
                                 "Ce lien expire dans 1 heure. Si vous n'avez pas fait cette demande, vous pouvez ignorer ce courriel.", lang), "warning"),
            lang);

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
            lang);
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

            var content = Greeting(userName, lang) +
                P(L($"This is a reminder that your payment for the <strong>{planName}</strong> is due.",
                     $"Ceci est un rappel que votre paiement pour le <strong>{planName}</strong> est dû.", lang)) +
                BuildInfoBox(new Dictionary<string, string?> {
                    { L("Plan", "Forfait", lang), planName },
                    { L("Amount", "Montant", lang), $"${amount:F2}" }
                });

            if (!string.IsNullOrEmpty(customMessage))
                content += BuildAlertBox(customMessage, "info");

            // Stripe payment buttons
            var stripeUrl = plan == PaymentPlan.Season
                ? "https://buy.stripe.com/28o6pW5ANh1q4VOdQQ"
                : "https://buy.stripe.com/14k15C6EReTi5ZS7st";

            content += BuildButton("Pay Online", "Payer en ligne", stripeUrl, lang) +
                P(L("Or send an Interac e-Transfer to <strong>pay@sainthenribasketball.com</strong>",
                     "Ou envoyez un virement Interac à <strong>pay@sainthenribasketball.com</strong>", lang));

            return BuildEmailLayout("Payment Reminder", "Rappel de paiement", content, lang);
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

        public static string GetAttendanceReminderEmail(Guid userId, Guid sessionId, DateTime sessionDate, string userName, string startTime, string endTime, string? location = null, string? customMessage = null, EmailLanguage lang = EmailLanguage.French)
        {
            var confirmUrl = $"https://sainthenribasketball.com/attendance/confirm?sessionId={sessionId}&userId={userId}&attending=true";

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
                BuildButton("I'll Be There!", "J'y serai!", confirmUrl, lang);

            return BuildEmailLayout("Session Reminder", "Rappel de session", content, lang);
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
        public static string GetSessionCancellationEmail(string userName, DateTime sessionDate, string startTime, string? location = null, string? cancellationReason = null, Guid? alternativeSessionId = null, EmailLanguage lang = EmailLanguage.French)
        {
            var content = Greeting(userName, lang) +
                P(L(
                    "We're sorry to let you know that the upcoming Saint-Henri Basketball session has been cancelled.",
                    "Nous sommes désolés de vous informer que la prochaine séance de basketball Saint-Henri a été annulée.",
                    lang)) +
                BuildAlertBox(L("Session cancelled", "Séance annulée", lang), "danger") +
                BuildInfoBox(new Dictionary<string, string?> {
                    { "Date", sessionDate.ToString("dddd dd MMMM yyyy", GetCulture(lang)) },
                    { L("Time", "Heure", lang), startTime },
                    { L("Location", "Lieu", lang), location ?? "717 Saint-Ferdinand Street" }
                });

            if (!string.IsNullOrEmpty(cancellationReason))
                content += P($"<strong>{L("Reason", "Raison", lang)}:</strong> {cancellationReason}");

            if (alternativeSessionId.HasValue)
                content += BuildAlertBox(L("An alternative session is available — tap below to register.", "Une séance alternative est disponible — réservez votre place ci-dessous.", lang), "info") +
                    BuildButton("View Alternative", "Voir l'alternative", $"https://sainthenribasketball.com/session/{alternativeSessionId}/register", lang);

            content += P(L(
                "If you had paid for this drop-in, your payment will be applied to a future session — no action needed on your end.",
                "Si vous aviez payé une séance à la pièce, votre paiement sera reporté à une séance future — aucune action requise de votre part.",
                lang));

            return BuildEmailLayout("Session Cancelled", "Séance annulée", content, lang);
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
