namespace SaintHenriBasketball.Application.FeatureFlags;

/// Central registry of every feature flag used in the app.
/// Each key must be added to <see cref="FeatureFlagDefinitions.All"/> so it is seeded on startup.
public static class FeatureFlagKeys
{
    // Phase 1 — Foundation & Quick Wins
    public const string CalendarSync = "calendar-sync";
    public const string AuditLogViewer = "audit-log-viewer";
    public const string Admin2fa = "admin-2fa";
    public const string PwaInstall = "pwa-install";

    // Phase 2 — Player Delight
    public const string QrCheckIn = "qr-check-in";
    public const string StreaksBadges = "streaks-badges";
    public const string SessionFeedback = "session-feedback";
    public const string PersonalStats = "personal-stats";
    public const string SessionRecaps = "session-recaps";

    // Phase 3 — Admin Operations
    public const string RecurringSessions = "recurring-sessions";
    public const string InteracReconciliation = "interac-reconciliation";
    public const string AdminBroadcast = "admin-broadcast";

    // Phase 4 — Revenue & Compliance
    public const string TaxReceipts = "tax-receipts";
    public const string Referrals = "referrals";
    public const string PromoCodes = "promo-codes";

    // Phase 5 — Safety, Communication, Public
    public const string Waiver = "waiver";
    public const string EmergencyProfile = "emergency-profile";
    public const string SmsReminders = "sms-reminders";
    public const string PublicSchedule = "public-schedule";

    // Phase 6 — In-app notifications
    public const string InAppNotifications = "in-app-notifications";

    // Phase 7 — Public marketing
    public const string Gallery = "gallery";
}
