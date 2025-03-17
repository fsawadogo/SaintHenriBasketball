using SaintHenriBasketball.Application.DTOs.Session;
using SaintHenriBasketball.Domain.Enums;
using System.Globalization;

namespace SaintHenriBasketball.Application.Templates;

public static class EmailTemplates
{
    #region Styles
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
        public const string Button = $"display: inline-block; padding: 12px 24px; background-color: {PrimaryColor}; color: white; text-decoration: none; border-radius: 4px; margin: 10px 0; text-align: center; font-weight: 500;";
        public const string SuccessButton = $"display: inline-block; padding: 12px 24px; background-color: {PrimaryColor}; color: white; text-decoration: none; border-radius: 4px; margin: 10px 0; text-align: center; font-weight: 500;";
        public const string DangerButton = $"display: inline-block; padding: 12px 24px; background-color: {PrimaryColor}; color: white; text-decoration: none; border-radius: 4px; margin: 10px 0; text-align: center; font-weight: 500;";

        // Table styles
        public const string Table = "width: 100%; border-collapse: collapse; margin: 15px 0;";
        public const string TableHead = "background-color: #f3f4f6; font-weight: bold;";
        public const string TableCell = "padding: 12px; border: 1px solid #e5e7eb;";

        // Logo and image styles
        public const string Logo = "width: 120px; height: auto; margin: 0 auto 20px auto;";
        public const string LogoContainer = "text-align: center; margin-bottom: 30px;";
        public const string PrimaryButton = "display: inline-block; padding: 15px 30px; background-color: #FF6B1A; color: white; text-decoration: none; border-radius: 6px; margin: 15px 0; text-align: center; font-weight: bold; box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1); transition: background-color 0.3s;";
    }
    #endregion

    #region Constants
    private static readonly string Logo = "https://sainthenribasketball.com/logo.png";
    private static readonly CultureInfo FrenchCulture = new("fr-CA");
    #endregion

    #region Helper methods
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
                    <p>{DateTime.UtcNow.Year} © Tous droits réservés</p>
                </div>
            </div>
        </body>
        </html>";
    #endregion

    #region Authentication
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

        public static string GetAccountCreatedEmail(string userName, string password, string loginLink) =>
            BuildEmailLayout(
                "Votre compte a été créé",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                <p style='{Styles.Content}'>Bienvenue dans la communauté Saint Henri Basketball! Votre compte a été créé avec succès.</p>
                
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Informations de connexion</h2>
                    <p style='{Styles.Content}'><strong>Nom d'utilisateur:</strong> {userName}</p>
                    <p style='{Styles.Content}'><strong>Mot de passe temporaire:</strong> {password}</p>
                    <p style='{Styles.Content}'>Veuillez changer votre mot de passe après votre première connexion.</p>
                </div>
                
                <div style='text-align: center;'>
                    <a href='{loginLink}' style='{Styles.PrimaryButton}'>Se connecter</a>
                </div>
                
                <p style='{Styles.Content}'>Si vous avez des questions, n'hésitez pas à nous contacter à info@sainthenribasketball.com.</p>"
            );
    }
    #endregion

    #region Payments
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

        public static string GetPaymentReminderEmail(string userName, decimal amount, PaymentPlan paymentPlan, string? customMessage = null, string? reference = null)
        {
            // Set the appropriate Stripe payment link based on the user's plan
            string stripePaymentLink = paymentPlan == PaymentPlan.Season
                ? "https://buy.stripe.com/28o6pW5ANh1q4VOdQQ"  // Season plan link
                : "https://buy.stripe.com/14k15C6EReTi5ZS7st"; // Drop-in link

            string planName = paymentPlan == PaymentPlan.Season ? "Saison" : "Par Séance";

            return BuildEmailLayout(
                "Rappel de paiement",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
        
        <div style='{Styles.InfoBox}'>
            <h2 style='{Styles.Header}'>Rappel de paiement</h2>
            <p style='{Styles.Content}'>Nous vous rappelons qu'un paiement de <strong>{amount:C}</strong> est en attente pour votre forfait <strong>{planName}</strong>.</p>
            {(!string.IsNullOrEmpty(reference) ? $"<p style='{Styles.Content}'>Référence: <strong>{reference}</strong></p>" : "")}
            {(!string.IsNullOrEmpty(customMessage) ? $"<p style='{Styles.Content}'>{customMessage}</p>" : "")}
        </div>
        
        <div style='{Styles.InfoBox}'>
            <h2 style='{Styles.Header}'>Options de paiement</h2>
            
            <p style='{Styles.Content}'><strong>Option 1: Paiement par carte</strong></p>
            <div style='text-align: center; margin: 20px 0;'>
                                            <a href='{stripePaymentLink}' 
                   style='background-color: #FF6B1A; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;'>
                    Payer maintenant
                </a>
            </div>
            
            <p style='{Styles.Content}'><strong>Option 2: Virement Interac (recommandé)</strong></p>
            <ol style='{Styles.Content}'>
                <li>Envoyez un virement Interac à: <strong>pay@sainthenribasketball.com</strong></li>
                {(!string.IsNullOrEmpty(reference) ? $"<li>Incluez votre numéro de référence: <strong>{reference}</strong></li>" : "")}
                <li>Utilisez votre nom complet dans le message</li>
            </ol>
        </div>

        <p style='{Styles.Content}'>Votre paiement nous permettra de continuer à fournir des sessions de qualité. Merci pour votre ponctualité!</p>
        
        <p style='{Styles.Content}'>Si vous avez des questions concernant ce paiement, n'hésitez pas à nous contacter à <a href='mailto:info@sainthenribasketball.com'>info@sainthenribasketball.com</a>.</p>"
            );
        }

        public static string GetPaymentPlanUpdateEmail(string userName, PaymentPlan newPlan, decimal newAmount, DateTime effectiveDate, string? additionalInfo = null)
        {
            string planName = newPlan == PaymentPlan.Season ? "Saison" : "Par Séance";

            return BuildEmailLayout(
                "Mise à jour de votre forfait",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Changement de forfait</h2>
                    <p style='{Styles.Content}'>Votre forfait a été modifié avec succès.</p>
                    
                    <table style='{Styles.Table}'>
                        <tr>
                            <td style='{Styles.TableCell}'><strong>Nouveau forfait:</strong></td>
                            <td style='{Styles.TableCell}'>{planName}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'><strong>Nouveau tarif:</strong></td>
                            <td style='{Styles.TableCell}'>{newAmount:C}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'><strong>Date d'effet:</strong></td>
                            <td style='{Styles.TableCell}'>{effectiveDate.ToString("dd MMMM yyyy", FrenchCulture)}</td>
                        </tr>
                    </table>
                    
                    {(!string.IsNullOrEmpty(additionalInfo) ? $@"
                    <div style='margin-top: 15px; padding: 15px; background-color: #f0f9ff; border-radius: 5px;'>
                        <p style='margin: 0; color: #0369a1;'>{additionalInfo}</p>
                    </div>" : "")}
                </div>
                
                <p style='{Styles.Content}'>Si ce changement n'est pas ce que vous attendiez ou si vous avez des questions, n'hésitez pas à nous contacter à <a href='mailto:info@sainthenribasketball.com'>info@sainthenribasketball.com</a>.</p>"
            );
        }
    }
    #endregion

    #region Attendance
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
                            <td style='{Styles.TableCell}'>10:00 - {endTime}</td>
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
            var rawDateStr = sessionDate.ToString("dddd dd MMMM yyyy", FrenchCulture);
            var sessionDateStr = FrenchCulture.TextInfo.ToTitleCase(rawDateStr);

            return BuildEmailLayout(
                    "Rappel de présence",
                    $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
        
        <div style='{Styles.InfoBox}'>
            <h2 style='{Styles.Header}'>Détails de la session: {sessionDateStr}</h2>
            <table style='{Styles.Table}'>
                <tr>
                    <td style='{Styles.TableCell}'><strong>Date:</strong></td>
                    <td style='{Styles.TableCell}'>{sessionDateStr}</td>
                </tr>
                <tr>
                    <td style='{Styles.TableCell}'><strong>Heure:</strong></td>
                    <td style='{Styles.TableCell}'>10:00 - {endTime}</td>
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

        <table style='width: 100%; border-collapse: collapse; margin-top: 30px;'>
            <tr>
                <td style='width: 100%; padding: 0;'>
                    <a href='https://sainthenribasketball.com/attendance-confirmation' 
                       style='display: block; background-color: #FF6B1A; color: white; padding: 15px 0; text-decoration: none; text-align: center; font-weight: normal; font-size: 16px; border-top-left-radius: 5px; border-bottom-left-radius: 5px;'>
                        J'y serai ✓
                    </a>
                </td>
            </tr>
        </table>"
                );
        }

        public static string GetAttendanceUpdateEmail(
            string userName,
            DateTime sessionDate,
            string startTime,
            string endTime,
            string? location,
            bool previousStatus,
            bool newStatus,
            string? reason = null) =>
            BuildEmailLayout(
                "Mise à jour de présence",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Mise à jour de votre statut de présence</h2>
                    <p style='{Styles.Content}'>Votre statut de présence pour la session suivante a été modifié:</p>
                    <table style='{Styles.Table}'>
                        <tr>
                            <td style='{Styles.TableCell}'>Date:</td>
                            <td style='{Styles.TableCell}'>{sessionDate.ToString("dddd dd MMMM yyyy", FrenchCulture)}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Heure:</td>
                            <td style='{Styles.TableCell}'>10:00 - {endTime}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Lieu:</td>
                            <td style='{Styles.TableCell}'>{location}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Ancien statut:</td>
                            <td style='{Styles.TableCell}'>{(previousStatus ? "Présent" : "Absent")}</td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Nouveau statut:</td>
                            <td style='{Styles.TableCell}'><strong>{(newStatus ? "Présent" : "Absent")}</strong></td>
                        </tr>
                    </table>
                    
                    {(!string.IsNullOrEmpty(reason) ? $@"
                    <div style='margin-top: 15px; padding: 15px; background-color: #f0f9ff; border-radius: 5px;'>
                        <p style='margin: 0; color: #0369a1;'><strong>Raison du changement:</strong> {reason}</p>
                    </div>" : "")}
                </div>
                
                <p style='{Styles.Content}'>Si vous avez des questions concernant cette mise à jour, veuillez nous contacter à <a href='mailto:info@sainthenribasketball.com'>info@sainthenribasketball.com</a>.</p>"
            );
    }
    #endregion

    #region Seasons
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

        public static string GetSeasonRegistrationReminderEmail(string userName, string seasonName, DateTime startDate, DateTime endDate, decimal price, string registrationLink, string? customMessage = null) =>
            BuildEmailLayout(
                "Rappel d'inscription à la saison",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Inscription à la saison - Rappel</h2>
                    <p style='{Styles.Content}'>Nous vous rappelons que les inscriptions pour la saison <strong>{seasonName}</strong> sont en cours.</p>
                    
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
                    
                    {(!string.IsNullOrEmpty(customMessage) ? $@"
                    <div style='margin-top: 15px; padding: 15px; background-color: #f0f9ff; border-radius: 5px;'>
                        <p style='margin: 0; color: #0369a1;'>{customMessage}</p>
                    </div>" : "")}
                </div>
                
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{registrationLink}' style='{Styles.PrimaryButton}'>
                        S'inscrire maintenant
                    </a>
                </div>
                
                <p style='{Styles.Content}'>Ne manquez pas cette opportunité de rejoindre notre communauté pour cette saison de basket-ball!</p>
                
                <p style='{Styles.Content}'>Si vous avez des questions, n'hésitez pas à nous contacter à <a href='mailto:info@sainthenribasketball.com'>info@sainthenribasketball.com</a>.</p>"
            );

        public static string GetSeasonStatusUpdateEmail(string userName, string seasonName, string newStatus, string? reasonForChange = null, string? additionalInfo = null) =>
            BuildEmailLayout(
                "Mise à jour du statut de la saison",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Mise à jour du statut de la saison</h2>
                    <p style='{Styles.Content}'>Le statut de la saison <strong>{seasonName}</strong> a changé.</p>
                    
                    <table style='{Styles.Table}'>
                        <tr>
                            <td style='{Styles.TableCell}'>Nouveau statut:</td>
                            <td style='{Styles.TableCell}'><strong>{newStatus}</strong></td>
                        </tr>
                    </table>
                    
                    {(!string.IsNullOrEmpty(reasonForChange) ? $@"
                    <div style='margin-top: 15px; padding: 15px; background-color: #f0f9ff; border-radius: 5px;'>
                        <p style='margin: 0; color: #0369a1;'><strong>Raison du changement:</strong> {reasonForChange}</p>
                    </div>" : "")}
                    
                    {(!string.IsNullOrEmpty(additionalInfo) ? $@"
                    <div style='margin-top: 15px;'>
                        <h3 style='{Styles.Header}'>Informations complémentaires:</h3>
                        <p style='{Styles.Content}'>{additionalInfo}</p>
                    </div>" : "")}
                </div>
                
                <p style='{Styles.Content}'>Pour toute question concernant ce changement, veuillez nous contacter à <a href='mailto:info@sainthenribasketball.com'>info@sainthenribasketball.com</a>.</p>"
            );

        public static string GetSeasonUpdateEmail(string userName, string seasonName, string updateSubject, string updateDetails, string? actionLink = null, string? actionText = null) =>
            BuildEmailLayout(
                $"Mise à jour de la saison - {updateSubject}",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Mise à jour - {seasonName}</h2>
                    <p style='{Styles.Content}'><strong>{updateSubject}</strong></p>
                    <div style='margin-top: 10px;'>
                        <p style='{Styles.Content}'>{updateDetails}</p>
                    </div>
                </div>
                
                {(!string.IsNullOrEmpty(actionLink) && !string.IsNullOrEmpty(actionText) ? $@"
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{actionLink}' style='{Styles.Button}'>
                        {actionText}
                    </a>
                </div>" : "")}
                
                <p style='{Styles.Content}'>Si vous avez des questions concernant cette mise à jour, n'hésitez pas à nous contacter.</p>"
            );

        public static string GetSeasonPaymentReminderEmail(string userName, string seasonName, decimal amountDue, string? paymentLink = null, string? reference = null, string? customMessage = null) =>
            BuildEmailLayout(
                "Rappel de paiement pour la saison",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Rappel de paiement - Saison {seasonName}</h2>
                    <p style='{Styles.Content}'>Nous vous rappelons qu'un paiement de <strong>{amountDue:C}</strong> est en attente pour votre inscription à la saison.</p>
                    
                    {(!string.IsNullOrEmpty(reference) ? $"<p style='{Styles.Content}'>Référence: <strong>{reference}</strong></p>" : "")}
                    {(!string.IsNullOrEmpty(customMessage) ? $"<p style='{Styles.Content}'>{customMessage}</p>" : "")}
                </div>
                
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Options de paiement</h2>
                    <p style='{Styles.Content}'><strong>Option 1: Virement Interac (recommandé)</strong></p>
                    <ol style='{Styles.Content}'>
                        <li>Envoyez un virement Interac à: <strong>pay@sainthenribasketball.com</strong></li>
                        {(!string.IsNullOrEmpty(reference) ? $"<li>Incluez votre numéro de référence: <strong>{reference}</strong></li>" : "")}
                        <li>Utilisez votre nom complet dans le message</li>
                    </ol>
                    
                    {(!string.IsNullOrEmpty(paymentLink) ? $@"
                    <p style='{Styles.Content}'><strong>Option 2: Paiement en ligne</strong></p>
                    <div style='text-align: center; margin: 20px 0;'>
                        <a href='{paymentLink}' 
                           style='background-color: #FF6B1A; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;'>
                            Payer maintenant
                        </a>
                    </div>" : "")}
                </div>
                
                <p style='{Styles.Content}'>Votre paiement nous permettra de finaliser votre inscription et d'assurer le bon déroulement de la saison.</p>
                
                <p style='{Styles.Content}'>Si vous avez des questions concernant ce paiement, n'hésitez pas à nous contacter à <a href='mailto:info@sainthenribasketball.com'>info@sainthenribasketball.com</a>.</p>"
            );
    }
    #endregion

    #region General
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
                                    <td style='{Styles.TableCell}'>{newTime.Value:hh\\:mm}</td>
                                </tr>"
                                : "")}
                        </table>"
                        : "")}
                </div>"
                );

        public static string GetFacilityUpdateEmail(string userName, string facilityName, string updateDetails, DateTime effectiveDate, string? alternativeFacility = null) =>
            BuildEmailLayout(
                "Mise à jour des installations",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Mise à jour concernant les installations</h2>
                    <p style='{Styles.Content}'>Nous souhaitons vous informer d'un changement concernant les installations de <strong>{facilityName}</strong>.</p>
                    
                    <div style='margin-top: 15px; padding: 15px; background-color: #f9fafb; border-radius: 5px;'>
                        <p style='{Styles.Content}'>{updateDetails}</p>
                    </div>
                    
                    <p style='{Styles.Content}'><strong>Date d'effet:</strong> {effectiveDate.ToString("dd MMMM yyyy", FrenchCulture)}</p>
                    
                    {(!string.IsNullOrEmpty(alternativeFacility) ? $@"
                    <div style='margin-top: 15px; padding: 15px; background-color: #f0f9ff; border-radius: 5px;'>
                        <p style='margin: 0; color: #0369a1;'><strong>Installation alternative:</strong> {alternativeFacility}</p>
                    </div>" : "")}
                </div>
                
                <p style='{Styles.Content}'>Nous nous excusons pour tout inconvénient que ce changement pourrait causer. Si vous avez des questions, n'hésitez pas à nous contacter.</p>"
            );

        public static string GetLowAttendanceWarningEmail(string userName, DateTime sessionDate, string startTime, string location) =>
            BuildEmailLayout(
                "Alerte de faible participation",
                $@"<p style='{Styles.Content}'>Bonjour {userName},</p>
                
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Alerte: Faible participation</h2>
                    <p style='{Styles.Content}'>Nous vous informons que la session suivante risque d'être annulée en raison d'un nombre insuffisant de participants:</p>
                    
                    <table style='{Styles.Table}'>
                        <tr>
                            <td style='{Styles.TableCell}'>Date:</td>
                            <td style='{Styles.TableCell}'>{sessionDate.ToString("dddd dd MMMM yyyy", FrenchCulture)}</td>
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
                </div>
                
                <div style='{Styles.InfoBox}'>
                    <p style='{Styles.Content}'>Si vous prévoyez participer à cette session mais n'avez pas encore confirmé votre présence, veuillez le faire dès que possible.</p>
                    <p style='{Styles.Content}'>Nous prendrons une décision finale concernant le maintien de cette session 24 heures avant l'heure prévue.</p>
                </div>
                
                <p style='{Styles.Content}'>Merci de votre compréhension et de votre collaboration.</p>"
            );
    }
    #endregion

    #region Sessions
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
                           style='background-color: #FF6B1A; color: white; padding: 12px 25px; text-decoration: none; border-radius: 3px; display: inline-block;'>
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
    #endregion

    #region Admin
    public static class Admin
    {
        public static string GetAdminNotificationEmail(string adminName, string subject, string message, string? actionLink = null, string? actionText = null) =>
            BuildEmailLayout(
                $"Admin: {subject}",
                $@"<p style='{Styles.Content}'>Bonjour {adminName},</p>
                
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>{subject}</h2>
                    <div style='margin-top: 10px;'>
                        <p style='{Styles.Content}'>{message}</p>
                    </div>
                </div>
                
                {(!string.IsNullOrEmpty(actionLink) && !string.IsNullOrEmpty(actionText) ? $@"
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{actionLink}' style='{Styles.Button}'>
                        {actionText}
                    </a>
                </div>" : "")}
                
                <div style='{Styles.InfoBox}'>
                    <p style='{Styles.Content}'>Cette notification est envoyée uniquement aux administrateurs du système.</p>
                </div>"
            );

        public static string GetNewUserNotificationEmail(string adminName, string newUserName, string newUserEmail, DateTime registrationDate, string? userPlan = null) =>
            BuildEmailLayout(
                "Nouvel utilisateur inscrit",
                $@"<p style='{Styles.Content}'>Bonjour {adminName},</p>
                
                <div style='{Styles.InfoBox}'>
                    <h2 style='{Styles.Header}'>Nouvel utilisateur inscrit</h2>
                    <table style='{Styles.Table}'>
                        <tr>
                            <td style='{Styles.TableCell}'>Nom d'utilisateur:</td>
                            <td style='{Styles.TableCell}'><strong>{newUserName}</strong></td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Courriel:</td>
                            <td style='{Styles.TableCell}'><a href='mailto:{newUserEmail}'>{newUserEmail}</a></td>
                        </tr>
                        <tr>
                            <td style='{Styles.TableCell}'>Date d'inscription:</td>
                            <td style='{Styles.TableCell}'>{registrationDate.ToString("dd MMMM yyyy HH:mm", FrenchCulture)}</td>
                        </tr>
                        {(!string.IsNullOrEmpty(userPlan) ? $@"
                        <tr>
                            <td style='{Styles.TableCell}'>Forfait:</td>
                            <td style='{Styles.TableCell}'>{userPlan}</td>
                        </tr>" : "")}
                    </table>
                </div>
                
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='https://sainthenribasketball.com/admin/users' style='{Styles.Button}'>
                        Gérer les utilisateurs
                    </a>
                </div>"
            );
    }
    #endregion
}