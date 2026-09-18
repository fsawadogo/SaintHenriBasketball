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

    // Phase 8 — Player engagement & payment follow-up
    public const string SessionAttendees = "session-attendees";
    public const string SeasonPaymentReminders = "season-payment-reminders";
    public const string InteracReviewQueue = "interac-review-queue";
    public const string SeasonCardPayments = "season-card-payments";

    // Phase 9 — Admin audit: new admin tools
    public const string OutstandingBalances = "outstanding-balances";
    public const string CourtAttendance = "court-attendance";
    public const string TreasurerReport = "treasurer-report";
    public const string PlayerTimeline = "player-timeline";
    public const string SeasonRollover = "season-rollover";
    public const string WaitlistAdmin = "waitlist-admin";
    public const string PromoReferralReports = "promo-referral-reports";
    public const string VolunteerRoles = "volunteer-roles";
    public const string SeasonDashboard = "season-dashboard";
    public const string SignupFunnel = "signup-funnel";
    public const string InteracAutoMatch = "interac-auto-match";
    public const string SeasonPlanChoiceEmail = "season-plan-choice-email";

    // Emails a player back when they pick how they will pay for a season.
    public const string PlanChoiceConfirmationEmail = "plan-choice-confirmation-email";

    // Lets the daily job send the plan-choice email by itself. Without this the email exists and an
    // admin can send it, but nothing goes out unattended.
    public const string SeasonPlanChoiceEmailAuto = "season-plan-choice-email-auto";

    // Season schedule wizard — create a season and all its sessions in one step.
    public const string SeasonScheduleWizard = "season-schedule-wizard";

    // Redesigned admin seasons page — one hero for the open season, rows for the rest.
    public const string SeasonsPageRedesign = "seasons-page-redesign";

    // Per-season plan choice: the after-login prompt, the season pass spot limit, and the admin reset.
    public const string SeasonPlanChoice = "season-plan-choice";

    // Reworked player dashboard: season block, balance owed, and a different layout on match day.
    public const string PlayerDashboardV2 = "player-dashboard-v2";
}
