using SaintHenriBasketball.Application.DTOs.Session;
using SaintHenriBasketball.Domain.Enums;
using System.Globalization;

namespace SaintHenriBasketball.Application.Templates;

public static class EmailTemplates
{
    private static class Styles
    {
        // Primary brand color (orange)
        public const string PrimaryColor = "#FF6B1A";

        // Secondary colors
        public const string SecondaryColor = "#4A4A4A";
        public const string AccentColor = "#3b82f6"; // Blue for buttons
        public const string SuccessColor = "#22c55e"; // Green for positive actions
        public const string DangerColor = "#ef4444"; // Red for negative actions

        // Basic elements
        public const string Container = "max-width: 600px; margin: 0 auto; padding: 20px; font-family: Arial, sans-serif;";
        public const string Header = $"color: {SecondaryColor}; margin-bottom: 20px; font-size: 20px; font-weight: bold;";
        public const string Content = "color: #333333; line-height: 1.6; font-size: 16px;";

        // UI elements
        public const string InfoBox = "background-color: #f9fafb; padding: 20px; border-radius: 8px; margin: 20px 0; border: 1px solid #e5e7eb;";
        public const string Button = $"display: inline-block; padding: 12px 24px; background-color: {AccentColor}; color: white; text-decoration: none; border-radius: 4px; margin: 10px 0; text-align: center; font-weight: 500;";
        public const string SuccessButton = $"display: inline-block; padding: 12px 24px; background-color: {SuccessColor}; color: white; text-decoration: none; border-radius: 4px; margin: 10px 0; text-align: center; font-weight: 500;";
        public const string DangerButton = $"display: inline-block; padding: 12px 24px; background-color: {DangerColor}; color: white; text-decoration: none; border-radius: 4px; margin: 10px 0; text-align: center; font-weight: 500;";

        // Table styles
        public const string Table = "width: 100%; border-collapse: collapse; margin: 15px 0;";
        public const string TableHead = "background-color: #f3f4f6; font-weight: bold;";
        public const string TableCell = "padding: 12px; border: 1px solid #e5e7eb;";

        // Logo and image styles
        public const string Logo = "width: 120px; height: auto; margin: 0 auto 20px auto;";
        public const string LogoContainer = "text-align: center; margin-bottom: 30px;";
        public const string PrimaryButton = "display: inline-block; padding: 15px 30px; background-color: #FF6B1A; color: white; text-decoration: none; border-radius: 6px; margin: 15px 0; text-align: center; font-weight: bold; box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1); transition: background-color 0.3s;";
    }

    private static readonly string Logo = Path.Combine("https://sainthenribasketball.com/", "logo.png");

    private static readonly CultureInfo FrenchCulture = new("fr-CA");

    private static string BuildEmailLayout(string title, string content) =>
        $@"<!DOCTYPE html>
        <html lang='fr'>
        <head>
            <meta charset='utf-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <title>{title} - Saint Henri Basketball</title>
        </head>
        <body style='margin: 0; padding: 0; background-color: #f5f5f5;'>
            <div style='{Styles.Container}'>
                <div style='text-align: center; background-color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px;'>
                    <h1 style='{Styles.Header}'>{title}</h1>
                    <div style='{Styles.LogoContainer}'>
                        <img src='{Logo}' 
                             alt='Saint Henri Basketball' 
                             style='{Styles.Logo}'>
                    </div>
                </div>
                <div style='background-color: white; padding: 20px; border-radius: 8px;'>
                    {content}
                </div>
                <div style='margin-top: 30px; text-align: center; color: #666; font-size: 12px;'>
                    <p>Saint Henri Basketball</p>
                    <p>{DateTime.Now.Year} © Tous droits réservés</p>
                </div>
            </div>
        </body>
        </html>";

    public static class Authentication
    {
        public static string GetConfirmationEmail(string userName, string confirmationLink) =>
            BuildEmailLayout(
                "Confirmez votre adresse courriel",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                <p style='{Styles.Content}'>Nous sommes ravis de vous accueillir dans notre communauté de basketball. Pour commencer, veuillez confirmer votre adresse courriel:</p>
                <div style='text-align: center;'>
                    <a href='{confirmationLink}' style='{Styles.Button}'>Confirmer le courriel</a>
                </div>
                <p style='{Styles.Content}'>Ou copiez et collez ce lien dans votre navigateur:</p>
                <p style='{Styles.Content}'>{confirmationLink}</p>"
            );

        public static string GetPasswordResetEmail(string userName, string resetLink) =>
            BuildEmailLayout(
                "Réinitialisation du mot de passe",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                <p style='{Styles.Content}'>Nous avons reçu une demande de réinitialisation de votre mot de passe:</p>
                <div style='text-align: center;'>
                    <a href='{resetLink}' style='{Styles.Button}'>Réinitialiser le mot de passe</a>
                </div>
                <p style='{Styles.Content}'>Ce lien expirera dans 1 heure.</p>
                <p style='{Styles.Content}'>Si vous n'avez pas fait cette demande, veuillez ignorer ce courriel.</p>"
            );
    }

    public static class Payments
    {
        public static string GetPaymentCreatedEmail(string userName, decimal amount, string reference) =>
            BuildEmailLayout(
                "Demande de paiement",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Détails du paiement</h2>
                    <table style='{Styles.Table}'>
                        <tr>
                            <td style='{Styles.TableCell}'>Montant dû:</td>
                            <td style='{Styles.TableCell}'>{amount:C}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Référence:</td>
                            <td style='{Styles.TableCell}'>{reference}</td>
                        </tr>
                    </table>
                </div>
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Instructions de paiement</h2>
                    <ol style='{Styles.Content}'>
                        <li>Envoyez un virement Interac à: <strong>pay@sainthenribasketball.com</strong></li>
                        <li>Incluez votre numéro de référence: <strong>{reference}</strong></li>
                        <li>Utilisez votre nom complet dans le message</li>
                    </ol>
                </div>"
            );

        public static string GetPaymentConfirmationEmail(string userName, decimal amount, string reference, DateTime date) =>
            BuildEmailLayout(
                "Confirmation de paiement",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Détails de la transaction</h2>
                    <table style='{Styles.Table}'>
                        <tr>
                            <td style='{Styles.TableCell}'>Montant payé:</td>
                            <td style='{Styles.TableCell}'>{amount:C}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Référence:</td>
                            <td style='{Styles.TableCell}'>{reference}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Date:</td>
                            <td style='{Styles.TableCell}'>{date.ToString("dd MMMM yyyy HH:mm", FrenchCulture)}</td>
                        </tr>
                    </table>
                </div>
                <p style='{Styles.Content}'>Merci pour votre paiement. Votre reçu est joint à ce courriel.</p>"
            );

        public static string GetPaymentReminderEmail(string userName, decimal amount, string? customMessage = null) =>
            BuildEmailLayout(
                "Rappel de paiement",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
        <div style='{Styles.InfoBox}'>
            <h2 style='{Styles.Header}'>Rappel de paiement</h2>
                    <p style='{Styles.Content}'>Un paiement de {amount:C} est en attente.</p>
            {(!string.IsNullOrEmpty(customMessage) ? $"<p style='{Styles.Content}'>{customMessage}</p>" : "")}
        </div>
        <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Instructions de paiement</h2>
                    <p style='{Styles.Content}'>
                        Veuillez effectuer le paiement par virement Interac à:<br>
                        <strong>pay@sainthenribasketball.com</strong>
                    </p>
                </div>"
            );
    }

    public static class Attendance
    {
        public static string GetAttendanceConfirmationEmail(
            string userName,
            DateTime sessionDate,
            string startTime,
            string endTime,
            string? location,
            bool isAttending,
            string? notes = null) =>
            BuildEmailLayout(
                "Confirmation de présence",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Détails de la session</h2>
                    <table style='{Styles.Table}'>
                        <tr>
                            <td style='{Styles.TableCell}'>Date:</td>
                            <td style='{Styles.TableCell}'>{sessionDate.ToString("dddd dd MMMM yyyy", FrenchCulture)}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Heure:</td>
                            <td style='{Styles.TableCell}'>{startTime:HH\\:mm} - {endTime:HH\\:mm}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Lieu:</td>
                            <td style='{Styles.TableCell}'>{location}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Statut:</td>
                            <td style='{Styles.TableCell}'>{(isAttending ? "Présent" : "Absent")}</td>
                        </tr>
                    </table>
                    {(!string.IsNullOrEmpty(notes) ? $@"
                        <div style='margin-top: 15px;'>
                            <h3 style='{Styles.Header}'>Notes:</h3>
                            <p style='{Styles.Content}'>{notes}</p>
                        </div>"
                    : "")}
                </div>"
            );

        public static string GetAttendanceReminderEmail(
            Guid sessionId,
            Guid userId,
            DateTime sessionDate,
            string userName,
            string startTime,
            string endTime,
            string? location,
            string? customMessage = null)
        {
            var sessionDateStr = sessionDate.ToString("dddd dd MMMM yyyy", FrenchCulture);

            return BuildEmailLayout(
                    "Rappel de présence",
                    $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
        
        <div style='{Styles.InfoBox}'>
            <h2 style='{Styles.Header}'>Détails de la session - {sessionDateStr}</h2>
            <table style='{Styles.Table}'>
                <tr>
                    <td style='{Styles.TableCell}'><strong>Date:</strong></td>
                    <td style='{Styles.TableCell}'>{sessionDateStr}</td>
                </tr>
                <tr>
                    <td style='{Styles.TableCell}'><strong>Heure:</strong></td>
                    <td style='{Styles.TableCell}'>{startTime} - {endTime}</td>
                </tr>
                <tr>
                    <td style='{Styles.TableCell}'><strong>Lieu:</strong></td>
                    <td style='{Styles.TableCell}'>{location}</td>
                </tr>
            </table>
        </div>

        {(!string.IsNullOrEmpty(customMessage) ?
                        $@"<div style='{Styles.InfoBox}'>
                <p style='{Styles.Content}'>{customMessage}</p>
            </div>"
                        : "")}

        <div style='{Styles.InfoBox}'>
            <h2 style='{Styles.Header}'>À ne pas oublier</h2>
            <ul style='{Styles.Content}'>
                <li>Bouteille d'eau</li>
                <li>Chaussures de sport propres</li>
                <li>Serviette</li>
            </ul>
            <p style='{Styles.Content}'>Arrivez 10-15 minutes à l'avance pour échauffement</p>
        </div>

        <!-- Improved button design to match the screenshot -->
        <table style='width: 100%; border-collapse: collapse; margin-top: 30px;'>
            <tr>
                <td style='width: 50%; padding: 0;'>
                    <a href='https://sainthenribasketball.com/attendance/confirm?sessionId={sessionId}&userId={userId}&attending=true' 
                       style='display: block; background-color: #4CAF50; color: white; padding: 15px 0; text-decoration: none; text-align: center; font-weight: normal; font-size: 16px; border-top-left-radius: 5px; border-bottom-left-radius: 5px;'>
                        J'y serai ✓
                    </a>
                </td>
                <td style='width: 50%; padding: 0;'>
                    <a href='https://sainthenribasketball.com/attendance/confirm?sessionId={sessionId}&userId={userId}&attending=false' 
                       style='display: block; background-color: #E57373; color: white; padding: 15px 0; text-decoration: none; text-align: center; font-weight: normal; font-size: 16px; border-top-right-radius: 5px; border-bottom-right-radius: 5px;'>
                        Je ne pourrai pas y être ✗
                    </a>
                </td>
            </tr>
        </table>"
                );
        }
    }

    public static class Season
    {
        public static string GetSeasonRegistrationConfirmationEmail(string userName, DateTime startDate, DateTime endDate, decimal price) =>
            BuildEmailLayout(
                "Inscription à la saison confirmée",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Informations sur la saison</h2>
                    <table style='{Styles.Table}'>
                        <tr>
                            <td style='{Styles.TableCell}'>Date de début:</td>
                            <td style='{Styles.TableCell}'>{startDate.ToString("dd MMMM yyyy", FrenchCulture)}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Date de fin:</td>
                            <td style='{Styles.TableCell}'>{endDate.ToString("dd MMMM yyyy", FrenchCulture)}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Prix:</td>
                            <td style='{Styles.TableCell}'>{price:C}</td>
                        </tr>
                    </table>
                </div>
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Instructions de paiement</h2>
                    <ul style='{Styles.Content}'>
                        <li>Envoyer à: <strong>pay@sainthenribasketball.com</strong></li>
                        <li>Montant: <strong>{price:C}</strong></li>
                        <li>Message: Inscription saison - {userName}</li>
                    </ul>
                </div>"
            );

        public static string GetSeasonCancellationEmail(string userName, DateTime startDate, DateTime endDate) =>
            BuildEmailLayout(
                "Annulation de l'inscription à la saison",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Détails de l'annulation</h2>
                    <table style='{Styles.Table}'>
                        <tr>
                            <td style='{Styles.TableCell}'>Saison:</td>
                            <td style='{Styles.TableCell}'>{startDate.ToString("dd MMMM yyyy", FrenchCulture)} - {endDate.ToString("dd MMMM yyyy", FrenchCulture)}</td>
                        </tr>
                    </table>
                    <p style='{Styles.Content}'>Votre inscription à la saison a été annulée. Si vous pensez qu'il s'agit d'une erreur, veuillez nous contacter.</p>
                </div>"
            );
    }

    public static class General
    {
        public static string GetAnnouncementEmail(string userName, string message, string? customMessage = null) =>
            BuildEmailLayout(
                "Annonce importante",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Annonce</h2>
                    <p style='{Styles.Content}'>{message}</p>
                    {(!string.IsNullOrEmpty(customMessage) ? $@"
                        <div style='margin-top: 15px; padding-top: 15px; border-top: 1px solid #e0e0e0;'>
                            <p style='{Styles.Content}'>{customMessage}</p>
                        </div>"
                    : "")}
                </div>"
            );

        public static string GetScheduleChangeEmail(string userName, string details, DateTime? newDate = null, TimeSpan? newTime = null) =>
            BuildEmailLayout(
                "Changement d'horaire",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Changement à l'horaire</h2>
                    <p style='{Styles.Content}'>{details}</p>
                    {(newDate.HasValue || newTime.HasValue ? $@"
                        <table style='{Styles.Table}'>
                            {(newDate.HasValue ? $@"
                                <tr>
                                    <td style='{Styles.TableCell}'>Nouvelle date:</td>
                                    <td style='{Styles.TableCell}'>{newDate.Value.ToString("dddd dd MMMM yyyy", FrenchCulture)}</td>
                                </tr>"
                            : "")}
                            {(newTime.HasValue ? $@"
                                <tr>
                                    <td style='{Styles.TableCell}'>Nouvel horaire:</td>
                                    <td style='{Styles.TableCell}'>{newTime.Value:HH\\:mm}</td>
                                </tr>"
                            : "")}
                        </table>"
                    : "")}
                </div>"
            );
    }

    public static class Sessions
    {
        public static string GetSessionCancellationEmail(
            string userName,
            DateTime sessionDate,
            string startTime,
            string? location,
            string? cancellationReason = null,
            SessionDto? alternativeSession = null)
        {
            var sessionDateStr = sessionDate.ToString("dddd dd MMMM yyyy", FrenchCulture);

            return BuildEmailLayout(
                "Session annulée",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
            
            <div style='{Styles.InfoBox}'>
                <h2 style='{Styles.Header}'>Annulation de session</h2>
                <p style='{Styles.Content}'>Nous regrettons de vous informer que la séance suivante a été <strong>annulée</strong> :</p>
                <table style='{Styles.Table}'>
                    <tr>
                        <td style='{Styles.TableCell}'>Date:</td>
                        <td style='{Styles.TableCell}'>{sessionDateStr}</td>
                    </tr>
                    <tr>
                        <td style='{Styles.TableCell}'>Heure:</td>
                        <td style='{Styles.TableCell}'>{startTime}</td>
                    </tr>
                    <tr>
                        <td style='{Styles.TableCell}'>Lieu:</td>
                        <td style='{Styles.TableCell}'>{location}</td>
                    </tr>
                </table>
                
                {(!string.IsNullOrEmpty(cancellationReason) ?
                        $@"<div style='margin-top: 15px; padding: 15px; background-color: #fef2f2; border-radius: 5px;'>
                        <p style='margin: 0; color: #991b1b;'><strong>Raison de l'annulation:</strong> {cancellationReason}</p>
                    </div>"
                        : "")}
            </div>
            
            {(alternativeSession != null ?
                    $@"<div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Session alternative disponible</h2>
                    <p style='{Styles.Content}'>Nous vous invitons à vous inscrire à une session alternative:</p>
                    <table style='{Styles.Table}'>
                        <tr>
                            <td style='{Styles.TableCell}'>Date:</td>
                            <td style='{Styles.TableCell}'>{alternativeSession.SessionDate.ToString("dddd dd MMMM yyyy", FrenchCulture)}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Heure:</td>
                            <td style='{Styles.TableCell}'>{alternativeSession.StartTime} - {alternativeSession.EndTime}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Lieu:</td>
                            <td style='{Styles.TableCell}'>{alternativeSession.Location}</td>
                        </tr>
                    </table>
                    <div style='text-align: center; margin-top: 20px;'>
                        <a href='https://sainthenribasketball.com/session/{alternativeSession.Id}/register' 
                           style='background-color: #3b82f6; color: white; padding: 12px 25px; text-decoration: none; border-radius: 3px; display: inline-block;'>
                            S'inscrire à cette session
                        </a>
                    </div>
                </div>"
                    : "")}
                
            <div style='{Styles.InfoBox}'>
                <p style='{Styles.Content}'>Nous nous excusons pour tout inconvénient que cette annulation pourrait causer.</p>
                <p style='{Styles.Content}'>Si vous avez des questions, n'hésitez pas à nous contacter:</p>
                <ul style='{Styles.Content}'>
                    <li>Téléphone: (438) 935-8129</li>
                    <li>Courriel: info@sainthenribasketball.com</li>
                </ul>
            </div>"
            );
        }
    }
}