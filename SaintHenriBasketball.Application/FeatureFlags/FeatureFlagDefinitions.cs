namespace SaintHenriBasketball.Application.FeatureFlags;

public record FeatureFlagDefinition(string Key, string Description, string DescriptionFr, bool IsPublic = true);

/// Authoritative list of every feature flag known at compile time. Seeded on startup.
public static class FeatureFlagDefinitions
{
    public static readonly IReadOnlyList<FeatureFlagDefinition> All = new[]
    {
        // Phase 1
        new FeatureFlagDefinition(FeatureFlagKeys.CalendarSync,
            "Per-user .ics calendar feed for registered sessions.",
            "Flux de calendrier .ics personnel pour les séances inscrites."),
        new FeatureFlagDefinition(FeatureFlagKeys.AuditLogViewer,
            "Admin UI for browsing the audit log.",
            "Interface d'administration pour consulter le journal d'audit.",
            IsPublic: false),
        new FeatureFlagDefinition(FeatureFlagKeys.Admin2fa,
            "Require TOTP two-factor authentication for admin accounts.",
            "Exiger une authentification à deux facteurs TOTP pour les comptes administrateurs.",
            IsPublic: false),
        new FeatureFlagDefinition(FeatureFlagKeys.PwaInstall,
            "Progressive Web App install prompt and offline shell.",
            "Installation PWA et coque hors ligne."),

        // Phase 2
        new FeatureFlagDefinition(FeatureFlagKeys.QrCheckIn,
            "QR-code-based session check-in flow.",
            "Inscription à la séance via code QR."),
        new FeatureFlagDefinition(FeatureFlagKeys.StreaksBadges,
            "Attendance streaks and badge awards.",
            "Séries de présences et badges."),
        new FeatureFlagDefinition(FeatureFlagKeys.SessionFeedback,
            "Post-session NPS rating and comment.",
            "Évaluation NPS et commentaires après la séance."),
        new FeatureFlagDefinition(FeatureFlagKeys.PersonalStats,
            "Personal stats page (My SHB Year).",
            "Page des statistiques personnelles (Mon année SHB)."),
        new FeatureFlagDefinition(FeatureFlagKeys.SessionRecaps,
            "Admin-uploaded session photo recaps.",
            "Récapitulatifs photo des séances publiés par l'administrateur."),

        // Phase 3
        new FeatureFlagDefinition(FeatureFlagKeys.RecurringSessions,
            "Recurring session generator from a template.",
            "Générateur de séances récurrentes à partir d'un modèle.",
            IsPublic: false),
        new FeatureFlagDefinition(FeatureFlagKeys.InteracReconciliation,
            "Admin dashboard for reconciling Interac payments.",
            "Tableau de bord administrateur pour rapprocher les paiements Interac.",
            IsPublic: false),
        new FeatureFlagDefinition(FeatureFlagKeys.AdminBroadcast,
            "Admin broadcast / announcement composer.",
            "Compositeur de diffusion / annonce administrateur.",
            IsPublic: false),

        // Phase 4
        new FeatureFlagDefinition(FeatureFlagKeys.TaxReceipts,
            "Annual tax-receipt PDF generation.",
            "Génération annuelle des reçus fiscaux en PDF."),
        new FeatureFlagDefinition(FeatureFlagKeys.Referrals,
            "Referral codes and invite-a-friend rewards.",
            "Codes de parrainage et récompenses d'invitation."),
        new FeatureFlagDefinition(FeatureFlagKeys.PromoCodes,
            "Admin-defined promo / discount codes at checkout.",
            "Codes promotionnels définis par l'administrateur au paiement."),

        // Phase 5
        new FeatureFlagDefinition(FeatureFlagKeys.Waiver,
            "Liability-waiver acceptance at registration.",
            "Acceptation de la décharge de responsabilité à l'inscription."),
        new FeatureFlagDefinition(FeatureFlagKeys.EmergencyProfile,
            "Emergency contact and medical alerts on the user profile.",
            "Contact d'urgence et alertes médicales sur le profil."),
        new FeatureFlagDefinition(FeatureFlagKeys.SmsReminders,
            "SMS session-day reminders via Twilio.",
            "Rappels SMS le jour de la séance via Twilio."),
        new FeatureFlagDefinition(FeatureFlagKeys.PublicSchedule,
            "Public anonymous schedule / upcoming-sessions page.",
            "Page publique anonyme du calendrier / séances à venir."),
        new FeatureFlagDefinition(FeatureFlagKeys.InAppNotifications,
            "Bell icon + in-app notification center.",
            "Icône cloche et centre de notifications dans l'application."),

        // Phase 7
        new FeatureFlagDefinition(FeatureFlagKeys.Gallery,
            "Public /gallery page mirroring the Instagram feed.",
            "Page publique /galerie miroir du fil Instagram."),
    };
}
