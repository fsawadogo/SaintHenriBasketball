namespace SaintHenriBasketball.Domain.Enums;

public enum EmailType
{
    // Authentication Emails
    EmailConfirmation = 0 ,
    PasswordReset = 1,
    
    // Payment Related
    PaymentConfirmation = 2,
    PaymentReminder = 3,
    PaymentPlanUpdate = 4,
    
    // Attendance Related
    AttendanceConfirmation = 5,
    AttendanceUpdate = 6,
    AttendanceReminder = 7,
    
    // Season Related
    SeasonRegistrationConfirmation = 8,
    SeasonRegistrationCancelled = 9,
    SeasonRegistrationReminder = 10,
    SeasonStatusUpdate = 11,
    SeasonUpdate = 12,
    SeasonPaymentReminder = 13,
    
    // General Communications
    GeneralAnnouncement = 14,
    FacilityUpdate = 15,
    ScheduleChange = 16,
    LowAttendanceWarning = 17,
    
    // Admin Notifications
    AdminNotification = 18,
    NewUserNotification = 19
}